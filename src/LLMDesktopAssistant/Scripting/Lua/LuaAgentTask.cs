using AsyncLua;
using AsyncLua.Values;
using LLMDesktopAssistant.Agents.Tasks;

namespace LLMDesktopAssistant.Scripting.Lua
{
	/// <summary>
	/// The <c>agent_task</c> handle that <c>dass.agent.execute_task</c> returns to Lua.
	/// </summary>
	public class LuaAgentTask
	{
		private static readonly LuaTable _metatable = CreateMetatable();

		private readonly AgentTask _task;
		private readonly TimeSpan? _timeout;
		private LuaUserData _self = null!;

		private LuaAgentTask(AgentTask task, TimeSpan? timeout)
		{
			_task = task;
			_timeout = timeout;
		}

		/// <summary>
		/// The unique identifier of the task.
		/// </summary>
		public string Id => _task.Id.ToString();

		/// <summary>
		/// The current status: <c>pending</c>, <c>executing</c>, <c>success</c>, <c>failed</c>, <c>cancelled</c>
		/// or <c>blocked</c>.
		/// </summary>
		public string Status => _task.Status switch
		{
			AgentTaskStatus.Pending => "pending",
			AgentTaskStatus.Executing => "executing",
			AgentTaskStatus.Success => "success",
			AgentTaskStatus.Failed => "failed",
			AgentTaskStatus.Cancelled => "cancelled",
			AgentTaskStatus.Blocked => "blocked",
			_ => _task.Status.ToString().ToLowerInvariant()
		};

		/// <summary>
		/// Whether the current launch has settled, whatever the outcome.
		/// </summary>
		/// <remarks>
		/// Read from the completion, not from <see cref="AgentTask.Completed"/>: the flag is set in the executor's
		/// <c>finally</c>, so a continuation released by the completion source can observe it before it flips.
		/// </remarks>
		public bool Completed => _task.Completion.IsCompleted;

		/// <summary>
		/// The failure message, or <see langword="nil"/> while running, after success and when blocked.
		/// </summary>
		public LuaValue Error =>
			_task.BlockReason is null && _task.Exception is { } exception
				? new LuaString(exception.Message)
				: LuaNil.Instance;

		/// <summary>
		/// The exception type behind <see cref="Error"/>, or <see langword="nil"/>.
		/// </summary>
		public LuaValue ErrorType =>
			_task.BlockReason is null && _task.Exception is { } exception
				? new LuaString(exception.GetType().Name)
				: LuaNil.Instance;

		/// <summary>
		/// Why a <c>task.start</c> hook refused the launch, or <see langword="nil"/>.
		/// </summary>
		public LuaValue BlockReason =>
			_task.BlockReason is { } reason ? new LuaString(reason) : LuaNil.Instance;

		/// <summary>
		/// The text of the last generated assistant message, or <see langword="nil"/> before the first response.
		/// </summary>
		public LuaValue LastContent =>
			_task.LastGeneratedContent is { } content ? new LuaString(content) : LuaNil.Instance;

		/// <summary>
		/// Usage totals for the whole task, or <see langword="nil"/> before the first response.
		/// </summary>
		public LuaValue Usage =>
			_task.UsageStatistics is { } usage ? LuaAgentMessageConverter.UsageToLua(usage) : LuaNil.Instance;

		/// <summary>
		/// Usage totals for the last launch only, or <see langword="nil"/> before the first response.
		/// </summary>
		public LuaValue NewUsage =>
			_task.NewUsageStatistics is { } usage ? LuaAgentMessageConverter.UsageToLua(usage) : LuaNil.Instance;

		/// <summary>
		/// The whole conversation, the messages passed to the launch included.
		/// </summary>
		public LuaTable Messages => ToLuaMessages(_task.Messages);

		/// <summary>
		/// Every message produced after the ones passed to the launch.
		/// </summary>
		public LuaTable AddedMessages => ToLuaMessages(_task.Messages.Skip(_task.InitialMessageCount));

		/// <summary>
		/// Every message produced by the current launch, follow-ups sent through the handle included.
		/// </summary>
		public LuaTable NewMessages => ToLuaMessages(_task.Messages.Skip(_task.ContinuedMessageCount));

