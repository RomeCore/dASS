using LLMDesktopAssistant.Hooks;
using LLMDesktopAssistant.Hooks.Chat;
using LLMDesktopAssistant.LLM.Domain;
using LLMDesktopAssistant.Localization;
using LLMDesktopAssistant.Scripting.Lua;
using LLMDesktopAssistant.SlashCommands;
using LLMDesktopAssistant.SlashCommands.Execution;
using LLMDesktopAssistant.Users;
using Serilog;

namespace LLMDesktopAssistant.LLM.Services
{
	/// <inheritdoc cref="IChatMessageInsertionService"/>
	[ChatService(typeof(IChatMessageInsertionService))]
	public class ChatMessageInsertionService(
		Chat chat,
		IChatStorageService storage,
		IChatSettingsService chatSettings,
		IChatCommandInvocationService commands,
		IUserManagementService users,
		IHookDispatcher hookDispatcher
	) : IChatMessageInsertionService
	{
		/// <inheritdoc/>
		public async Task<UserInputInsertionCheckResult> CanInsertUserInputAsync(UserInputState input,
			UserSubmitKind kind, bool generate, CancellationToken ct = default)
		{
			UserMessageHookPayload.UserMessageCommandInfo? command = null;

			// With commands off, or with the permission denied, the whole command path is disabled.
			if (CommandsEnabled && input.CanExecuteCommand &&
				SlashCommandExtractor.TryExtractToken(input.Content, out var rawToken, out var rawArguments))
			{
				var resolution = commands.Resolve(rawToken, rawArguments);
				if (resolution.Error is not null)
					return UserInputInsertionCheckResult.Blocked(resolution.Error, resolution.ErrorPosition);

				command = new UserMessageHookPayload.UserMessageCommandInfo
				{
					Token = rawToken,
					Arguments = rawArguments,
					Valid = true
				};
			}

			var sender = users.FindByLogin(input.SenderLogin)?.Name;
			var payload = UserMessageHookPayload.Create(chat.Id, input.SenderLogin, sender, input.Content,
				input.Parts, input.Visibility, input.VisibleTo, input.IsVisibleToWhiteList,
				isEdit: kind is UserSubmitKind.Edit, generate: generate, command: command);

			var result = await hookDispatcher.ExecuteHooksAsync<ChatHookVerdict>(payload,
				ChatHookEvents.ComposeUserSubmit(kind), ChatHookEvents.ComposeUserTarget(input.SenderLogin, sender),
				HookExecutionFlow.Sequential, cancellationToken: ct);

			// A hook that broke the chain itself - a blocking exit code - beats any verdict: there is none.
			if (!string.IsNullOrEmpty(result.BlockingMessage))
				return UserInputInsertionCheckResult.Blocked(Locale.GetConstKey(result.BlockingMessage));

			if (result.Value is not { } verdict)
				return UserInputInsertionCheckResult.Ok;

			var reason = string.IsNullOrWhiteSpace(verdict.Reason) ? null : verdict.Reason;

			return verdict.Decision switch
			{
				ChatHookDecision.Disallow => UserInputInsertionCheckResult.Blocked(
					reason is null ? Locale.GetKey("chat.hook.submit.disallowed") : Locale.GetConstKey(reason)),
				ChatHookDecision.Error => UserInputInsertionCheckResult.Blocked(
					reason is null ? Locale.GetKey("chat.hook.submit.error") : Locale.GetConstKey(reason)),
				_ => UserInputInsertionCheckResult.Ok
			};
		}

		/// <inheritdoc/>
		public async Task<UserInputInsertion?> AppendUserInputAsync(UserInputState input, int? editIndex = null,
			CancellationToken ct = default)
		{
			// The permission is read here and only here, so the direct path and the queued path cannot diverge.
			string rawToken = string.Empty;
			string rawArguments = string.Empty;
			var commandHandlingActive = input.CanExecuteCommand && CommandsEnabled;
			var isCommand = commandHandlingActive &&
				SlashCommandExtractor.TryExtractToken(input.Content, out rawToken, out rawArguments);

			// A "//..." escape is undone while command handling is active, even when the token after the escape does
			// not resolve to a command; forbidden or off means verbatim.
			var content = commandHandlingActive
				? SlashCommandExtractor.UnescapeLeadingSlash(input.Content)
				: input.Content;

			var message = CreateUserMessage(input, content);

			if (!await RunInsertHookAsync(input, message, editIndex, UserInsertPhase.Pre, ct))
			{
				Log.Warning("A hook refused the insertion of a message from {Sender}.", input.SenderLogin);
				return null;
			}

			if (editIndex is not null)
				storage.EditMessage(editIndex.Value, message);
			else
				storage.AppendMessage(message);

			// EditMessage creates a new branch and inserts the message to the end, so we can just get the final message.
			var branched = chat.Messages[^1];
			FireInsertedHook(message, branched, editIndex);

			if (!isCommand)
				return new UserInputInsertion(message, null);

			var resolution = commands.Resolve(rawToken, rawArguments);
			return new UserInputInsertion(message,
				new PendingCommand(rawToken, rawArguments, resolution));
		}

		/// <inheritdoc/>
		public async Task<UserInputInsertionResult> RunInsertedCommandAsync(UserInputInsertion insertion,
			bool generateIntent, CancellationToken ct = default)
		{
			if (insertion.Command is not { } pending)
				return new UserInputInsertionResult(generateIntent);

			var message = insertion.Message;
			var resolution = pending.Resolution;

			if (resolution.Error is not null || resolution.Command is null)
			{
				// Guard: inserted without a passing pre-flight (or a race). Attach the error and the failed trace,
				// skip the command - but never drop the message.
				var error = resolution.Error ?? Locale.GetKey("command.error.unknown");
				message.Error = error;
				message.AdditionalData.Add(SlashCommandFingerprint.Create(pending.RawToken, resolution.Command,
					arguments: null, generateIntent, generateOutcome: false, SlashCommandExecutionStatus.Failed, error));
				return new UserInputInsertionResult(false);
			}

			var execution = await commands.ExecuteAsync(resolution, pending.RawToken, message, generateIntent, ct);

			if (execution.Error is not null)
				message.Error = execution.Error;

			message.AdditionalData.Add(SlashCommandFingerprint.Create(pending.RawToken, resolution.Command,
				resolution.Arguments, generateIntent, execution.Generate, execution.Status, execution.Error,
				execution.EffectSummary, execution.ModelFacingMode));

			return new UserInputInsertionResult(execution.Generate);
		}

		private bool CommandsEnabled => chatSettings.Settings.Commands.AddonsCategoryEnabled;

		private async Task<bool> RunInsertHookAsync(UserInputState input, UserMessage message, int? editIndex,
			UserInsertPhase phase, CancellationToken ct)
		{
			var sender = users.FindByLogin(message.SenderLogin)?.Name;
			var payload = UserMessageHookPayload.Create(chat.Id, message.SenderLogin, sender, message.Content,
				message.AdditionalData, message.Visibility, message.VisibleTo, message.IsVisibleToWhiteList,
				isEdit: editIndex is not null, generate: null, command: null,
				messageId: null, index: editIndex + 1);

			var result = await hookDispatcher.ExecuteHooksAsync<ChatHookVerdict>(payload,
				ChatHookEvents.ComposeUserInsert(phase),
				ChatHookEvents.ComposeUserTarget(message.SenderLogin, sender),
				HookExecutionFlow.Sequential, cancellationToken: ct);

			if (!string.IsNullOrEmpty(result.BlockingMessage))
			{
				Log.Warning("The insertion of a message from {Sender} was blocked with: {Reason}",
					input.SenderLogin, result.BlockingMessage);
				return false;
			}

			if (result.Value is not { } verdict)
				return true;

			if (verdict.Decision is not (ChatHookDecision.Disallow or ChatHookDecision.Error))
				return true;

			Log.Warning("The insertion of a message from {Sender} was refused by a hook: {Reason}",
				input.SenderLogin, verdict.Reason ?? "no reason given");
			return false;
		}

		private void FireInsertedHook(UserMessage message, BranchedMessage branched,
			int? editIndex)
		{
			LuaExecutionContext.SetMessage(branched);
			var sender = users.FindByLogin(message.SenderLogin)?.Name;
			var payload = UserMessageHookPayload.Create(chat.Id, message.SenderLogin, sender, message.Content,
				message.AdditionalData, message.Visibility, message.VisibleTo, message.IsVisibleToWhiteList,
				isEdit: editIndex is not null, generate: null, command: null,
				messageId: branched.MessageId, index: branched.MessageIndex + 1);

			hookDispatcher.ExecuteHooksFnF(payload, ChatHookEvents.ComposeUserInsert(UserInsertPhase.Post),
				ChatHookEvents.ComposeUserTarget(message.SenderLogin, sender));
		}

		private UserMessage CreateUserMessage(UserInputState userInput, string content)
		{
			var message = new UserMessage
			{
				CreatedAt = DateTime.Now,
				Sender = users.FindByLogin(userInput.SenderLogin)?.Name ?? userInput.SenderLogin,
				SenderLogin = userInput.SenderLogin,
				Content = content,
				Visibility = userInput.Visibility,
				VisibleTo = [..userInput.VisibleTo],
				IsVisibleToWhiteList = userInput.IsVisibleToWhiteList
			};
			message.AdditionalData.Reset(userInput.Parts);
			return message;
		}
	}
}
