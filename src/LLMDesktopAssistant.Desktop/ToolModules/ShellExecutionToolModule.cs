using System.ComponentModel;
using LLMDesktopAssistant.Desktop.Execution;
using LLMDesktopAssistant.Desktop.ToolModules.Terminal;
using LLMDesktopAssistant.LLM.Domain;
using LLMDesktopAssistant.LLM.Services;
using LLMDesktopAssistant.Tools;
using LLMDesktopAssistant.Tools.Specifiers;

using Material.Icons;
using LLMDesktopAssistant.Localization;
using LLMDesktopAssistant.Addons.Management;

namespace LLMDesktopAssistant.Desktop.ToolModules
{
	[ToolModule(chatScoped: true)]
	public class ShellExecutionToolModule : TerminalBasedToolModule
	{
		private readonly IWorkingDirectoryAccessService _wdAccess;

		public ShellExecutionToolModule(IWorkingDirectoryAccessService wdAccess,
			IProcessLauncher processLauncher, IAddonManagerInvalidator addonInvalidator)
			: base(processLauncher, addonInvalidator)
		{
			_wdAccess = wdAccess;

			AddTool(new ToolInitializationInfo
			{
				Executor = ExecuteBash,
				StreamingAnalyzer = ExecuteBashStreaming,
				PreviewExecutor = ExecuteBashPreview,
				SpecifierAnalyzer = AnalyzeBashSpecifier,
				Name = "shell-bash",
				Description = $"Executes UNIX BASH command or script from the current working directory. Examples: `git status`, `python script.py`",
				NameKey = Locale.GetKey("tool.name.shell-bash"),
				DescriptionKey = Locale.GetKey("tool.description.shell-bash"),
				CategoryKey = Locale.GetKey("tool.category.scripting"),
				DefaultExpectedBehaviour = ToolBehaviour.ExecuteExternalProcess | ToolBehaviour.PossiblyUnexpected | ToolBehaviour.RunTerminal,
				SpecifierParameters = ["runTerminal", "wait"],
			});

			AddTool(new ToolInitializationInfo
			{
				Executor = ExecuteBatch,
				StreamingAnalyzer = ExecuteBatchStreaming,
				PreviewExecutor = ExecuteBatchPreview,
				SpecifierAnalyzer = AnalyzeBatchSpecifier,
				Name = "shell-batch",
				Description = $"Executes WINDOWS BATCH command or script from the current working directory. Examples: `git status`, `python script.py`",
				NameKey = Locale.GetKey("tool.name.shell-batch"),
				DescriptionKey = Locale.GetKey("tool.description.shell-batch"),
				CategoryKey = Locale.GetKey("tool.category.scripting"),
				DefaultExpectedBehaviour = ToolBehaviour.ExecuteExternalProcess | ToolBehaviour.PossiblyUnexpected | ToolBehaviour.RunTerminal,
				SpecifierParameters = ["runTerminal", "wait"],
			});

			AddTool(new ToolInitializationInfo
			{
				Executor = ExecutePowerShell,
				StreamingAnalyzer = ExecutePowerShellStreaming,
				PreviewExecutor = ExecutePowerShellPreview,
				SpecifierAnalyzer = AnalyzePowerShellSpecifier,
				Name = "shell-powershell",
				Description = $"Executes WINDOWS POWERSHELL command or script from the current working directory. Examples: `git status`, `python script.py`",
				NameKey = Locale.GetKey("tool.name.shell-powershell"),
				DescriptionKey = Locale.GetKey("tool.description.shell-powershell"),
				CategoryKey = Locale.GetKey("tool.category.scripting"),
				DefaultExpectedBehaviour = ToolBehaviour.ExecuteExternalProcess | ToolBehaviour.PossiblyUnexpected | ToolBehaviour.RunTerminal,
				SpecifierParameters = ["runTerminal", "wait"],
			});
		}

		private StreamingToolArgumentsAnalysisResult ExecuteBashStreaming(string bash)
		{
			return new StreamingToolArgumentsAnalysisResult
			{
				StatusIcon = MaterialIconKind.Console,
				StatusTitle = $"`{bash}`"
			};
		}

		private PreviewToolExecutionResult ExecuteBashPreview(string bash, bool runTerminal)
		{
			return new PreviewToolExecutionResult
			{
				StatusIcon = MaterialIconKind.Console,
				StatusTitle = $"`{bash}`",
				ExpectedBehaviour = ToolBehaviour.ExecuteExternalProcess | ToolBehaviour.PossiblyUnexpected |
					(runTerminal ? ToolBehaviour.RunTerminal : 0)
			};
		}

		private ReactiveToolResult ExecuteBash(
			ToolExecutionContext context,
			[Description("The bash command to run.")] string bash,
			[Description("Whether to run the output in an embedded terminal emulator. Use `true` for long-running scripts.")] bool runTerminal,
			[Description("Whether to wait for the script to finish before returning. Use false for fire-and-forget scripts.")] bool wait = true,
			CancellationToken cancellationToken = default)
		{
			var workDir = _wdAccess.GetWorkingDirectory();

			return Run(new TerminalToolRunParameters
			{
				StatusIcon = MaterialIconKind.Console,
				StatusTitle = $"`{bash}`",
				Wait = wait,
				ProcessParameters = new ProcessLaunchParameters
				{
					ProcessName = "Bash",
					RunInTerminal = runTerminal,
					FileName = "/bin/bash",
					Arguments = ["-c", bash],
					WorkingDirectory = workDir
				}
			}, context, cancellationToken);
		}

