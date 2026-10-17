using System.IO;
using System.Text.Json.Nodes;
using LLMDesktopAssistant.Addons;
using LLMDesktopAssistant.Addons.Management;
using LLMDesktopAssistant.Desktop.Execution;
using LLMDesktopAssistant.LLM.Services;
using LLMDesktopAssistant.Scripting;
using LLMDesktopAssistant.Services;
using LLMDesktopAssistant.StructuredValues.Converters;
using LLMDesktopAssistant.Tools;
using LLMDesktopAssistant.Tools.Execution.Domain;
using LLMDesktopAssistant.Tools.Execution.Executors;
using LLMDesktopAssistant.Tools.Execution.Support;
using LLMDesktopAssistant.Tools.Scripting;
using Microsoft.Extensions.DependencyInjection;

namespace LLMDesktopAssistant.Desktop.Scripting.Python
{
	/// <summary>
	/// Python implementation of <see cref="IScriptableToolEngine"/>.
	/// Handles tools written in Python with YAML frontmatter in `"""` docstring blocks.
	/// Requires Python runtime and optional virtual environment.
	/// Only available on Desktop platform.
	/// </summary>
	[Service(typeof(IScriptableToolEngine))]
	public class PythonToolEngine : IScriptableToolEngine
	{
		private readonly IProcessLauncher _processLauncher;
		private readonly PythonHelperService _pythonHelperService;

		public ScriptLanguageType Language => ScriptLanguageType.Python;

		public IScriptableToolEngineDescriptor Descriptor { get; } = new PythonToolEngineDescriptor();

		public PythonToolEngine(IProcessLauncher processLauncher, PythonHelperService pythonHelperService)
		{
			_processLauncher = processLauncher;
			_pythonHelperService = pythonHelperService;
		}

		public IToolMainExecutor CreateExecutor(ToolInfo tool)
		{
			return new DelegateMainExecutor(async (args, ctx, cancellationToken) =>
			{
				try
				{
					string pythonCode = $"""
						import sys
						sys.stdout.reconfigure(encoding="utf-8")
						sys.stderr.reconfigure(encoding="utf-8")
						tool_args = {PythonStructuredSerializer.ToPythonString(args.ToConstNodeValue())}
						{tool.Body}
						""";

					var addonInvalidator = ctx.Services.GetRequiredService<IAddonManagerInvalidator>();
					var chatSettings = ctx.Services.GetRequiredService<IChatSettingsService>().Settings;
					var workDir = chatSettings.Environment.GetEffectiveWorkingDirectories().GetWorkingDirectory();

					var tempPyFile = Path.GetFullPath(Path.Combine(workDir, $"{Guid.NewGuid()}.py"));
					File.WriteAllText(tempPyFile, pythonCode);

					ProcessDescriptor? process = null;
					try
					{
						process = _processLauncher.Launch(_pythonHelperService.CreateLaunchParameters(
							chatSettings.Environment, $"python \"{tempPyFile}\"", "Python Tool", false, true), cancellationToken);
						addonInvalidator.Invalidate(AddonKind.All);

						int exitCode = await process;
						ctx.Call.SetResult(process.Output + $"\nProcess exited with code {exitCode}. Check terminal output above for details.");
						return exitCode == 0 ? ToolExecutionResult.Succeeded : ToolExecutionResult.Failed;
					}
					catch (Exception ex)
					{
						if (process != null)
							ctx.Call.SetResult(process.Output + $"\nProcess finished with error: {ex.Message}");
						else
							ctx.Call.SetResult(ex.Message);
						return ToolExecutionResult.Failed;
					}
					finally
					{
						File.Delete(tempPyFile);
					}
				}
				catch (Exception ex)
				{
					ctx.Call.SetResult($"Python execution error: {ex.Message}");
					return ToolExecutionResult.Failed;
				}
			});
		}
	}
}
