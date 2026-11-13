using AsyncLua;
using AsyncLua.Values;
using LLMDesktopAssistant.LLM.Domain;
using LLMDesktopAssistant.LLM.MVVM.Additional;
using LLMDesktopAssistant.LLM.Services;
using LLMDesktopAssistant.Users;
using LLMDesktopAssistant.Utils;

namespace LLMDesktopAssistant.Scripting.Lua.API
{
	/// <summary>
	/// Lua API for controlling the current chat: <c>dass.chat.send/queue/stop/...</c>.
	/// Mirrors <see cref="IChatOperationService"/> together with <see cref="IChatExecutionStatusService"/>, so a script
	/// can do what the chat UI does.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The service is chat-scoped, so "the chat" is always the chat the script runs in. This is a separate part from
	/// the chat's read/attachment surface (<see cref="LuaApiChat"/>): both populate the same <c>dass.chat</c> table.
	/// </para>
	/// <para>
	/// Every turn-starting call takes <see cref="ChatExecutionLevel.Operation"/>, which cancels every narrower level -
	/// including the very turn a tool or a command script is running in. Those calls therefore refuse to run from
	/// inside a turn; only <c>queue</c> (no execution scope at all) and <c>stop</c> survive there.
	/// </para>
	/// </remarks>
	[LuaApi(chatScoped: true)]
	public class LuaApiChatOperation : LuaApiBase
	{
		public override string? Namespace => "dass.chat";