		private StreamingToolArgumentsAnalysisResult ExecuteBatchStreaming(string batch)
		{
			return new StreamingToolArgumentsAnalysisResult
			{
				StatusIcon = MaterialIconKind.Console,
				StatusTitle = $"`{batch}`"
			};
		}

		private PreviewToolExecutionResult ExecuteBatchPreview(string batch, bool runTerminal)
		{
			return new PreviewToolExecutionResult
			{
				StatusIcon = MaterialIconKind.Console,
				StatusTitle = $"`{batch}`",
				ExpectedBehaviour = ToolBehaviour.ExecuteExternalProcess | ToolBehaviour.PossiblyUnexpected |
					(runTerminal ? ToolBehaviour.RunTerminal : 0)
			};
		}

		private ReactiveToolResult ExecuteBatch(
			ToolExecutionContext context,
			[Description("The batch command to run.")] string batch,
			[Description("Whether to run the output in an embedded terminal emulator. Use `true` for long-running scripts.")] bool runTerminal,
			[Description("Whether to wait for the script to finish before returning. Use false for fire-and-forget scripts.")] bool wait = true,
			CancellationToken cancellationToken = default)
		{
			var workDir = _wdAccess.GetWorkingDirectory();

			return Run(new TerminalToolRunParameters
			{
				StatusIcon = MaterialIconKind.Console,
				StatusTitle = $"`{batch}`",
				Wait = wait,
				ProcessParameters = new ProcessLaunchParameters
				{
					ProcessName = "Batch",
					RunInTerminal = runTerminal,
					FileName = "cmd.exe",
					Arguments = ["/c", $"\"{batch}\""],
					VerbatimArguments = true,
					WorkingDirectory = workDir
				}
			}, context, cancellationToken);
		}

		private StreamingToolArgumentsAnalysisResult ExecutePowerShellStreaming(string powershell)
		{
			return new StreamingToolArgumentsAnalysisResult
			{
				StatusIcon = MaterialIconKind.Powershell,
				StatusTitle = $"`{powershell}`"
			};
		}

		private PreviewToolExecutionResult ExecutePowerShellPreview(string powershell, bool runTerminal)
		{
			return new PreviewToolExecutionResult
			{
				StatusIcon = MaterialIconKind.Powershell,
				StatusTitle = $"`{powershell}`",
				ExpectedBehaviour = ToolBehaviour.ExecuteExternalProcess | ToolBehaviour.PossiblyUnexpected |
					(runTerminal ? ToolBehaviour.RunTerminal : 0)
			};
		}

		private ReactiveToolResult ExecutePowerShell(
			ToolExecutionContext context,
			[Description("The powershell command to run.")] string powershell,
			[Description("Whether to run the output in an embedded terminal emulator. Use `true` for long-running scripts.")] bool runTerminal,
			[Description("Whether to wait for the script to finish before returning. Use false for fire-and-forget scripts.")] bool wait = true,
			CancellationToken cancellationToken = default)
		{
			var workDir = _wdAccess.GetWorkingDirectory();

			return Run(new TerminalToolRunParameters
			{
				StatusIcon = MaterialIconKind.Powershell,
				StatusTitle = $"`{powershell}`",
				Wait = wait,
				ProcessParameters = new ProcessLaunchParameters
				{
					ProcessName = "PowerShell",
					RunInTerminal = runTerminal,
					FileName = "powershell",
					Arguments = ["-Command", powershell],
					WorkingDirectory = workDir
				}
			}, context, cancellationToken);
		}

		private SpecifierMatchResult AnalyzeBashSpecifier(
			Specifier specifier,
			string bash,
			bool runTerminal = false,
			bool wait = true)
		{
			return SpecifierMatcher.Match(specifier, ShellCommandSplitter.Split(bash), ShellParameters(runTerminal, wait));
		}

		private SpecifierMatchResult AnalyzeBatchSpecifier(
			Specifier specifier,
			string batch,
			bool runTerminal = false,
			bool wait = true)
		{
			return SpecifierMatcher.Match(specifier,
				ShellCommandSplitter.Split(batch, singleAmpersandIsSeparator: true), ShellParameters(runTerminal, wait));
		}

		private SpecifierMatchResult AnalyzePowerShellSpecifier(
			Specifier specifier,
			string powershell,
			bool runTerminal = false,
			bool wait = true)
		{
			var result = SpecifierMatcher.Match(specifier, ShellCommandSplitter.Split(powershell), ShellParameters(runTerminal, wait));
			return result;
		}

		private static IEnumerable<KeyValuePair<string, string>> ShellParameters(bool runTerminal, bool wait)
		{
			yield return new KeyValuePair<string, string>("runTerminal", runTerminal ? "true" : "false");
			yield return new KeyValuePair<string, string>("wait", wait ? "true" : "false");
		}
	}
}