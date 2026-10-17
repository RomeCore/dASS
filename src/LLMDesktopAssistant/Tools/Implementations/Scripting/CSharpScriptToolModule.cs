using System.ComponentModel;
using System.Text.Json;
using LLMDesktopAssistant.LLM.Services;
using LLMDesktopAssistant.Localization;
using LLMDesktopAssistant.Scripting.CSX;
using LLMDesktopAssistant.Tools.Execution.Domain;
using LLMDesktopAssistant.Tools.Execution.Support;
using LLMDesktopAssistant.Utils;
using Material.Icons;
using Microsoft.CodeAnalysis.Scripting;

namespace LLMDesktopAssistant.Tools.Implementations.Scripting
{
	/// <summary>
	/// Tool module that executes C# scripts (.csx) via <see cref="CSharpScriptService"/>.
	/// </summary>
	[ToolModule]
	public class CSharpScriptToolModule : ToolModule
	{
		private readonly CSharpScriptService _scriptService;

		/// <summary>
		/// Initializes a new instance of the <see cref="CSharpScriptToolModule"/> class.
		/// </summary>
		/// <param name="scriptService">The service used to run C# scripts.</param>
		public CSharpScriptToolModule(CSharpScriptService scriptService)
		{
			_scriptService = scriptService;

			AddTool(new ToolInitializationInfo
			{
				Executor = Execute,
				StreamingAnalyzer = ExecuteStreaming,
				PreviewExecutor = ExecutePreview,
				Name = "csx-execute",
				Description = """
					# MAIN INFO
					Executes C# (.csx) code compiled with Roslyn scripting and returns the script result.
					Scripts are compiled on every execution (no state is shared between executions)
					and have full access to .NET APIs and all loaded dASS assemblies.

					# GLOBALS
					- `ToolArgs` — the arguments of this tool call as JsonNode (e.g. `(string?)ToolArgs?["code"]`)
					- `Context` — the execution context of the current tool call
					- `Result` — the streaming result API (write output, status, progress, structured result)
					- `Workdir` — the working directory of the current context

					# SMART UX WITH STREAMING AND STATUS ICONS/TITLES
					Use `Result` for streaming output, progress and status (icon names are from Material Icons):

					// 1. Basic streaming output with status icon
					Result.SetStatus("Download", "Processing..."); // "Download" is the icon name, "Processing..." is the title
					Result.Write("Step 1: Starting...");
					Result.Write("Step 2: Working...");
					Result.Write("Step 3: Done!");
					Result.CompleteWithSuccess();

					// 2. Progress bar and Markdown output
					Result.UseMarkdown = true;
					Result.SetStatus("ChartTimeline", "Processing...");
					Result.SetProgress(0, 0, 10); // current, min, max
					for (int i = 1; i <= 10; i++)
					{
						Result.SetProgress(i);
						Result.Write($"  - **Item {i}** completed");
						await Task.Delay(100); // simulate work
					}
					Result.SetProgress(1.0);
					Result.SetStatus("Check", "All done!");
					Result.CompleteWithSuccess();

					// 3. Structured result + error handling
					try
					{
						var data = File.ReadAllText(Path.Combine(Workdir, "data.json"));
						Result.SetStructured(JsonNode.Parse(data));
						Result.SetStatus("FileCheck", "Loaded");
						Result.CompleteWithSuccess();
					}
					catch (Exception ex)
					{
						Result.SetStatus("AlertCircle", "File not found");
						Result.Write("Error: " + ex.Message);
						Result.CompleteWithError();
						return;
					}

					// The return value is used as the structured result when `Result.SetStructured` was not called.
					return "done";
					""",
				NameKey = Locale.GetKey("tool.name.csx-execute"),
				DescriptionKey = Locale.GetKey("tool.description.csx-execute"),
				CategoryKey = Locale.GetConstKey("C#"),
				DefaultExpectedBehaviour = ToolBehaviour.PossiblyUnexpected
			});
		}

		private StreamingToolAnalysisResult ExecuteStreaming(string? csharp, IToolExecutionContext ctx)
		{
			int lines = 0;
			if (csharp != null)
				foreach (var line in csharp.EnumerateLines())
					lines++;

			ctx.Call.SetStatus(MaterialIconKind.LanguageCsharp,
				LocalizationManager.LocalizeStaticFormat("tool.status.script.lines", lines));
			return StreamingToolAnalysisResult.Continue;
		}

		private PreviewToolExecutionResult ExecutePreview(string csharp, IToolExecutionContext ctx)
		{
			ctx.Call.SetStatus(MaterialIconKind.LanguageCsharp, null);
			return new PreviewToolExecutionResult
			{
				ExpectedBehaviour = ToolBehaviour.PossiblyUnexpected
			};
		}

		private async Task<ToolExecutionResult> Execute(
			[Description("The C# code to execute.")] string csharp,
			IToolExecutionContext context,
			CancellationToken cancellationToken = default)
		{
			context.Call.SetStatus(MaterialIconKind.LanguageCsharp, null);
			var result = new CSharpScriptToolResult(context.Call);

			try
			{
				var workdir = context.Services.GetService<IWorkingDirectoryAccessService>()?.GetWorkingDirectory();

				var globals = new CSharpScriptToolGlobals
				{
					ToolArgs = null,
					Context = context,
					Result = result,
					Workdir = workdir ?? Directories.DefaultWorkingDirectory,
				};

				var returnValue = await _scriptService.RunAsync(csharp, globals, cancellationToken);

				if (context.Call.StructuredResult == null && returnValue != null)
				{
					try
					{
						context.Call.SetStructuredResult(JsonSerializer.SerializeToNode(returnValue));
					}
					catch (Exception ex)
					{
						result.Write("Failed to serialize return value: " + ex.Message);
					}
				}

				result.Write("Script returned: " + returnValue);
				result.CompleteWithSuccess();
			}
			catch (CompilationErrorException cex)
			{
				result.Write("Compilation errors:");
				foreach (var diagnostic in cex.Diagnostics)
					result.Write(diagnostic.ToString());
				result.CompleteWithError();
			}
			catch (Exception ex)
			{
				result.Write("Caught error: " + (DebugHelper.IsDebug ? ex.ToString() : ex.Message));
				result.CompleteWithError();
			}

			return result.ToExecutionResult();
		}
	}
}
