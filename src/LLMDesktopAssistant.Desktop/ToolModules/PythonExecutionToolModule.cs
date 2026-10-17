using System.ComponentModel;
using System.IO;
using System.Runtime.InteropServices;
using LLMDesktopAssistant.Addons.Management;
using LLMDesktopAssistant.Desktop.Execution;
using LLMDesktopAssistant.Desktop.Scripting.Python;
using LLMDesktopAssistant.Desktop.ToolModules.Terminal;
using LLMDesktopAssistant.LLM.Domain;
using LLMDesktopAssistant.LLM.Services;
using LLMDesktopAssistant.LLM.Settings;
using LLMDesktopAssistant.Localization;
using LLMDesktopAssistant.Tools;
using LLMDesktopAssistant.Tools.Execution.Domain;
using LLMDesktopAssistant.Tools.Execution.Support;
using Material.Icons;

namespace LLMDesktopAssistant.Desktop.ToolModules
{
	/// <summary>
	/// Terminal-based Python execution tool module.
	/// Runs Python scripts and displays live output in an embedded terminal emulator.
	/// Replaces PythonInterpreterToolModule with terminal UI support.
	/// </summary>
	[ToolModule]
	public class PythonExecutionToolModule : TerminalBasedToolModule
	{
		private readonly IChatSettingsService _chatSettings;
		private readonly IWorkingDirectoryAccessService _fileAccess;
		private readonly PythonHelperService _pythonHelperService;

		public PythonExecutionToolModule(IChatSettingsService chatSettings, IWorkingDirectoryAccessService fileAccess,
			PythonHelperService pythonHelperService, IProcessLauncher processLauncher, IAddonManagerInvalidator addonInvalidator)
			: base(processLauncher, addonInvalidator)
		{
			_chatSettings = chatSettings;
			_fileAccess = fileAccess;
			_pythonHelperService = pythonHelperService;

			AddTool(new ToolInitializationInfo
			{
				Executor = Execute,
				StreamingAnalyzer = ExecuteStreaming,
				PreviewExecutor = ExecutePreview,
				Name = "execute-python",
				Description = "Executes Python in isolated virtual environment (global variables are not accessible between scripts) from the working directory. It returns STOUT of the executed code (e.g., print('Hello World!') should return 'Hello World!'). Displays live output in a terminal.",
				NameKey = Locale.GetKey("tool.name.execute-python"),
				DescriptionKey = Locale.GetKey("tool.description.execute-python"),
				CategoryKey = Locale.GetConstKey("Python"),
				DefaultExpectedBehaviour = ToolBehaviour.ExecuteExternalProcess | ToolBehaviour.PossiblyUnexpected | ToolBehaviour.RunTerminal
			});

			AddTool(new ToolInitializationInfo
			{
				Executor = ExecuteVenvShell,
				StreamingAnalyzer = ExecuteVenvShellStreaming,
				PreviewExecutor = ExecuteVenvShellPreview,
				Name = "execute-python_venv_shell",
				Description = "Executes shell script in a Python's virtual environment from the working directory. Useful for installing packages via 'pip'. Displays live output in a terminal.",
				NameKey = Locale.GetKey("tool.name.execute-python_venv_shell"),
				DescriptionKey = Locale.GetKey("tool.description.execute-python_venv_shell"),
				CategoryKey = Locale.GetConstKey("Python"),
				DefaultExpectedBehaviour = ToolBehaviour.ExecuteExternalProcess | ToolBehaviour.PossiblyUnexpected | ToolBehaviour.RunTerminal
			});

			AddTool(new ToolInitializationInfo
			{
				Executor = GetInstalledPackagesList,
				Name = "python-get_installed_packages_list",
				Description = "Returns the list of installed packages in the current Python's virtual environment.",
				NameKey = Locale.GetKey("tool.name.python-get_installed_packages_list"),
				DescriptionKey = Locale.GetKey("tool.description.python-get_installed_packages_list"),
				CategoryKey = Locale.GetConstKey("Python"),
				DefaultExpectedBehaviour = ToolBehaviour.ExecuteExternalProcess
			});
		}

		public StreamingToolAnalysisResult ExecuteStreaming(IToolExecutionContext ctx, string? python)
		{
			int lines = 0;
			if (python != null)
				foreach (var line in python.EnumerateLines())
					lines++;

			ctx.Call.SetStatus(MaterialIconKind.LanguagePython,
				LocalizationManager.LocalizeStaticFormat("tool.status.script.lines", lines));
			return StreamingToolAnalysisResult.Continue;
		}

		public PreviewToolExecutionResult ExecutePreview(IToolExecutionContext ctx, string python, bool runTerminal)
		{
			ctx.Call.SetStatus(MaterialIconKind.LanguagePython, null);

			return new PreviewToolExecutionResult
			{
				ExpectedBehaviour = ToolBehaviour.ExecuteExternalProcess | ToolBehaviour.PossiblyUnexpected |
					(runTerminal ? ToolBehaviour.RunTerminal : 0)
			};
		}