		public override string? Manuals => """
			--- dass.chat - controlling the current chat

			Every function here acts on the chat the script runs in (this API is chat-scoped) and
			mirrors what the chat UI itself calls.

			TURN CONTROL:

			--- async dass.chat.send(options) -> nil
			  Sends a user message as a turn and waits until the WHOLE turn is over: the message is
			  inserted, its slash command (if any) runs, the answer is generated, and whatever was
			  queued meanwhile is drained. Do not await it when the end of the turn is not needed.

			--- async dass.chat.queue(options) -> nil
			  Puts a message into the follow-up queue without starting a turn. This is the only
			  message function allowed from inside a running turn (a tool or a command): the queue
			  outlives the run it is filled during, and the running turn drains it at the barrier its
			  level names (the message's tool batch by default). options are the same as for send();
			  options.generate asks for an answer to this entry.

			--- async dass.chat.continue_generation() -> nil
			  Continues the conversation without new input: generates a response, then drains the queue.

			--- async dass.chat.stop() -> nil
			  Hard stop: cancels the running turn and leaves the queue untouched. Allowed anywhere,
			  including inside a turn - a script running in that turn dies together with it.

			--- async dass.chat.stop_and_flush() -> nil
			  Stops the running turn, waits until the history is quiet, then drains the queue and
			  generates.

			--- async dass.chat.flush([generate]) -> nil
			  Drains the pending follow-ups without stopping anything: inserts them in order, runs
			  their commands, and generates once when `generate` is true and some entry asked for an
			  answer. Default generate: false.

			--- async dass.chat.wait_for_idle([timeout_ms]) -> boolean
			  Waits until the chat is idle (nothing is executing). Returns true when it became idle,
			  false when timeout_ms elapsed first; without a timeout it waits indefinitely.

			  A generating chat is never idle, and a turn cannot end while the tool that holds it is
			  still running, so this is useless from inside a turn - the call throws there.

			EDITING HISTORY:

			--- async dass.chat.regenerate([target]) -> nil
			  Regenerates the message at `target`: a new branch, the answer comes first, the queue
			  after it. Regenerating a user message is an error.

			--- async dass.chat.resend([target]) -> nil
			  Resends from `target`: the answer comes first, the queue after it.

			--- async dass.chat.edit(target, options) -> nil
			  Replaces the message at `target` with a user message built from options (the same shape
			  as send()), truncates everything after it, then generates an answer. `target` is
			  required and may be any message, not only a user one.

			--- async dass.chat.switch_branch(target, branch) -> nil
			  Switches the visible branch of `target` to `branch` (1-based, the same number
			  chat_message.branch_index reports). Both arguments are required.

			--- async dass.chat.delete([target]) -> nil
			  Deletes the message at `target` together with its descendants.

			OPTIONS (send, queue, edit):

			  options = {
			    content = "text",                 -- the message text, optional
			    parts = { { title = "done", icon = "Check", color = "#88ff88" } },
			                                      -- chips attached to the message, optional
			    generate = true,                  -- generate an answer, default true
			    sender = "Master",                -- the login written into the message, optional
			    visibility = "always",            -- "always" (default), "reveal-after-send",
			                                      -- "only-users", "only-agents"
			    visible_to = { "user", "<agent-guid>" }, -- empty means everyone
			    visible_to_white_list = false,    -- whether visible_to is a whitelist
			    can_execute_command = true,       -- may the message run a slash command
			  }

			  options.content or options.parts is required: an empty message is an error.
			  `sender` is free-form: any login the chat knows, or a name like "System" or
			  "Master". It defaults to the first local user.

			TARGET:

			  `target` is a chat_message handle, a 1-based message number (the same number
			  chat_message.index reports), or nil for the last message. A handle is preferred:
			  numbers shift after an edit or a delete, messages do not.

			WHAT IS NOT ALLOWED:

			  Every function except queue() and stop() starts or rewrites a turn by taking the chat
			  operation level, and taking it cancels everything narrower - including the turn the
			  calling tool or command script is running in. So from a tool or a command those
			  functions throw: use queue() to add a message and stop() to stop.

			CANCELLATION:

			  The calls run under the token of the calling context, so a Stop from the UI aborts both
			  the turn and the script waiting for it. Wrap the call in task.without_cancellation(...)
			  to detach it from that token:

			    await task.without_cancellation(function()
			      dass.chat.send({ content = "continue" })
			    end)

			  A cancelled call is not a Lua error: the script simply stops.

			EXAMPLES:

			  -- Say something and wait for the answer
			  await dass.chat.send({ content = "Summarize the last answer", sender = "Master" })

			  -- A message with a chip that needs no answer
			  dass.chat.send({ content = "Noted", generate = false,
			    parts = { { title = "noted", icon = "Pencil", color = "#88ff88" } } })

			  -- A tool that asks the user something from inside the running turn
			  dass.chat.queue({ content = "I need the API key in .env, then answer again" })

			  -- Fire and forget: the script moves on, the turn runs on its own
			  dass.chat.send({ content = "Rerun the tests" })

			  -- A UI button that stops whatever is running
			  dass.chat.stop()

			  -- Branch navigation on the last message
			  dass.chat.switch_branch(nil, 2)
			""";

		private readonly Chat _chat;
		private readonly IChatOperationService _operations;
		private readonly IChatExecutionStatusService _status;
		private readonly IUserManagementService _users;

		public LuaApiChatOperation(Chat chat, IChatOperationService operations,
			IChatExecutionStatusService status, IUserManagementService users)
		{
			_chat = chat;
			_operations = operations;
			_status = status;
			_users = users;
		}

		public override Action? Populate(LuaTable globals, LuaTable ns, LuaService luaService)
		{
			ns["send"] = new LuaCallbackFunction(SendAsync);
			ns["queue"] = new LuaCallbackFunction(QueueAsync);
			ns["continue_generation"] = new LuaCallbackFunction(ContinueGenerationAsync);
			ns["stop"] = new LuaCallbackFunction(StopAsync);
			ns["stop_and_flush"] = new LuaCallbackFunction(StopAndFlushAsync);
			ns["flush"] = new LuaCallbackFunction(FlushAsync);
			ns["wait_for_idle"] = new LuaCallbackFunction(WaitForIdleAsync);
			ns["regenerate"] = new LuaCallbackFunction(RegenerateAsync);
			ns["resend"] = new LuaCallbackFunction(ResendAsync);
			ns["edit"] = new LuaCallbackFunction(EditAsync);
			ns["switch_branch"] = new LuaCallbackFunction(SwitchBranch);
			ns["delete"] = new LuaCallbackFunction(Delete);
			return null;
		}

