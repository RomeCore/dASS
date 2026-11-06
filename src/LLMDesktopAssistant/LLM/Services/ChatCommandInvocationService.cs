using LLMDesktopAssistant.LLM.Domain;
using LLMDesktopAssistant.Localization;
using LLMDesktopAssistant.Scripting.Lua;
using LLMDesktopAssistant.SlashCommands;
using LLMDesktopAssistant.SlashCommands.Arguments;
using LLMDesktopAssistant.SlashCommands.Execution;
using LLMDesktopAssistant.SlashCommands.Resolution;

namespace LLMDesktopAssistant.LLM.Services
{
	/// <inheritdoc cref="IChatCommandInvocationService"/>
	[ChatService(typeof(IChatCommandInvocationService))]
	public class ChatCommandInvocationService(
		Chat chat,
		ISlashCommandResolver resolver,
		IChatExecutionTokenService tokens
	) : IChatCommandInvocationService
	{
		/// <summary>
		/// The schema used when a command declares none (<see cref="SlashCommandInfo.ArgumentSchema"/> is
		/// <see langword="null"/>): a command with no schema takes no arguments.
		/// </summary>
		private static readonly SlashCommandArgumentSchema EmptySchema = new();

		/// <inheritdoc/>
		public CommandResolution Resolve(string rawToken, string rawArguments)
		{
			var resolution = resolver.Resolve(rawToken);
			if (resolution.Command is null)
				return new CommandResolution(null, null, rawArguments,
					resolution.Error ?? Locale.GetKey("command.error.unknown"), -1);

			var command = resolution.Command;
			var schema = command.Executor.ArgumentSchema ?? EmptySchema;

			if (!SlashCommandArgumentParser.TryParse(schema, rawArguments, out var parsed))
				return new CommandResolution(command, null, parsed.RawArguments,
					parsed.Error ?? Locale.GetKey("command.error.parse_error"), parsed.ErrorPosition);

			var bound = SlashCommandArgumentBinder.Bind(schema, parsed);
			if (!bound.IsValid)
				return new CommandResolution(command, null, parsed.RawArguments, bound.Error, bound.ErrorPosition);

			return new CommandResolution(command, bound, parsed.RawArguments, null, -1);
		}

		/// <inheritdoc/>
		public async Task<CommandExecution> ExecuteAsync(CommandResolution resolution, string rawToken,
			ChatMessage message, bool generateIntent, CancellationToken ct = default)
		{
			var command = resolution.Command!;

			var context = new SlashCommandExecutionContext
			{
				Chat = chat,
				Message = message,
				Command = command,
				Token = command.CanonicalToken,
				RawToken = rawToken,
				RawText = message.Content,
				RawArguments = resolution.RawArguments,
				Arguments = resolution.Arguments!,
				GenerateIntent = generateIntent,
				Services = chat.Services
			};

			LuaExecutionContext.Set(command: context);

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
				SlashCommandExecutionStatus.Executed, null, result.EffectSummary, result.ModelFacingMode);
		}
	}
}
