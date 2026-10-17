using System.Text.Json;
using LLMDesktopAssistant.LLM.Services;
using LLMDesktopAssistant.Services;
using LLMDesktopAssistant.Tools;
using LLMDesktopAssistant.Tools.Execution.Domain;
using LLMDesktopAssistant.Tools.Execution.Executors;
using LLMDesktopAssistant.Tools.Execution.Support;
using LLMDesktopAssistant.Tools.Scripting;
using LLMDesktopAssistant.Utils;
using Microsoft.CodeAnalysis.Scripting;

namespace LLMDesktopAssistant.Scripting.CSX
{
	/// <summary>
	/// C# Script implementation of <see cref="IScriptableToolEngine"/>.
	/// Handles meta tools written in C# (.csx) with YAML frontmatter in <c>/* ... */</c> comment blocks.
	/// Scripts are compiled with Roslyn and have full access to all loaded assemblies.
	/// </summary>
	[Service(typeof(IScriptableToolEngine))]
	public class CSharpScriptToolEngine : IScriptableToolEngine
	{
		private readonly CSharpScriptService _scriptService;

		/// <inheritdoc/>
		public ScriptLanguageType Language => ScriptLanguageType.CSharpScript;

		/// <inheritdoc/>
		public IScriptableToolEngineDescriptor Descriptor { get; } = new CSharpScriptToolEngineDescriptor();

		/// <summary>
		/// Initializes a new instance of the <see cref="CSharpScriptToolEngine"/> class.
		/// </summary>
		/// <param name="scriptService">The service used to run C# scripts.</param>
		public CSharpScriptToolEngine(CSharpScriptService scriptService)
		{
			_scriptService = scriptService;
		}

		/// <inheritdoc/>
		public IToolMainExecutor CreateExecutor(ToolInfo tool)
		{
			return new DelegateMainExecutor(async (args, ctx, cancellationToken) =>
			{
				var result = new CSharpScriptToolResult(ctx.Call);

				try
				{
					var workdir = ctx.Services.GetService<IWorkingDirectoryAccessService>()?.GetWorkingDirectory();

					var globals = new CSharpScriptToolGlobals
					{
						ToolArgs = args,
						Context = ctx,
						Result = result,
						Workdir = workdir ?? Directories.DefaultWorkingDirectory,
					};

					var returnValue = await _scriptService.RunAsync(tool.Body, globals, cancellationToken);

					if (ctx.Call.StructuredResult == null && returnValue != null)
					{
						try
						{
							ctx.Call.SetStructuredResult(JsonSerializer.SerializeToNode(returnValue));
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
					result.Write("Caught error: " + ex.Message);
					result.CompleteWithError();
				}

				return result.ToExecutionResult();
			});
		}
	}
}