		private async Task<LuaTuple> SendAsync(LuaCallingContext ctx, LuaValue[] args)
		{
			const string call = "dass.chat.send(options)";
			EnsureOutsideTurn(call);

			var options = Options(args, 0, call);
			var input = ReadInput(options, call);
			var generate = GetBoolean(options, "generate") ?? true;

			await RunAsync(() => _operations.SendUserInputAsync(input, generate, ctx.CancellationToken), call);
			return Nil();
		}

		private async Task<LuaTuple> QueueAsync(LuaCallingContext ctx, LuaValue[] args)
		{
			const string call = "dass.chat.queue(options)";

			var options = Options(args, 0, call);
			var input = ReadInput(options, call);
			var generate = GetBoolean(options, "generate") ?? true;

			await RunAsync(() => _operations.QueueUserInputAsync(input, generate, ctx.CancellationToken), call);
			return Nil();
		}

		private async Task<LuaTuple> ContinueGenerationAsync(LuaCallingContext ctx, LuaValue[] args)
		{
			const string call = "dass.chat.continue_generation()";
			EnsureOutsideTurn(call);

			await RunAsync(() => _operations.ContinueGenerationAsync(ctx.CancellationToken), call);
			return Nil();
		}

		private async Task<LuaTuple> StopAsync(LuaCallingContext ctx, LuaValue[] args)
		{
			const string call = "dass.chat.stop()";

			await RunAsync(() => _operations.StopGenerationAsync(ctx.CancellationToken), call);
			return Nil();
		}

		private async Task<LuaTuple> StopAndFlushAsync(LuaCallingContext ctx, LuaValue[] args)
		{
			const string call = "dass.chat.stop_and_flush()";
			EnsureOutsideTurn(call);

			await RunAsync(() => _operations.StopAndFlushAsync(ctx.CancellationToken), call);
			return Nil();
		}

		private async Task<LuaTuple> FlushAsync(LuaCallingContext ctx, LuaValue[] args)
		{
			const string call = "dass.chat.flush([generate])";
			EnsureOutsideTurn(call);

			var generate = args.Length > 0 && args[0] is LuaBoolean value && value.Value;

			await RunAsync(() => _operations.FlushFollowUpsAsync(generate, ctx.CancellationToken), call);
			return Nil();
		}

		private async Task<LuaTuple> WaitForIdleAsync(LuaCallingContext ctx, LuaValue[] args)
		{
			const string call = "dass.chat.wait_for_idle([timeout_ms])";
			EnsureOutsideTurn(call);

			int? timeout = args.Length > 0 && args[0] is LuaNumber number && number.Value > 0
				? (int)number.Value
				: null;

			try
			{
				if (timeout is null)
				{
					await _status.WaitUntilIdleAsync(ctx.CancellationToken);
					return new LuaTuple(LuaBoolean.True);
				}

				using var deadline = CancellationTokenSource.CreateLinkedTokenSource(ctx.CancellationToken);
				deadline.CancelAfter(timeout.Value);

				try
				{
					await _status.WaitUntilIdleAsync(deadline.Token);
					return new LuaTuple(LuaBoolean.True);
				}
				catch (OperationCanceledException) when (!ctx.CancellationToken.IsCancellationRequested)
				{
					return new LuaTuple(LuaBoolean.False);
				}
			}
			catch (ObjectDisposedException)
			{
				throw StaleContext(call);
			}
		}

		private async Task<LuaTuple> RegenerateAsync(LuaCallingContext ctx, LuaValue[] args)
		{
			const string call = "dass.chat.regenerate([target])";
			EnsureOutsideTurn(call);

			var messageIndex = ResolveMessageIndex(args, 0, call, required: false);
			await RunAsync(() => _operations.RegenerateMessageAsync(messageIndex, ctx.CancellationToken), call);
			return Nil();
		}

