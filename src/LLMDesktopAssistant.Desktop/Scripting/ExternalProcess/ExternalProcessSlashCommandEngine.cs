using System.Collections.Immutable;
using System.Text.Json.Nodes;
using LLMDesktopAssistant.Controls.Icons;
using LLMDesktopAssistant.LLM.MVVM.Additional;
using LLMDesktopAssistant.Localization;
using LLMDesktopAssistant.Scripting;
using LLMDesktopAssistant.Services;
using LLMDesktopAssistant.SlashCommands;
using LLMDesktopAssistant.SlashCommands.Arguments;
using LLMDesktopAssistant.SlashCommands.Execution;
using LLMDesktopAssistant.SlashCommands.Scripting;
using LLMDesktopAssistant.StructuredValues;
using Serilog;

namespace LLMDesktopAssistant.Desktop.Scripting.ExternalProcess
{
	/// <summary>
	/// The out-of-process slash-command kind: it names the <c>command_args</c> variable, passes the addon's flags to
	/// the language engine and maps the JSON result onto the command's message.
	/// </summary>
	/// <remarks>
	/// There is no chat API for an external process: everything a command does to its message it declares in the
	/// result - an effect summary, a generation decision, an error and one content part.
	/// </remarks>
	[Service(typeof(IScriptableSlashCommandEngine))]
	public class ExternalProcessSlashCommandEngine(ExternalProcessEngineRegistry registry) : IScriptableSlashCommandEngine
	{
		/// <inheritdoc/>
		public IReadOnlyList<IScriptEngineDescriptor> Descriptors => registry.Descriptors;

		/// <inheritdoc/>
		public ISlashCommandExecutor CreateExecutor(SlashCommandInfo command, SlashCommandArgumentSchema? argumentSchema)
			=> new ExternalProcessCommandExecutor(command, argumentSchema, registry);
	}

	/// <summary>
	/// Runs a command body as an external process and maps its result onto the message the command produced.
	/// </summary>
	internal sealed class ExternalProcessCommandExecutor(SlashCommandInfo command,
		SlashCommandArgumentSchema? argumentSchema, ExternalProcessEngineRegistry registry) : ISlashCommandExecutor
	{
		private static readonly TimeSpan _timeout = TimeSpan.FromMinutes(10);

		/// <inheritdoc/>
		public SlashCommandArgumentSchema? ArgumentSchema { get; } = argumentSchema;

		/// <inheritdoc/>
		public async Task<SlashCommandExecutionResult> ExecuteAsync(SlashCommandExecutionContext ctx, CancellationToken ct)
		{
			var engine = registry.Resolve(command.Path ?? throw new InvalidOperationException(
				$"The path of the command '{command.Name}' is not set.")).Engine;

			var variables = ImmutableDictionary<string, INodeValue?>.Empty
				.Add("command_args", CommandArgsNode.Create(ctx.Arguments, ctx.RawArguments));

			if (command.RunTerminal)
			{
				var terminal = await engine.RunInTerminalAsync(new ExternalProcessScriptTerminalParameters
				{
					Kind = ScriptableAddonKind.SlashCommand,
					Body = command.Body,
					Variables = variables,
					InputMode = command.InputMode,
					ProcessName = command.Name,
					Wait = command.TerminalWait
				}, ctx.Message, ctx.Services, ct);

				AddPart(ctx, terminal.Output, null, null);

				return terminal.Status is ExternalProcessStatus.Succeeded
					? SlashCommandExecutionResult.Ok()
					: new SlashCommandExecutionResult(false, Locale.GetConstKey(terminal.Error ?? "The command failed."));
			}

			var result = await engine.RunAsync(new ExternalProcessScriptParameters
			{
				Kind = ScriptableAddonKind.SlashCommand,
				Body = command.Body,
				Variables = variables,
				InputMode = command.InputMode,
				OutputMode = command.OutputMode,
				OutputPattern = command.OutputPattern,
				ProcessName = command.Name,
				TimeOut = _timeout
			}, ctx.Services, ct);

			if (result.Status is not ExternalProcessStatus.Succeeded)
				return new SlashCommandExecutionResult(false, Locale.GetConstKey(result.Error ?? "The command failed."));

			return Apply(ctx, result);
		}

		/// <summary>
		/// Applies the command's result contract: <c>effect</c>, <c>generate</c>, <c>error</c> and <c>part</c>.
		/// </summary>
		private SlashCommandExecutionResult Apply(SlashCommandExecutionContext ctx, ExternalProcessScriptResult result)
		{
			var json = result.Json as JsonObject;

			// Without JSON the raw output is the injected part: a command with no structure is a text producer.
			if (json is null)
			{
				AddPart(ctx, result.Output, null, null);
				return SlashCommandExecutionResult.Ok();
			}

			if (json["part"] is JsonObject part)
			{
				var content = Text(part, "content");

				// Once the script speaks JSON, its part must be explicit: stdout is raw output, not a substitute for a
				// declared part.
				if (string.IsNullOrEmpty(content))
					return new SlashCommandExecutionResult(false,
						Locale.GetConstKey("The command's 'part' has no 'content'."));

				AddPart(ctx, content, Text(part, "title"), Text(part, "icon"));
			}

			// The declarative ceiling stays with the host: this value can only lower the caller's intent.
			var generate = json["generate"] is JsonValue value && value.TryGetValue<bool>(out var requested)
				? requested
				: true;
			var effect = Text(json, "effect");

			var error = Text(json, "error");
			return error is null
				? new SlashCommandExecutionResult(generate, null, effect)
				: new SlashCommandExecutionResult(generate, Locale.GetConstKey(error), effect);
		}

		private void AddPart(SlashCommandExecutionContext ctx, string? content, string? title, string? icon)
		{
			if (string.IsNullOrEmpty(content))
				return;

			ctx.Message.AdditionalData.Add(new AdditionalMessageContentPart
			{
				Content = content,
				ChipTitle = Locale.GetConstKey(string.IsNullOrWhiteSpace(title) ? command.Name : title),
				ChipIcon = ParseIcon(icon),
				IsRestorable = false
			});
		}

		private static VisualIconKind? ParseIcon(string? icon)
		{
			if (string.IsNullOrWhiteSpace(icon))
				return null;

			if (VisualIconKind.TryParse(icon, out var kind))
				return kind;

			Log.Warning("Command script returned an unknown icon '{Icon}'.", icon);
			return null;
		}

		private static string? Text(JsonObject json, string key)
			=> json[key] is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;
	}
}
