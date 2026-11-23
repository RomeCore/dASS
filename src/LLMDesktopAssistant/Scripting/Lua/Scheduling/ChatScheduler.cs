using AsyncLua;
using AsyncLua.Values;
using LLMDesktopAssistant.LLM.Domain;
using LLMDesktopAssistant.LLM.Services;
using LLMDesktopAssistant.Services.Instances;
using Serilog;

namespace LLMDesktopAssistant.Scripting.Lua.Scheduling
{
	/// <summary>
	/// Owns the scheduled jobs of one chat.
	/// </summary>
	/// <remarks>
	/// A job belongs to the chat, not to the script that registered it: it survives the tool call, the turn
	/// and Stop, and dies with the chat. Reloading the addons rebuilds the Lua state, so <see cref="Reset"/>
	/// drops every job and every failure subscription - their functions belong to the state being thrown away.
	/// </remarks>
	[ChatService]
	[ChatService(typeof(IChatClosingHandler))]
	public class ChatScheduler(Chat chat) : Disposable, IChatClosingHandler, ILuaCallbackOwner
	{
		private readonly List<LuaScheduleHandle> _jobs = [];
		private readonly List<LuaCallbackRegistration> _failureSubscriptions = [];
		private readonly List<LuaCallbackRegistration> _disableSubscriptions = [];
		private readonly Lock _lock = new();
		private readonly CancellationTokenSource _chatLifetime = new();
		private int _nextId = 1;

		/// <summary>
		/// The token every firing and every failure callback is invoked with: it cancels when the chat closes.
		/// </summary>
		public CancellationToken ChatLifetime => _chatLifetime.Token;

		/// <summary>
		/// The Lua state the scheduler fires into. Set by <c>dass.schedule</c> while it populates its namespace.
		/// </summary>
		public LuaService? Lua { get; set; }

		/// <summary>
		/// How many jobs are registered right now.
		/// </summary>
		public int Count
		{
			get
			{
				lock (_lock)
					return _jobs.Count;
			}
		}

		/// <summary>
		/// Takes the next per-chat job id.
		/// </summary>
		public int TakeId()
		{
			lock (_lock)
				return _nextId++;
		}

		/// <summary>
		/// Adds a job to the chat and starts it.
		/// </summary>
		public void Register(LuaScheduleHandle handle)
		{
			lock (_lock)
				_jobs.Add(handle);

			handle.Start();
		}

		/// <summary>
		/// Removes and stops one job.
		/// </summary>
		/// <returns><see langword="true"/> when the job was still registered.</returns>
		public bool Remove(LuaScheduleHandle handle)
		{
			lock (_lock)
			{
				if (!_jobs.Remove(handle))
					return false;
			}

			handle.Stop();
			return true;
		}

		/// <summary>
		/// The registered jobs, disabled ones included.
		/// </summary>
		public IReadOnlyList<LuaScheduleHandle> Snapshot()
		{
			lock (_lock)
				return [.. _jobs];
		}

		/// <summary>
		/// Stops and removes every job, leaving the chat itself alive.
		/// </summary>
		/// <returns>How many jobs were removed.</returns>
		public int Clear()
		{
			List<LuaScheduleHandle> removed;

			lock (_lock)
			{
				removed = [.. _jobs];
				_jobs.Clear();
			}

			foreach (var job in removed)
				job.Stop();

			return removed.Count;
		}

		/// <summary>
		/// Subscribes one function to the failures of every job of the chat.
		/// </summary>
		public LuaCallbackRegistration RegisterFailure(LuaFunction function, LuaCallingContext context)
			=> AddSubscription(_failureSubscriptions, function, context);

		/// <summary>
		/// Subscribes one function to the auto-disable of every job of the chat.
		/// </summary>
		public LuaCallbackRegistration RegisterDisabled(LuaFunction function, LuaCallingContext context)
			=> AddSubscription(_disableSubscriptions, function, context);

		/// <summary>
		/// Checks whether a failure subscription is still armed.
		/// </summary>
		public bool Contains(LuaCallbackRegistration registration)
		{
			lock (_lock)
				return _failureSubscriptions.Contains(registration) || _disableSubscriptions.Contains(registration);
		}

		/// <summary>
		/// Removes a failure subscription.
		/// </summary>
		/// <returns><see langword="true"/> when it was still armed.</returns>
		public bool Unregister(LuaCallbackRegistration registration)
		{
			lock (_lock)
			{
				var removed = _failureSubscriptions.Remove(registration);
				removed |= _disableSubscriptions.Remove(registration);
				return removed;
			}
		}

