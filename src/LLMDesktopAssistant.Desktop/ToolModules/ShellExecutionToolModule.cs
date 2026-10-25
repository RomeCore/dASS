using System.ComponentModel;
using LLMDesktopAssistant.Addons.Management;
using LLMDesktopAssistant.Desktop.Execution;
using LLMDesktopAssistant.Desktop.Scripting.Bash;
using LLMDesktopAssistant.Desktop.Scripting.Batch;
using LLMDesktopAssistant.Desktop.Scripting.PowerShell;
using LLMDesktopAssistant.Desktop.ToolModules.Terminal;
using LLMDesktopAssistant.LLM.Services;
using LLMDesktopAssistant.LLM.Settings;
using Microsoft.Extensions.DependencyInjection;
using LLMDesktopAssistant.Localization;
using LLMDesktopAssistant.Tools;
using LLMDesktopAssistant.Tools.Execution.Domain;
using LLMDesktopAssistant.Tools.Execution.Support;
using LLMDesktopAssistant.Tools.Specifiers;
using LLMDesktopAssistant.Utils;
using Material.Icons;

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
				// A shell may touch any file, and what it will do is unknowable: it excludes every other
				// file claim for the duration of the call.
				StaticSyncKeys = [new(ToolSyncKeyKind.AllFiles, string.Empty, ToolSyncAccess.Write)],
				ChatAvailablePredicate = (tool, svc) => GetBashInterpreter(svc).Found,
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
				StaticSyncKeys = [new(ToolSyncKeyKind.AllFiles, string.Empty, ToolSyncAccess.Write)],
				ChatAvailablePredicate = (tool, svc) => GetBatchInterpreter(svc).Found,
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
				StaticSyncKeys = [new(ToolSyncKeyKind.AllFiles, string.Empty, ToolSyncAccess.Write)],
				ChatAvailablePredicate = (tool, svc) => GetPowerShellInterpreter(svc).Found,
				Name = "shell-powershell",
				Description = $"Executes WINDOWS POWERSHELL command or script from the current working directory. Examples: `git status`, `python script.py`",
				NameKey = Locale.GetKey("tool.name.shell-powershell"),
				DescriptionKey = Locale.GetKey("tool.description.shell-powershell"),
				CategoryKey = Locale.GetKey("tool.category.scripting"),
				DefaultExpectedBehaviour = ToolBehaviour.ExecuteExternalProcess | ToolBehaviour.PossiblyUnexpected | ToolBehaviour.RunTerminal,
				SpecifierParameters = ["runTerminal", "wait"],
			});
		}

		// Previously, main arguments were called 'bash', 'batch', 'powershell' instead of 'command'.
		// If arguments are named corresponding to their languages, the arguments syntax highlighting will work,
		// but LLM will hallicunate wrong argument names ('command', for example), so it is renamed now
		// and syntax highlighting will not work.

		// Streaming analyzers leave blocks unclosed, so incremental rendering will work
		const int TruncationLength = 500;

		private static ExecutableResolution GetBashInterpreter(IServiceProvider services)
		{
			var locator = services.GetRequiredService<IBashInterpreterLocator>();
			var configured = services.GetService<IChatSettingsService>()?.Settings.Environment
				.GetEffectiveAdditionalSettings().OfType<BashEnvironmentConfiguration>().FirstOrDefault()?.ExecutablePath;
			return locator.Resolve(configured);
		}

		private static ExecutableResolution GetBatchInterpreter(IServiceProvider services)
		{
			var locator = services.GetRequiredService<IBatchInterpreterLocator>();
			return locator.Resolve();
		}

		private static ExecutableResolution GetPowerShellInterpreter(IServiceProvider services)
		{
			var locator = services.GetRequiredService<IPowerShellInterpreterLocator>();
			var configured = services.GetService<IChatSettingsService>()?.Settings.Environment
				.GetEffectiveAdditionalSettings().OfType<PowerShellEnvironmentConfiguration>().FirstOrDefault()?.ExecutablePath;
			return locator.Resolve(configured);
		}

		private StreamingToolAnalysisResult ExecuteBashStreaming(IToolExecutionContext ctx, string? command)
		{
			ctx.Call.SetStatus(MaterialIconKind.Console, $"""
				```bash
				{command.TruncateEnd(TruncationLength)}
				""");
			return (command?.Length ?? 0) > TruncationLength ? StreamingToolAnalysisResult.Stop : StreamingToolAnalysisResult.Continue;
		}

		private PreviewToolExecutionResult ExecuteBashPreview(bool runTerminal)
		{
			return new PreviewToolExecutionResult
			{
				ExpectedBehaviour = ToolBehaviour.ExecuteExternalProcess | ToolBehaviour.PossiblyUnexpected |
					(runTerminal ? ToolBehaviour.RunTerminal : 0)
			};
		}

		private Task<ToolExecutionResult> ExecuteBash(
			IToolExecutionContext context,
			[Description("The bash command to run.")] string command,
			[Description("Whether to run the output in an embedded terminal emulator. Use `true` for long-running scripts.")] bool runTerminal,
			[Description("Whether to wait for the script to finish before returning. Use false for fire-and-forget scripts.")] bool wait = true,
			CancellationToken cancellationToken = default)
		{
			var interpreter = GetBashInterpreter(context.Services);
			if (!interpreter.Found)
			{
				context.Call.SetResult(interpreter.Problem ?? "No bash interpreter was found on this machine.");
				return Task.FromResult(ToolExecutionResult.Failed);
			}

			var workDir = _wdAccess.GetWorkingDirectory();

			return Run(new TerminalToolRunParameters
			{
				Wait = wait,
				ProcessParameters = new ProcessLaunchParameters
				{
					ProcessName = "Bash",
					RunInTerminal = runTerminal,
					FileName = interpreter.Path!,
					Arguments = ["-c", command],
					WorkingDirectory = workDir
				}
			}, context, cancellationToken);
		}

		private StreamingToolAnalysisResult ExecuteBatchStreaming(IToolExecutionContext ctx, string? command)
		{
			ctx.Call.SetStatus(MaterialIconKind.Console, $"""
				```batch
				{command.TruncateEnd(TruncationLength)}
				""");
			return (command?.Length ?? 0) > TruncationLength ? StreamingToolAnalysisResult.Stop : StreamingToolAnalysisResult.Continue;
		}

		private PreviewToolExecutionResult ExecuteBatchPreview(bool runTerminal)
		{
			return new PreviewToolExecutionResult
			{
				ExpectedBehaviour = ToolBehaviour.ExecuteExternalProcess | ToolBehaviour.PossiblyUnexpected |
					(runTerminal ? ToolBehaviour.RunTerminal : 0)
			};
		}

		private Task<ToolExecutionResult> ExecuteBatch(
			IToolExecutionContext context,
			[Description("The batch command to run.")] string command,
			[Description("Whether to run the output in an embedded terminal emulator. Use `true` for long-running scripts.")] bool runTerminal,
			[Description("Whether to wait for the script to finish before returning. Use false for fire-and-forget scripts.")] bool wait = true,
			CancellationToken cancellationToken = default)
		{
			var interpreter = GetBatchInterpreter(context.Services);
			if (!interpreter.Found)
			{
				context.Call.SetResult(interpreter.Problem ?? "No command processor was found on this machine.");
				return Task.FromResult(ToolExecutionResult.Failed);
			}

			var workDir = _wdAccess.GetWorkingDirectory();

			return Run(new TerminalToolRunParameters
			{
				Wait = wait,
				ProcessParameters = new ProcessLaunchParameters
				{
					ProcessName = "Batch",
					RunInTerminal = runTerminal,
					FileName = interpreter.Path!,
					Arguments = ["/c", $"\"{command}\""],
					VerbatimArguments = true,
					WorkingDirectory = workDir
				}
			}, context, cancellationToken);
		}

		private StreamingToolAnalysisResult ExecutePowerShellStreaming(IToolExecutionContext ctx, string? command)
		{
			ctx.Call.SetStatus(MaterialIconKind.Powershell, $"""
				```powershell
				{command.TruncateEnd(TruncationLength)}
				""");
			return (command?.Length ?? 0) > TruncationLength ? StreamingToolAnalysisResult.Stop : StreamingToolAnalysisResult.Continue;
		}

		private PreviewToolExecutionResult ExecutePowerShellPreview(bool runTerminal)
		{
			return new PreviewToolExecutionResult
			{
				ExpectedBehaviour = ToolBehaviour.ExecuteExternalProcess | ToolBehaviour.PossiblyUnexpected |
					(runTerminal ? ToolBehaviour.RunTerminal : 0)
			};
		}

		private Task<ToolExecutionResult> ExecutePowerShell(
			IToolExecutionContext context,
			[Description("The powershell command to run.")] string command,
			[Description("Whether to run the output in an embedded terminal emulator. Use `true` for long-running scripts.")] bool runTerminal,
			[Description("Whether to wait for the script to finish before returning. Use false for fire-and-forget scripts.")] bool wait = true,
			CancellationToken cancellationToken = default)
		{
			var configured = context.Services.GetService<IChatSettingsService>()?.Settings.Environment
				.GetEffectiveAdditionalSettings().OfType<PowerShellEnvironmentConfiguration>().FirstOrDefault()?.ExecutablePath;

			var interpreter = GetPowerShellInterpreter(context.Services);
			if (!interpreter.Found)
			{
				context.Call.SetResult(interpreter.Problem ?? "No PowerShell interpreter was found on this machine.");
				return Task.FromResult(ToolExecutionResult.Failed);
			}

			var workDir = _wdAccess.GetWorkingDirectory();

			return Run(new TerminalToolRunParameters
			{
				Wait = wait,
				ProcessParameters = new ProcessLaunchParameters
				{
					ProcessName = "PowerShell",
					RunInTerminal = runTerminal,
					FileName = interpreter.Path!,
					Arguments = ["-Command", command],
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