		/// <summary>
		/// Waits until the task settles, or until the given number of seconds runs out.
		/// </summary>
		/// <param name="ctx">The calling context, whose token is the host's.</param>
		/// <param name="seconds">
		/// A number of seconds, or <see langword="null"/> to wait as long as it takes. A non-positive number also
		/// waits indefinitely, mirroring <c>dass.chat.wait_for_idle</c>.
		/// </param>
		/// <returns>
		/// The handle itself and whether the task stood still; <see langword="false"/> means the wait ran out of time.
		/// </returns>
		public async Task<LuaTuple> Wait(LuaCallingContext ctx, object? seconds = null)
		{
			var settled = await WaitCoreAsync(ctx, ReadSeconds(seconds));
			return new LuaTuple(_self, LuaBoolean.FromBoolean(settled));
		}

		/// <summary>
		/// Adds follow-up messages and returns the handle, so <c>await t:send(...)</c> waits for the continuation.
		/// </summary>
		/// <param name="ctx">The calling context, whose token the continuation inherits.</param>
		/// <param name="messages">A string (a user message), a message table, or an array of message tables.</param>
		/// <returns>The handle itself.</returns>
		public LuaTuple Send(LuaCallingContext ctx, params LuaValue[] messages)
		{
			var followUp = ReadMessages(messages);
			var wasCompleted = Completed;

			_task.Continue(followUp, ctx.CancellationToken);

			// Continue creates a fresh token for a relaunch and the launch timeout is not carried over by the
			// executor, so the handle re-arms it to keep the promise the launch made.
			if (wasCompleted && _timeout is { } timeout && timeout > TimeSpan.Zero)
			{
				try
				{
					_task.CancellationTokenSource.CancelAfter(timeout);
				}
				catch (ObjectDisposedException)
				{
				}
			}

			return new LuaTuple(_self);
		}

		/// <summary>
		/// Cancels the task and, by default, its whole sub-tree.
		/// </summary>
		/// <param name="ctx">The calling context.</param>
		/// <param name="cascade">Whether sub-tasks are cancelled too; <see langword="nil"/> keeps the default.</param>
		/// <returns>Whether this very task was still cancellable.</returns>
		public LuaTuple Cancel(LuaCallingContext ctx, object? cascade = null)
		{
			// nil keeps the default, and only a real `false` turns the cascade off.
			var cancelSubTasks = cascade is not false;
			return new LuaTuple(LuaBoolean.FromBoolean(CancelTask(_task, cancelSubTasks)));
		}

		/// <summary>
		/// Describes the handle for <c>tostring</c> and in error messages.
		/// </summary>
		public override string ToString() => $"agent_task {_task.Id} ({Status})";

		/// <summary>
		/// Wraps a launched agent task into an <c>agent_task</c> userdata.
		/// </summary>
		/// <param name="task">The task the handle controls.</param>
		/// <param name="timeout">The launch timeout, re-armed on every relaunch the handle triggers.</param>
		[LuaHidden]
		public static LuaUserData Wrap(AgentTask task, TimeSpan? timeout)
		{
			var handle = new LuaAgentTask(task, timeout);
			var userData = new LuaUserData(handle, "agent_task") { Metatable = _metatable };
			handle._self = userData;
			return userData;
		}

		/// <summary>
		/// Builds the metatable: the generated one, cloned so <c>__await</c> can be added without touching the cache.
		/// </summary>
		private static LuaTable CreateMetatable()
		{
			var metatable = UserDataMetatableGenerator.GetOrCreate(typeof(LuaAgentTask)).DeepClone();
			metatable.Set(LuaMetatableEvent.Await, new LuaCallbackFunction(AwaitAsync, "__await"));
			return metatable;
		}

		private static async Task<LuaTuple> AwaitAsync(LuaCallingContext ctx, LuaValue[] args)
		{
			if (args.Length == 0 || args[0] is not LuaUserData { Target: LuaAgentTask handle })
				throw new LuaRuntimeException("agent_task: cannot await a detached handle.");

			await handle.WaitCoreAsync(ctx, seconds: null);

			// Awaiting a handle hands the handle itself back, so `await t` and `t:wait()` read the same way.
			return new LuaTuple(args[0]);
		}

