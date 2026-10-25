using System.Collections.Immutable;
using System.Text.Json.Nodes;
using LLMDesktopAssistant.Controls.Icons;
using LLMDesktopAssistant.Scripting;
using LLMDesktopAssistant.Services;
using LLMDesktopAssistant.StructuredValues;
using LLMDesktopAssistant.StructuredValues.Converters;
using LLMDesktopAssistant.Tools;
using LLMDesktopAssistant.Tools.Execution.Domain;
using LLMDesktopAssistant.Tools.Execution.Executors;
using LLMDesktopAssistant.Tools.Execution.Support;
using LLMDesktopAssistant.Tools.Scripting;
using Serilog;

namespace LLMDesktopAssistant.Desktop.Scripting.ExternalProcess
{
	/// <summary>
	/// The out-of-process tool kind: it names the variable a tool exposes, passes the addon's flags to the language
	/// engine and maps the JSON result onto the call.
	/// </summary>
	/// <remarks>
	/// One adapter serves every out-of-process language; the extension of the tool's own file picks the engine. The
	/// JSON keys are read one by one and unknown ones are ignored - a tool card tolerates extra fields that a newer
	/// engine writes.
	/// </remarks>
	[Service(typeof(IScriptableToolEngine))]
	public class ExternalProcessToolEngine(ExternalProcessEngineRegistry registry) : IScriptableToolEngine
	{
		private static readonly TimeSpan _timeout = TimeSpan.FromMinutes(30);

		/// <inheritdoc/>
		public IReadOnlyList<IScriptEngineDescriptor> Descriptors => registry.Descriptors;

		/// <inheritdoc/>
		public IToolMainExecutor CreateExecutor(ToolInfo tool)
			=> new DelegateMainExecutor((args, ctx, ct) => ExecuteAsync(tool, args, ctx, ct));

		private async Task<ToolExecutionResult> ExecuteAsync(ToolInfo tool, JsonNode? args,
			IToolExecutionContext ctx, CancellationToken ct)
		{
			var engine = registry.Resolve(tool.Path ?? throw new InvalidOperationException(
				$"The path of the tool '{tool.Name}' is not set.")).Engine;

			var variables = ImmutableDictionary<string, INodeValue?>.Empty
				.Add("tool_args", args.ToConstNodeValue());

			if (tool.RunTerminal)
			{
				var terminal = await engine.RunInTerminalAsync(new ExternalProcessScriptTerminalParameters
				{
					Kind = ScriptableAddonKind.Tool,
					Body = tool.Body,
					Variables = variables,
					InputMode = tool.InputMode,
					ProcessName = tool.Name,
					Wait = tool.TerminalWait
				}, ctx.Call, ctx.Services, ct);

				var completed = terminal.Status is ExternalProcessStatus.Succeeded;
				ctx.Call.SetResult(completed ? terminal.Output : Join(terminal.Output, terminal.Error));
				return completed ? ToolExecutionResult.Succeeded : ToolExecutionResult.Failed;
			}

			var result = await engine.RunAsync(new ExternalProcessScriptParameters
			{
				Kind = ScriptableAddonKind.Tool,
				Body = tool.Body,
				Variables = variables,
				InputMode = tool.InputMode,
				OutputMode = tool.OutputMode,
				OutputPattern = tool.OutputPattern,
				ProcessName = tool.Name,
				TimeOut = _timeout
			}, ctx.Services, ct);

			if (result.Status is not ExternalProcessStatus.Succeeded)
			{
				ctx.Call.SetResult(Join(result.Output, result.Error));
				return ToolExecutionResult.Failed;
			}

			Apply(ctx.Call, result);
			return ToolExecutionResult.Succeeded;
		}

		/// <summary>
		/// Applies the tool's result contract: <c>status</c>, <c>result</c>, <c>markdown</c> and <c>structured</c>.
		/// </summary>
		private static void Apply(IToolCall call, ExternalProcessScriptResult result)
		{
			var json = result.Json as JsonObject;

			// Without a promised structure the result is the merged stream: a script with no JSON channel is heard
			// exactly as a terminal would show it. With JSON, only stdout is a candidate - stderr would poison it.
			call.SetResult(json is null
				? result.Output
				: Text(json, "result") ?? result.Stdout);

			if (json?["markdown"] is JsonValue markdown && markdown.TryGetValue<bool>(out var useMarkdown))
				call.UseMarkdown = useMarkdown;

			if (json?["structured"] is { } structured)
				call.StructuredResult = structured.DeepClone();

			if (json?["status"] is not JsonObject status)
				return;

			call.SetStatusTitle(Text(status, "title"));

			var icon = Text(status, "icon");
			if (icon is null)
				return;

			if (VisualIconKind.TryParse(icon, out var kind))
			{
				call.SetStatusIcon(kind);
			}
			else
			{
				// An unknown icon is a cosmetic mistake, not a broken invocation: the title still says what happened.
				Log.Warning("Tool script '{Tool}' returned an unknown icon '{Icon}'.", call.ToolName, icon);
			}
		}

		private static string? Text(JsonObject json, string key)
			=> json[key] is JsonValue value && value.TryGetValue<string>(out var text) ? text : null;

		private static string Join(string? output, string? error)
			=> string.IsNullOrWhiteSpace(error) ? output ?? string.Empty
				: string.IsNullOrWhiteSpace(output) ? error
				: output + "\n\n" + error;
	}
}
