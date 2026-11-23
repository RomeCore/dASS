using AsyncLua;
using AsyncLua.Values;
using Serilog;

namespace LLMDesktopAssistant.Scripting.Lua.Scheduling
{
	/// <summary>
	/// One scheduled job of a chat, exposed to Lua as a <c>schedule</c> handle.
	/// </summary>
	/// <remarks>
	/// The job owns its own loop and its own cancellation, while the context it fires with is built from
	/// the chat lifetime token: a job outlives the tool call, the turn and Stop, and dies with the chat.
	/// </remarks>
	public class LuaScheduleHandle : ILuaCallbackOwner
	{
		private const int failureLimit = 3;

		private readonly ChatScheduler _owner;
		private readonly LuaFunction _function;
		private readonly LuaService _lua;
		private readonly CancellationToken _chatLifetime;
		private readonly string _kind;
		private readonly string _spec;

		private readonly long _periodMs;
		private readonly long _delayMs;
		private readonly long _fromMs;
		private readonly bool _grid;

		private readonly CronExpression? _cron;
		private readonly string? _catchSlot;

		private readonly long _atMs;

		private readonly List<LuaCallbackRegistration> _failureSubscriptions = [];
		private readonly List<LuaCallbackRegistration> _disableSubscriptions = [];

		private readonly Lock _lock = new();
		private CancellationTokenSource? _cts;
		private Task? _loop;
		private long _nextRunMs;
		private bool _running;
		private bool _enabled = true;
		private bool _completed;
		private int _runCount;
		private int _failCount;
		private int _skipped;
		private double? _lastRun;
		private string? _lastError;

		private LuaScheduleHandle(ChatScheduler owner, int id, string kind, string spec, LuaFunction function,
			LuaService lua, long periodMs, long delayMs, long fromMs, bool grid, CronExpression? cron,
			string? catchSlot, long atMs)
		{
			_owner = owner;
			_function = function;
			_lua = lua;
			_chatLifetime = owner.ChatLifetime;
			_kind = kind;
			_spec = spec;
			_periodMs = periodMs;
			_delayMs = delayMs;
			_fromMs = fromMs;
			_grid = grid;
			_cron = cron;
			_catchSlot = catchSlot;
			_atMs = atMs;
			Id = id;
		}

		/// <summary>
		/// Creates a job that repeats on a millisecond grid.
		/// </summary>
		public static LuaScheduleHandle ForEvery(ChatScheduler owner, int id, LuaFunction function, LuaService lua,
			long periodMs, long delayMs, long fromMs, bool grid)
		{
			var spec = $"every(period={periodMs}ms, delay={delayMs}ms, grid={(grid ? "true" : "false")})";
			return new LuaScheduleHandle(owner, id, "every", spec, function, lua, periodMs, delayMs, fromMs, grid,
				null, null, 0);
		}

		/// <summary>
		/// Creates a job that follows a cron expression.
		/// </summary>
		public static LuaScheduleHandle ForCron(ChatScheduler owner, int id, LuaFunction function, LuaService lua,
			CronExpression cron, string? catchSlot)
		{
			var spec = $"cron({cron}, catch_slot={(catchSlot is null ? "nil" : catchSlot)})";
			return new LuaScheduleHandle(owner, id, "cron", spec, function, lua, 0, 0, 0, false,
				cron, catchSlot, 0);
		}

		/// <summary>
		/// Creates a job that fires once at the given Unix millisecond timestamp.
		/// </summary>
		public static LuaScheduleHandle ForAt(ChatScheduler owner, int id, LuaFunction function, LuaService lua,
			long atMs)
		{
			var spec = $"at({DateTimeOffset.FromUnixTimeMilliseconds(atMs):O})";
			return new LuaScheduleHandle(owner, id, "at", spec, function, lua, 0, 0, 0, false,
				null, null, atMs);
		}

		/// <summary>
		/// The per-chat identifier of the job.
		/// </summary>
		public int Id { get; }

		/// <summary>
		/// The trigger kind: <c>every</c>, <c>cron</c> or <c>at</c>.
		/// </summary>
		public string Kind => _kind;

		/// <summary>
		/// The trigger written as text, for logs and listings.
		/// </summary>
		public string Spec => _spec;

