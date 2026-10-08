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
				if (resolution.Invocation is not { } invocation)
				{
					// Guard: inserted without a passing pre-flight (or a race). Attach the error, skip the command and
					// the generation — but never drop the input.
					message.Error = resolution.Error ?? Locale.GetKey("command.error.unknown");
					generate = false;
				}
				else
				{
					generate = await ExecuteCommandAsync(invocation, message, generateIntent, ct);
				}
			}

			if (generate)
				await executor.GenerateResponseAsync(ct);
		}

		private bool CommandsEnabled => chatSettings.Settings.Commands.EnableCommands;

		private async Task<bool> ExecuteCommandAsync(CommandInvocation invocation, ChatMessage message,
			bool generateIntent, CancellationToken ct)
		{
			var command = invocation.Command;

			var context = new SlashCommandExecutionContext
			{
				Chat = chat,
				Message = message,
				Command = command,
				Token = command.CanonicalToken,
				RawToken = invocation.RawToken,
				RawText = message.Content,
				RawArguments = invocation.RawArguments,
				Arguments = invocation.Arguments,
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
				return false;
			}
			catch (Exception ex)
			{
				// The message is already in history, so a runtime failure is attached rather than thrown.
				message.Error = Locale.GetConstKey(ex.Message);
				return false;
			}

			if (result.Error is not null)
				message.Error = result.Error;

			return SlashCommandIntent.Resolve(generateIntent, command.Generate, result.Generate);
		}

		/// <summary>
		/// Resolves the token, parses and binds its arguments. Anything that fails yields a
		/// <see cref="CommandResolution"/> whose <see cref="CommandResolution.Error"/> blocks the send.
		/// </summary>
		private CommandResolution Resolve(string rawToken, string rawArguments)
		{
			var resolution = resolver.Resolve(rawToken);
			if (resolution.Command is null)
				return new CommandResolution(null, resolution.Error ?? Locale.GetKey("command.error.unknown"), -1);

			var schema = resolution.Command.ArgumentSchema ?? EmptySchema;

			if (!SlashCommandArgumentParser.TryParse(schema, rawArguments, out var parsed))
				return new CommandResolution(null, parsed.Error ?? Locale.GetKey("command.error.parse_error"),
					parsed.ErrorPosition);

			var bound = SlashCommandArgumentBinder.Bind(schema, parsed);
			if (!bound.IsValid)
				return new CommandResolution(null, bound.Error, bound.ErrorPosition);

			return new CommandResolution(
				new CommandInvocation(resolution.Command, rawToken, parsed.RawArguments, bound), null, -1);
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

		/// <summary>A resolved and bound command invocation.</summary>
		private readonly record struct CommandInvocation(
			SlashCommandInfo Command, string RawToken, string RawArguments, SlashCommandBoundArguments Arguments);

		/// <summary>The outcome of resolving a command: either an invocation or the reason the send is blocked.</summary>
		private readonly record struct CommandResolution(
			CommandInvocation? Invocation, LocaleKeyBase? Error, int ErrorPosition);
	}
}