		private async Task<LuaTuple> ResendAsync(LuaCallingContext ctx, LuaValue[] args)
		{
			const string call = "dass.chat.resend([target])";
			EnsureOutsideTurn(call);

			var messageIndex = ResolveMessageIndex(args, 0, call, required: false);
			await RunAsync(() => _operations.ResendMessageAsync(messageIndex, ctx.CancellationToken), call);
			return Nil();
		}

		private async Task<LuaTuple> EditAsync(LuaCallingContext ctx, LuaValue[] args)
		{
			const string call = "dass.chat.edit(target, options)";
			EnsureOutsideTurn(call);

			var messageIndex = ResolveMessageIndex(args, 0, call, required: true);
			var options = Options(args, 1, call);
			var input = ReadInput(options, call);
			var generate = GetBoolean(options, "generate") ?? true;

			await RunAsync(() => _operations.SendEditedUserInputAsync(messageIndex, input, generate,
				ctx.CancellationToken), call);
			return Nil();
		}

		private LuaTuple SwitchBranch(LuaCallingContext ctx, LuaValue[] args)
		{
			const string call = "dass.chat.switch_branch(target, branch)";
			EnsureOutsideTurn(call);
			ctx.CancellationToken.ThrowIfCancellationRequested();

			var messageIndex = ResolveMessageIndex(args, 0, call, required: true);
			var branches = _chat.Messages[messageIndex].AvailableBranchesCount;

			if (args.Length < 2 || args[1] is not LuaNumber branch || branch.Value != Math.Truncate(branch.Value) ||
				branch.Value < 1 || branch.Value > branches)
			{
				throw new LuaRuntimeException(
					$"{call}: 'branch' must be a 1-based number (1..{branches}).");
			}

			_operations.SwitchBranch(messageIndex, (int)branch.Value - 1);
			return Nil();
		}

		private LuaTuple Delete(LuaCallingContext ctx, LuaValue[] args)
		{
			const string call = "dass.chat.delete([target])";
			EnsureOutsideTurn(call);
			ctx.CancellationToken.ThrowIfCancellationRequested();

			_operations.DeleteMessageWithDescendants(ResolveMessageIndex(args, 0, call, required: false));
			return Nil();
		}

		/// <summary>
		/// Refuses a call that would take the operation level from inside a turn: taking it cancels every narrower
		/// level, so the turn the script belongs to would die mid-flight and its tool result would never land.
		/// </summary>
		private static void EnsureOutsideTurn(string call)
		{
			if (LuaExecutionContext.Tool is null && LuaExecutionContext.Command is null)
				return;

			throw new LuaRuntimeException(
				$"{call}: called from inside the running turn, which this call would cancel. " +
				"Use dass.chat.queue() to add a message or dass.chat.stop() to stop.");
		}

		/// <summary>
		/// Runs the operation and translates the one exception a stale Lua context produces into what the author can
		/// act on. A context captured inside a finished turn carries a token of a disposed source, which cannot be
		/// linked to a new one.
		/// </summary>
		private static async Task RunAsync(Func<Task> operation, string call)
		{
			try
			{
				await operation();
			}
			catch (ObjectDisposedException)
			{
				throw StaleContext(call);
			}
		}

		private static LuaRuntimeException StaleContext(string call)
			=> new($"{call}: the calling context belongs to a turn that is already over; " +
				"run the call through task.without_cancellation(...).");

		private static LuaTable Options(LuaValue[] args, int index, string call)
		{
			if (args.Length <= index || args[index] is not LuaTable table)
				throw new LuaRuntimeException($"{call}: options table expected.");

			return table;
		}

