using LLMDesktopAssistant.LLM.Domain;
using LLMDesktopAssistant.Localization;
using LLMDesktopAssistant.SlashCommands;
using LLMDesktopAssistant.SlashCommands.Arguments;
using LLMDesktopAssistant.SlashCommands.Execution;
using LLMDesktopAssistant.SlashCommands.Resolution;

namespace LLMDesktopAssistant.LLM.Services
{
	/// <inheritdoc cref="IChatMessageInsertionService"/>
	[ChatService(typeof(IChatMessageInsertionService))]
	public class ChatMessageInsertionService(
		Chat chat,
		IChatStorageService storage,
		IChatSettingsService chatSettings,
		ISlashCommandResolver resolver,
		IChatExecutionTokenService tokens,
		IChatExecutionService executor
	) : IChatMessageInsertionService
	{
		/// <summary>
		/// The schema used when a command declares none (<see cref="SlashCommandInfo.ArgumentSchema"/> is
		/// <see langword="null"/>): a command with no schema takes no arguments, so any argument blocks the send.
		/// </summary>
		private static readonly SlashCommandArgumentSchema EmptySchema = new();

		/// <inheritdoc/>
		public UserInputInsertionCheckResult CanInsertUserInput(UserInput input, bool generateIntent, int? editIndex = null)
		{
			// Edits never run a command, and with commands off the whole command path is disabled.
			if (editIndex is not null || !CommandsEnabled)
				return UserInputInsertionCheckResult.Ok;

			if (!SlashCommandExtractor.TryExtractToken(input.Content, out var rawToken, out var rawArguments))
				return UserInputInsertionCheckResult.Ok;

			var resolution = Resolve(rawToken, rawArguments);
			return resolution.Error is null
				? UserInputInsertionCheckResult.Ok
				: UserInputInsertionCheckResult.Blocked(resolution.Error, resolution.ErrorPosition);
		}

		/// <inheritdoc/>
		public async Task InsertUserInputAsync(UserInput input, bool generateIntent, int? editIndex = null,
			CancellationToken ct = default)
		{
			if (editIndex is not null)
			{
				storage.EditMessage(editIndex.Value, CreateUserMessage(input, input.Content));
				if (generateIntent)
					await executor.GenerateResponseAsync(ct);
				return;
			}

			var commandsEnabled = CommandsEnabled;
			string rawToken = string.Empty;
			string rawArguments = string.Empty;
			var isCommand = commandsEnabled &&
				SlashCommandExtractor.TryExtractToken(input.Content, out rawToken, out rawArguments);

			// A "//…" escape is undone only while command handling is active; with commands off the text is literal.
			var content = commandsEnabled ? SlashCommandExtractor.UnescapeLeadingSlash(input.Content) : input.Content;

			var resolution = isCommand ? Resolve(rawToken, rawArguments) : default;

			var message = CreateUserMessage(input, content);
			storage.AppendMessage(message);

			var generate = generateIntent;

			if (isCommand)
			{
				if (resolution.Error is not null || resolution.Command is null)
				{
					// Guard: inserted without a passing pre-flight (or a race). Attach the error and the failed trace,
					// skip the command and the generation — but never drop the input.
					var error = resolution.Error ?? Locale.GetKey("command.error.unknown");
					message.Error = error;
					message.AdditionalData.Add(SlashCommandFingerprint.Create(rawToken, resolution.Command,
						arguments: null, generateIntent, generateOutcome: false, SlashCommandExecutionStatus.Failed, error));
					generate = false;
				}
				else
				{
					var execution = await ExecuteCommandAsync(resolution.Command, resolution.Arguments!,
						resolution.RawArguments, rawToken, message, generateIntent, ct);

					if (execution.Error is not null)
						message.Error = execution.Error;

					message.AdditionalData.Add(SlashCommandFingerprint.Create(rawToken, resolution.Command,
						resolution.Arguments, generateIntent, execution.Generate, execution.Status, execution.Error,
						execution.EffectSummary));

					generate = execution.Generate;
				}
			}

			if (generate)
				await executor.GenerateResponseAsync(ct);
		}

		private bool CommandsEnabled => chatSettings.Settings.Commands.EnableCommands;

		private async Task<CommandExecution> ExecuteCommandAsync(SlashCommandInfo command,
			SlashCommandBoundArguments arguments, string rawArguments, string rawToken, ChatMessage message,
			bool generateIntent, CancellationToken ct)
		{
			var context = new SlashCommandExecutionContext
			{
				Chat = chat,
				Message = message,
				Command = command,
				Token = command.CanonicalToken,
				RawToken = rawToken,
				RawText = message.Content,
				RawArguments = rawArguments,
				Arguments = arguments,
				GenerateIntent = generateIntent,
				Services = chat.Services
			};

			SlashCommandExecutionResult result;
			try
			{
				using var scope = tokens.WithToken(ChatExecutionLevel.Command, ct, out var commandToken);
				result = await command.Executor.ExecuteAsync(context, commandToken);
			}
			catch (OperationCanceledException)
			{
				// A cancelled command is not a failure: no message error, no generation.
				return new CommandExecution(false, SlashCommandExecutionStatus.Cancelled, null, null);
			}
			catch (Exception ex)
			{
				// The message is already in history, so a runtime failure is attached rather than thrown.
				return new CommandExecution(false, SlashCommandExecutionStatus.Failed,
					Locale.GetConstKey(ex.Message), null);
			}

			if (result.Error is not null)
				return new CommandExecution(false, SlashCommandExecutionStatus.Failed, result.Error,
					result.EffectSummary);

			return new CommandExecution(
				SlashCommandIntent.Resolve(generateIntent, command.Generate, result.Generate),
				SlashCommandExecutionStatus.Executed, null, result.EffectSummary);
		}

		/// <summary>
		/// Resolves the token, parses and binds its arguments. The resolved command and the bound arguments are carried
		/// even when binding fails, so the fingerprint can describe the attempt.
		/// </summary>
		private CommandResolution Resolve(string rawToken, string rawArguments)
		{
			var resolution = resolver.Resolve(rawToken);
			if (resolution.Command is null)
				return new CommandResolution(null, null, rawArguments,
					resolution.Error ?? Locale.GetKey("command.error.unknown"), -1);

			var command = resolution.Command;
			var schema = command.ArgumentSchema ?? EmptySchema;

			if (!SlashCommandArgumentParser.TryParse(schema, rawArguments, out var parsed))
				return new CommandResolution(command, null, parsed.RawArguments,
					parsed.Error ?? Locale.GetKey("command.error.parse_error"), parsed.ErrorPosition);

			var bound = SlashCommandArgumentBinder.Bind(schema, parsed);
			if (!bound.IsValid)
				return new CommandResolution(command, null, parsed.RawArguments, bound.Error, bound.ErrorPosition);

			return new CommandResolution(command, bound, parsed.RawArguments, null, -1);
		}

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

		/// <summary>The outcome of resolving a command: the resolved command and bound arguments when it succeeded, or
		/// the reason the send is blocked.</summary>
		private readonly record struct CommandResolution(
			SlashCommandInfo? Command, SlashCommandBoundArguments? Arguments, string RawArguments,
			LocaleKeyBase? Error, int ErrorPosition);

		/// <summary>The outcome of running a command: the final generation decision and what to record.</summary>
		private readonly record struct CommandExecution(
			bool Generate, SlashCommandExecutionStatus Status, LocaleKeyBase? Error, string? EffectSummary);
	}
}