		/// <summary>
		/// Whether the job still fires. Setting it to <see langword="false"/> stops the job and keeps it
		/// in the list, so it can be brought back without re-registering it.
		/// </summary>
		public bool Enabled
		{
			get => _enabled;
			set
			{
				lock (_lock)
				{
					if (_enabled == value)
						return;

					_enabled = value;
				}

				if (value)
					Start();
				else
					Stop();
			}
		}

		/// <summary>
		/// Whether a firing is running right now.
		/// </summary>
		public bool Running => _running;

		/// <summary>
		/// The Unix timestamp of the next firing, or <see langword="null"/> when the job is disabled or done.
		/// </summary>
		public double? NextRun
		{
			get
			{
				lock (_lock)
					return _enabled && !_completed && _nextRunMs > 0 ? _nextRunMs / 1000.0 : null;
			}
		}

		/// <summary>
		/// How many firings were attempted, failures included.
		/// </summary>
		public int RunCount => _runCount;

		/// <summary>
		/// How many firings failed in a row.
		/// </summary>
		public int FailCount => _failCount;

		/// <summary>
		/// How many cron occurrences were dropped because the previous firing was still running.
		/// </summary>
		public int Skipped => _skipped;

		/// <summary>
		/// The Unix timestamp of the last firing, or <see langword="null"/> before the first one.
		/// </summary>
		public double? LastRun => _lastRun;

		/// <summary>
		/// The message of the last failure, or <see langword="null"/>.
		/// </summary>
		public string? LastError => _lastError;

		/// <summary>
		/// Removes the job from the chat's schedule.
		/// </summary>
		/// <returns><see langword="true"/> when the job was still registered.</returns>
		public bool Cancel() => _owner.Remove(this);

		/// <summary>
		/// Wraps the job into a <c>schedule</c> userdata.
		/// </summary>
		[LuaHidden]
		public static LuaUserData Wrap(LuaScheduleHandle handle)
			=> new(handle, "schedule")
			{
				Metatable = UserDataMetatableGenerator.GetOrCreate(typeof(LuaScheduleHandle))
			};

		/// <summary>
		/// Starts the firing loop unless it is already running or the job is done.
		/// </summary>
		[LuaHidden]
		public void Start()
		{
			lock (_lock)
			{
				if (!_enabled || _completed)
					return;

				if (_loop is { IsCompleted: false })
					return;

				_cts = new CancellationTokenSource();
				var token = _cts.Token;
				_nextRunMs = FirstDueMs();
				_loop = Task.Run(() => _kind switch
				{
					"every" => RunEveryAsync(token),
					"cron" => RunCronAsync(token),
					_ => RunAtAsync(token),
				});
			}
		}

		/// <summary>
		/// Cancels the firing loop. A firing that is already running is left alone.
		/// </summary>
		[LuaHidden]
		public void Stop()
		{
			lock (_lock)
			{
				try
				{
					_cts?.Cancel();
				}
				catch
				{
				}

				_cts = null;
				_loop = null;
				_nextRunMs = 0;
			}
		}

		/// <summary>
		/// Fires on a millisecond grid and reports how many grid steps were skipped before each firing.
		/// </summary>
		private async Task RunEveryAsync(CancellationToken cancellationToken)
		{
			var period = _periodMs;
			var now = NowMs();
			var due = FirstDueMs();
			var pendingMissed = 0;

			// The first firing catches the grid up: the steps that passed while the job was not registered
			// are ignored, and their number is what the callback sees.
			if (_grid && now > due)
				pendingMissed = (int)Math.Min((due - (_fromMs + _delayMs)) / period, int.MaxValue);

			while (!cancellationToken.IsCancellationRequested)
			{
				lock (_lock)
				{
					if (!_enabled)
						return;

					_nextRunMs = due;
				}

				var wait = due - NowMs();
				if (wait > 0)
				{
					try
					{
						await Task.Delay(TimeSpan.FromMilliseconds(wait), cancellationToken).ConfigureAwait(false);
					}
					catch (OperationCanceledException)
					{
						return;
					}
				}

				if (cancellationToken.IsCancellationRequested)
					return;

				await FireAsync(pendingMissed, cancellationToken).ConfigureAwait(false);

				now = NowMs();
				if (_grid)
				{
					due += period;
					if (due <= now)
					{
						var missedSteps = (now - due) / period + 1;
						pendingMissed = (int)Math.Min(missedSteps, int.MaxValue);
						due += missedSteps * period;
					}
					else
					{
						pendingMissed = 0;
					}
				}
				else
				{
					due = now + period;
					pendingMissed = 0;
				}
			}
		}