		/// <summary>
		/// Resolves a message target: a <c>chat_message</c> handle, a 1-based index, or <see langword="null"/> for the
		/// last message. The handle wins over the number, because numbers shift after an edit or a delete.
		/// </summary>
		private int ResolveMessageIndex(LuaValue[] args, int index, string call, bool required)
		{
			if (args.Length <= index || args[index] is LuaNil)
			{
				if (required)
					throw new LuaRuntimeException(
						$"{call}: 'target' is required: a chat_message handle or a 1-based number.");

				if (_chat.Messages.Count == 0)
					throw new LuaRuntimeException($"{call}: the chat has no messages.");

				return _chat.Messages.Count - 1;
			}

			switch (args[index])
			{
				case LuaUserData { Target: LuaChatMessage handle }:
				{
					var messageIndex = _chat.Messages.IndexOf(handle.Message);
					if (messageIndex < 0)
						throw new LuaRuntimeException($"{call}: the message handle is no longer in the chat.");

					return messageIndex;
				}
				case LuaNumber number:
				{
					var messageIndex = (int)number.Value - 1;
					if (number.Value != Math.Truncate(number.Value) || messageIndex < 0 ||
						messageIndex >= _chat.Messages.Count)
					{
						throw new LuaRuntimeException(
							$"{call}: the message index {number.Value} is out of range (1..{_chat.Messages.Count}).");
					}

					return messageIndex;
				}
				default:
					throw new LuaRuntimeException(
						$"{call}: 'target' must be a chat_message handle, a 1-based number, or nil.");
			}
		}

		/// <summary>
		/// Builds the user input the operation service inserts, from the Lua options table.
		/// </summary>
		private UserInputState ReadInput(LuaTable options, string call)
		{
			var state = new UserInputState
			{
				Content = GetString(options, "content") ?? string.Empty,
				// The default sender is someone who may actually speak: an unavailable user is only kept for history.
				SenderLogin = GetString(options, "sender") ??
					_users.GetAvailableLocalUsers().FirstOrDefault()?.Login ??
					_users.GetLocalUsers().FirstOrDefault()?.Login ?? "user",
				Visibility = ReadVisibility(options, call),
				VisibleTo = [.. ReadStrings(options, "visible_to", call)],
				IsVisibleToWhiteList = GetBoolean(options, "visible_to_white_list") ?? false,
				CanExecuteCommand = GetBoolean(options, "can_execute_command") ?? true
			};

			state.Parts = [.. ReadParts(options, call)];

			// The service would happily insert a message with no text and no parts; a script sending nothing is
			// always a mistake, and an empty user message is hard to spot in the history.
			if (state.IsEmpty)
				throw new LuaRuntimeException($"{call}: options.content or options.parts is required.");

			return state;
		}

		private static MessageVisibility ReadVisibility(LuaTable options, string call)
		{
			if (GetString(options, "visibility") is not { Length: > 0 } name)
				return MessageVisibility.Always;

			foreach (var (value, kebab) in KebabEnumNames<MessageVisibility>.Names)
			{
				if (string.Equals(kebab, name, StringComparison.OrdinalIgnoreCase))
					return value;
			}

			var known = string.Join(", ", KebabEnumNames<MessageVisibility>.Names.Values.Order());
			throw new LuaRuntimeException($"{call}: options.visibility '{name}' is unknown. Expected one of: {known}.");
		}

		private static List<AdditionalMessagePart> ReadParts(LuaTable options, string call)
		{
			List<AdditionalMessagePart> parts = [];
			if (options.Get("parts") is not LuaTable table)
				return parts;

			for (int index = 1; index <= table.Length; index++)
			{
				if (table.Get(index) is not LuaTable part)
					throw new LuaRuntimeException($"{call}: options.parts must be an array of tables.");

				parts.Add(LuaAttachmentParts.CreatePart(
					GetString(part, "title"), GetString(part, "icon"), GetString(part, "color")));
			}

			return parts;
		}

		private static IEnumerable<string> ReadStrings(LuaTable options, string key, string call)
		{
			if (options.Get(key) is not LuaTable table)
				yield break;

			for (int index = 1; index <= table.Length; index++)
			{
				if (table.Get(index) is not LuaString value)
					throw new LuaRuntimeException($"{call}: options.{key} must be an array of strings.");

				yield return value.Value;
			}
		}

		private static string? GetString(LuaTable table, string key)
			=> table.Get(key) is LuaString value && value.Value.Length > 0 ? value.Value : null;

		private static bool? GetBoolean(LuaTable table, string key)
			=> table.Get(key) is LuaBoolean value ? value.Value : null;

		private static LuaTuple Nil() => new(LuaNil.Instance);
	}
}