		/// <summary>
		/// Waits for the task to settle, swallowing its own failure and cancellation: the outcome belongs to the
		/// handle, while the host token keeps cancelling the script as usual.
		/// </summary>
		/// <param name="ctx">The calling context, whose token is the host's.</param>
		/// <param name="seconds">How long to wait, or <see langword="null"/> to wait indefinitely.</param>
		/// <returns>Whether the task is settled when the wait returns.</returns>
		private async Task<bool> WaitCoreAsync(LuaCallingContext ctx, double? seconds)
		{
			var completion = _task.Completion;

			try
			{
				if (seconds is { } budget)
				{
					using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ctx.CancellationToken);
					// CancelAfter takes an int-millisecond delay, so a huge budget would throw instead of waiting.
					deadline.CancelAfter(TimeSpan.FromMilliseconds(Math.Min(budget * 1000, int.MaxValue - 1)));
					await completion.WaitAsync(deadline.Token);
				}
				else
				{
					await completion.WaitAsync(ctx.CancellationToken);
				}
			}
			catch (Exception) when (!ctx.CancellationToken.IsCancellationRequested)
			{
				// A settled task - failed, cancelled or blocked - is an outcome to read, not an error to raise.
			}

			return completion.IsCompleted;
		}

		/// <summary>
		/// Reads the wait budget out of the raw Lua argument the generator hands over: a number, <c>false</c>, or
		/// nothing at all.
		/// </summary>
		private static double? ReadSeconds(object? value) => value switch
		{
			null => null,
			bool boolean => boolean
				? throw new LuaRuntimeException("agent_task:wait([seconds]): the timeout is a number of seconds or false, not true.")
				: null,
			// A non-positive budget means "wait as long as it takes", mirroring dass.chat.wait_for_idle.
			double number => number > 0 ? number : null,
			_ => throw new LuaRuntimeException("agent_task:wait([seconds]): the timeout must be a number of seconds.")
		};

		private static List<AgentChatMessage> ReadMessages(LuaValue[] values)
		{
			List<AgentChatMessage> messages = [];

			foreach (var value in values)
			{
				switch (value)
				{
					case LuaString text:
						messages.Add(new AgentUserMessage { Content = text.Value });
						break;

					case LuaTable table when table.Length > 0 && table.IsArrayTable():
						for (int i = 1; i <= table.Length; i++)
						{
							if (table.Get(i) is not LuaTable messageTable)
								throw new LuaRuntimeException("agent_task:send(...): every entry of a message array must be a table.");

							messages.Add(LuaAgentMessageConverter.FromLua(messageTable));
						}
						break;

					case LuaTable table:
						messages.Add(LuaAgentMessageConverter.FromLua(table));
						break;

					default:
						throw new LuaRuntimeException("agent_task:send(...): expected a string, a message table or an array of message tables.");
				}
			}

			if (messages.Count == 0)
				throw new LuaRuntimeException("agent_task:send(...): at least one message is expected.");

			if (messages[^1] is not AgentUserMessage)
				throw new LuaRuntimeException("agent_task:send(...): the last message must be a user message.");

			return messages;
		}

		/// <summary>
		/// Cancels the task and, when asked, its whole sub-tree.
		/// </summary>
		/// <returns>Whether this very task was still cancellable; a sub-task's outcome never changes it.</returns>
		private static bool CancelTask(AgentTask task, bool cascade)
		{
			var fired = false;

			// A settled task is past cancelling even though its token source was never fired, and a second cancel
			// against a running task must not report itself as the one that stopped it.
			if (!task.Completion.IsCompleted && !task.CancellationTokenSource.IsCancellationRequested)
			{
				try
				{
					task.CancellationTokenSource.Cancel();
					fired = true;
				}
				catch (ObjectDisposedException)
				{
				}
			}

			if (cascade)
				foreach (var subTask in task.SubTasks.ToArray())
					CancelTask(subTask, cascade: true);

			return fired;
		}

		private static LuaTable ToLuaMessages(IEnumerable<AgentChatMessage> messages)
		{
			// Marked as an array: an empty message list must encode as [], not as an empty object.
			var table = LuaExtensions.CreateArrayTable();
			foreach (var message in messages)
				table.Append(LuaAgentMessageConverter.ToLua(message));
			return table;
		}
	}
}