		/// <summary>
		/// Tells every subscriber that one firing failed.
		/// </summary>
		public async Task RaiseFailureAsync(LuaScheduleHandle job, string error, int failCount)
		{
			if (Lua is not { } lua)
				return;

			LuaCallbackRegistration[] snapshot;

			lock (_lock)
				snapshot = [.. _failureSubscriptions];

			if (snapshot.Length == 0)
				return;

			LuaValue[] arguments = [LuaScheduleHandle.Wrap(job), new LuaString(error), new LuaNumber(failCount)];

			await RaiseAsync(snapshot, CreateContext(lua, "subscription"), arguments).ConfigureAwait(false);
		}

		/// <summary>
		/// Tells every subscriber that a job disabled itself after repeated failures.
		/// </summary>
		public async Task RaiseDisabledAsync(LuaScheduleHandle job, string error)
		{
			if (Lua is not { } lua)
				return;

			LuaCallbackRegistration[] snapshot;

			lock (_lock)
				snapshot = [.. _disableSubscriptions];

			if (snapshot.Length == 0)
				return;

			LuaValue[] arguments = [LuaScheduleHandle.Wrap(job), new LuaString(error)];

			await RaiseAsync(snapshot, CreateContext(lua, "subscription"), arguments).ConfigureAwait(false);
		}

		/// <summary>
		/// Runs every callback of a snapshot. A failing subscriber is logged and never stops the rest.
		/// </summary>
		public static async Task RaiseAsync(IReadOnlyList<LuaCallbackRegistration> subscriptions,
			LuaCallingContext context, LuaValue[] arguments)
		{
			foreach (var subscription in subscriptions)
			{
				try
				{
					await subscription.Function.InvokeAsync(context, arguments).ConfigureAwait(false);
				}
				catch (Exception ex)
				{
					Log.Warning(ex, "A dass.schedule failure subscription failed: {Error}", ex.Message);
				}
			}
		}

		/// <summary>
		/// Builds a firing context: the chat lifetime token, the live globals, and the log as the print sink.
		/// </summary>
		public LuaCallingContext CreateContext(LuaService lua, string source)
		{
			var state = lua.GetState();

			return new LuaCallingContext(state, state.Globals)
			{
				CancellationToken = ChatLifetime,
				Ambient = state.AmbientContext,
				Print = line => Log.Warning("[schedule {Source}] {Line}", source, line),
				Warn = line => Log.Warning("[schedule {Source}] {Line}", source, line),
			};
		}

		/// <summary>
		/// Drops every job and every failure subscription, keeping the chat alive.
		/// </summary>
		public void Reset()
		{
			Clear();
			ClearSubscriptions();
		}

		/// <summary>
		/// Cancels the chat lifetime token and drops everything the scheduler holds.
		/// </summary>
		public void Stop()
		{
			try
			{
				_chatLifetime.Cancel();
			}
			catch
			{
			}

			Clear();
			ClearSubscriptions();
		}

		/// <summary>
		/// Reads the last firing time stored under a slot key.
		/// </summary>
		public DateTime? TryGetSlotStamp(string slot)
			=> GetSlotData().LastShots.TryGetValue(slot, out var stamp) ? stamp : null;

		/// <summary>
		/// Reads a slot as Unix seconds, the unit the Lua callback receives.
		/// </summary>
		public double? TryGetSlotStampSeconds(string slot)
		{
			var stamp = TryGetSlotStamp(slot);
			return stamp is null ? null : new DateTimeOffset(stamp.Value).ToUnixTimeMilliseconds() / 1000.0;
		}

		/// <summary>
		/// Records that a slot fired.
		/// </summary>
		public void WriteSlotStamp(string slot, DateTime at)
			=> GetSlotData().LastShots[slot] = at;

		/// <summary>
		/// The slot row of this chat, created on first use.
		/// </summary>
		private ScheduleSlotChatData GetSlotData()
		{
			if (chat.AdditionalData.TryGet<ScheduleSlotChatData>(out var existing))
				return existing;

			var created = new ScheduleSlotChatData();
			chat.AdditionalData.Add(created);
			return created;
		}

		private LuaCallbackRegistration AddSubscription(List<LuaCallbackRegistration> subscriptions,
			LuaFunction function, LuaCallingContext context)
		{
			var registration = new LuaCallbackRegistration(this, function, context);

			lock (_lock)
				subscriptions.Add(registration);

			return registration;
		}

		private void ClearSubscriptions()
		{
			lock (_lock)
			{
				_failureSubscriptions.Clear();
				_disableSubscriptions.Clear();
			}
		}

		void IChatClosingHandler.OnChatClosing() => Stop();

		protected override void Dispose(bool disposing)
		{
			base.Dispose(disposing);

			if (disposing)
				Stop();
		}
	}
}
