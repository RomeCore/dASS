using LLMDesktopAssistant.LLM.Domain;
using LLMDesktopAssistant.Localization;
using LLMDesktopAssistant.SlashCommands;
using LLMDesktopAssistant.SlashCommands.Execution;

namespace LLMDesktopAssistant.LLM.Services
{
	/// <inheritdoc cref="IChatMessageInsertionService"/>
	[ChatService(typeof(IChatMessageInsertionService))]
	public class ChatMessageInsertionService(
		IChatStorageService storage,
		IChatSettingsService chatSettings,
		IChatCommandInvocationService commands
	) : IChatMessageInsertionService
	{
		/// <inheritdoc/>
		public UserInputInsertionCheckResult CanInsertUserInput(UserInput input)
		{
			// With commands off, or with the permission denied, the whole command path is disabled.
			if (!CommandsEnabled || !input.CanExecuteCommand)
				return UserInputInsertionCheckResult.Ok;

			if (!SlashCommandExtractor.TryExtractToken(input.Content, out var rawToken, out var rawArguments))
				return UserInputInsertionCheckResult.Ok;

			var resolution = commands.Resolve(rawToken, rawArguments);
			return resolution.Error is null
				? UserInputInsertionCheckResult.Ok
				: UserInputInsertionCheckResult.Blocked(resolution.Error, resolution.ErrorPosition);
		}

		/// <inheritdoc/>
		public Task<UserInputInsertion> AppendUserInputAsync(UserInput input, int? editIndex = null,
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
			if (editIndex is not null)
				storage.EditMessage(editIndex.Value, message);
			else
				storage.AppendMessage(message);

			if (!isCommand)
				return Task.FromResult(new UserInputInsertion(message, null));

			var resolution = commands.Resolve(rawToken, rawArguments);
			return Task.FromResult(new UserInputInsertion(message,
				new PendingCommand(rawToken, rawArguments, resolution)));
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

		private bool CommandsEnabled => chatSettings.Settings.Commands.EnableCommands;

		private static UserMessage CreateUserMessage(UserInput userInput, string content)
		{
			var message = new UserMessage
			{
				CreatedAt = DateTime.Now,
				Content = content,
				SenderLogin = userInput.SenderLogin,
				Visibility = userInput.Visibility,
				VisibleTo = userInput.VisibleTo,
				IsVisibleToWhiteList = userInput.IsVisibleToWhiteList
			};
			message.AdditionalData.Reset(userInput.Parts);
			return message;
		}
	}
}