		/// <summary>
		/// Executes a Python script in the terminal with live output.
		/// Creates a temporary .py file, runs it via the virtual environment's Python,
		/// and displays the output in an embedded terminal.
		/// </summary>
		public async Task<ToolExecutionResult> Execute(
			[Description("The Python code to execute or the path to *.py file.")] string python,
			[Description("Whether to run the output in an embedded terminal emulator. Use `true` for long-running scripts.")] bool runTerminal,
			IToolExecutionContext context,
			CancellationToken cancellationToken = default)
		{
			var workDir = _chatSettings.Settings.Environment.GetEffectiveWorkingDirectories().GetWorkingDirectory();

			string pyFile;
			bool isTemporaryFile;
			if (!python.Contains('\n') && !python.Contains('\r') && python.EndsWith(".py"))
			{
				// Treat as a file path
				pyFile = _fileAccess.AccessPath(python, DirectoryAccessMode.Execute);
				python = File.ReadAllText(pyFile);

				// Ensure the script is UTF-8 encoded
				python = $"""
				import sys
				sys.stdout.reconfigure(encoding="utf-8")
				sys.stderr.reconfigure(encoding="utf-8")
				{python}
				""";

				// Write to a temporary file
				pyFile = Path.GetFullPath(Path.Combine(workDir, $"_dass_temp_{Guid.NewGuid()}.py"));
				File.WriteAllText(pyFile, python);
				isTemporaryFile = true;
			}
			else
			{
				// Ensure the script is UTF-8 encoded
				python = $"""
				import sys
				sys.stdout.reconfigure(encoding="utf-8")
				sys.stderr.reconfigure(encoding="utf-8")
				{python}
				""";

				// Write to a temporary file
				pyFile = Path.GetFullPath(Path.Combine(workDir, $"_dass_temp_{Guid.NewGuid()}.py"));
				File.WriteAllText(pyFile, python);
				isTemporaryFile = true;
			}

			try
			{
				// Run in terminal
				var result = Run(new TerminalToolRunParameters
				{
					StatusIcon = MaterialIconKind.LanguagePython,
					StatusTitle = null,
					ProcessParameters = _pythonHelperService.CreateLaunchParameters(_chatSettings.Settings.Environment,
						$"python \"{pyFile}\"", "Python", runTerminal, false)
				}, context, cancellationToken);

				_ = result.ContinueWith(t =>
				{
					try
					{
						if (isTemporaryFile && File.Exists(pyFile))
							File.Delete(pyFile);
					}
					catch { }
				}, CancellationToken.None);

				return await result;
			}
			catch (Exception ex)
			{
				try
				{
					if (isTemporaryFile && File.Exists(pyFile))
						File.Delete(pyFile);
				}
				catch { }

				context.Call.SetStatus(MaterialIconKind.LanguagePython, null);
				context.Call.SetResult("Error running Python script: " + ex.Message);
				return ToolExecutionResult.Failed;
			}
		}

		public StreamingToolAnalysisResult ExecuteVenvShellStreaming(IToolExecutionContext ctx, string shell)
		{
			ctx.Call.SetStatus(MaterialIconKind.LanguagePython, $"`{shell}`");
			return StreamingToolAnalysisResult.Continue;
		}

		public PreviewToolExecutionResult ExecuteVenvShellPreview(IToolExecutionContext ctx, string shell, bool runTerminal)
		{
			ctx.Call.SetStatus(MaterialIconKind.LanguagePython, $"`{shell}`");

			return new PreviewToolExecutionResult
			{
				ExpectedBehaviour = ToolBehaviour.ExecuteExternalProcess | ToolBehaviour.PossiblyUnexpected |
					(runTerminal ? ToolBehaviour.RunTerminal : 0)
			};
		}

		/// <summary>
		/// Executes a shell command in the Python virtual environment via terminal.
		/// Useful for pip install, pip list, etc.
		/// </summary>
		public Task<ToolExecutionResult> ExecuteVenvShell(
			[Description("The shell command to run in the Python's virtual environment.")] string shell,
			[Description("Whether to run the output in an embedded terminal emulator. Use `true` for long-running scripts.")] bool runTerminal,
			IToolExecutionContext context,
			CancellationToken cancellationToken = default)
		{
			return Run(new TerminalToolRunParameters
			{
				StatusIcon = MaterialIconKind.LanguagePython,
				StatusTitle = null,
				ProcessParameters = _pythonHelperService.CreateLaunchParameters(_chatSettings.Settings.Environment,
					shell, "Python Shell", runTerminal, false)
			}, context, cancellationToken);
		}

		/// <summary>
		/// Gets the list of installed packages in the current Python virtual environment.
		/// Still returns a simple ToolResult since it's a quick query, not a long-running process.
		/// </summary>
		public Task<ToolExecutionResult> GetInstalledPackagesList(
			IToolExecutionContext context, CancellationToken cancellationToken = default)
		{
			return Run(new TerminalToolRunParameters
			{
				StatusIcon = MaterialIconKind.LanguagePython,
				StatusTitle = null,
				ProcessParameters = _pythonHelperService.CreateLaunchParameters(_chatSettings.Settings.Environment,
					"pip list", "Python Shell", false, false)
			}, context, cancellationToken);
		}
	}
}