		/// <summary>
		/// The first grid point of a job: the registration instant itself when <c>from</c> has not passed,
		/// and the current grid position when it has.
		/// </summary>
		private long FirstDueMs()
		{
			var origin = _fromMs + _delayMs;
			var now = NowMs();

			if (_kind == "at")
				return _atMs;

			if (_kind == "cron")
				return ToUnixMs(_cron!.NextOccurrence(DateTime.Now));

			if (now <= origin)
				return origin;

			if (!_grid)
				return now;

			return origin + (now - origin) / _periodMs * _periodMs;
		}

		/// <summary>
		/// Fires on the calendar and reports how many occurrences were missed across the registration.
		/// </summary>
		private async Task RunCronAsync(CancellationToken cancellationToken)
		{
			var cron = _cron!;
			var pendingMissed = 0;
			var fireNow = false;

			if (_catchSlot is not null)
			{
				var stamp = _owner.TryGetSlotStamp(_catchSlot);
				if (stamp is null)
				{
					pendingMissed = -1;
					fireNow = true;
				}
				else
				{
					var missed = cron.CountOccurrences(stamp.Value, DateTime.Now);
					if (missed > 0)
					{
						pendingMissed = missed;
						fireNow = true;
					}
				}
			}

			if (fireNow)
				await FireAsync(pendingMissed, cancellationToken).ConfigureAwait(false);

			while (!cancellationToken.IsCancellationRequested)
			{
				var due = cron.NextOccurrence(DateTime.Now);

				lock (_lock)
				{
					if (!_enabled)
						return;

					_nextRunMs = ToUnixMs(due);
				}

				var wait = due - DateTime.Now;
				if (wait > TimeSpan.Zero)
				{
					try
					{
						await Task.Delay(wait, cancellationToken).ConfigureAwait(false);
					}
					catch (OperationCanceledException)
					{
						return;
					}
				}

				if (cancellationToken.IsCancellationRequested)
					return;

				await FireAsync(0, cancellationToken).ConfigureAwait(false);

				var skipped = cron.CountOccurrences(due, DateTime.Now);
				if (skipped > 0)
				{
					lock (_lock)
						_skipped += skipped;
				}
			}
		}

		/// <summary>
		/// Fires once at the registered timestamp and completes.
		/// </summary>
		private async Task RunAtAsync(CancellationToken cancellationToken)
		{
			lock (_lock)
				_nextRunMs = _atMs;

			var wait = _atMs - NowMs();
			if (wait > 0)
			{
				try
				{
					await Task.Delay(TimeSpan.FromMilliseconds(wait), cancellationToken).ConfigureAwait(false);
				}
				catch (OperationCanceledException)
				{
					return;
				}
			}

			if (cancellationToken.IsCancellationRequested)
				return;

			await FireAsync(null, cancellationToken).ConfigureAwait(false);

			lock (_lock)
			{
				_completed = true;
				_nextRunMs = 0;
			}
		}

		/// <summary>
		/// Runs the Lua function once, with the missed count and the previous firing timestamp.
		/// </summary>
		private async Task FireAsync(int? missed, CancellationToken cancellationToken)
		{
			double? previous;

			lock (_lock)
			{
				if (!_enabled || cancellationToken.IsCancellationRequested)
					return;

				_running = true;
				previous = _lastRun;
				_lastRun = NowMs() / 1000.0;
			}

			try
			{
				var context = _owner.CreateContext(_lua, $"{Id} {_spec}");

				var lastShot = _catchSlot is not null ? _owner.TryGetSlotStampSeconds(_catchSlot) : previous;
				if (_catchSlot is not null)
					_owner.WriteSlotStamp(_catchSlot, DateTime.Now);

				LuaValue[] arguments =
				[
					missed is null ? LuaNil.Instance : new LuaNumber(missed.Value),
					lastShot is null ? LuaNil.Instance : new LuaNumber(lastShot.Value),
				];

				await _function.InvokeAsync(context, arguments).ConfigureAwait(false);

				lock (_lock)
				{
					_runCount++;
					_failCount = 0;
				}
			}
			catch (OperationCanceledException) when (_chatLifetime.IsCancellationRequested)
			{
			}
			catch (Exception ex)
			{
				int failures;
				var reason = Reason(ex);

				lock (_lock)
				{
					_runCount++;
					_failCount++;
					failures = _failCount;
					_lastError = reason;
				}

				Log.Warning(ex, "Scheduled job {Id} ({Spec}) failed: {Error}", Id, _spec, ex.Message);

				await RaiseFailureAsync(reason, failures).ConfigureAwait(false);

				if (failures >= failureLimit)
				{
					Enabled = false;
					await RaiseDisabledAsync(reason).ConfigureAwait(false);
				}
			}
			finally
			{
				lock (_lock)
					_running = false;
			}
		}

		/// <summary>
		/// Subscribes a function to the failures of this job alone.
		/// </summary>
		public LuaUserData OnFailure(LuaCallingContext context, LuaFunction function)
		{
			var registration = new LuaCallbackRegistration(this, function, context);

			lock (_lock)
				_failureSubscriptions.Add(registration);

			return LuaCallbackRegistration.Wrap(registration);
		}

		/// <summary>
		/// Subscribes a function to the auto-disable of this job alone.
		/// </summary>
		public LuaUserData OnFailureDisable(LuaCallingContext context, LuaFunction function)
		{
			var registration = new LuaCallbackRegistration(this, function, context);

			lock (_lock)
				_disableSubscriptions.Add(registration);

			return LuaCallbackRegistration.Wrap(registration);
		}

		/// <summary>
		/// Checks whether a subscription of this job is still armed.
		/// </summary>
		[LuaHidden]
		public bool Contains(LuaCallbackRegistration registration)
		{
			lock (_lock)
				return _failureSubscriptions.Contains(registration) || _disableSubscriptions.Contains(registration);
		}

		/// <summary>
		/// Removes a subscription of this job.
		/// </summary>
		[LuaHidden]
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
		/// Tells this job's subscribers, then the chat's, that a firing failed.
		/// </summary>
		private async Task RaiseFailureAsync(string error, int failCount)
		{
			LuaCallbackRegistration[] snapshot;

			lock (_lock)
				snapshot = [.. _failureSubscriptions];

			if (snapshot.Length > 0)
			{
				LuaValue[] arguments =
				[
					LuaScheduleHandle.Wrap(this),
					new LuaString(error),
					new LuaNumber(failCount),
				];

				await ChatScheduler.RaiseAsync(snapshot, _owner.CreateContext(_lua, $"{Id} {_spec}"), arguments).ConfigureAwait(false);
			}

			await _owner.RaiseFailureAsync(this, error, failCount).ConfigureAwait(false);
		}

		/// <summary>
		/// Tells this job's subscribers, then the chat's, that the job disabled itself.
		/// </summary>
		private async Task RaiseDisabledAsync(string error)
		{
			LuaCallbackRegistration[] snapshot;

			lock (_lock)
				snapshot = [.. _disableSubscriptions];

			if (snapshot.Length > 0)
			{
				LuaValue[] arguments = [LuaScheduleHandle.Wrap(this), new LuaString(error)];

				await ChatScheduler.RaiseAsync(snapshot, _owner.CreateContext(_lua, $"{Id} {_spec}"), arguments).ConfigureAwait(false);
			}

			await _owner.RaiseDisabledAsync(this, error).ConfigureAwait(false);
		}

		/// <summary>
		/// The bare message of a failure: a Lua exception carries the source location in its text, which is
		/// noise in a property and in a subscription argument.
		/// </summary>
		private static string Reason(Exception exception)
			=> exception is LuaRuntimeException lua ? lua.OriginalMessage : exception.Message;

		/// <summary>
		/// The wall clock the grid and the deadlines are measured against.
		/// </summary>
		private static long NowMs() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

		private static long ToUnixMs(DateTime local)
			=> new DateTimeOffset(local).ToUnixTimeMilliseconds();
	}
}
