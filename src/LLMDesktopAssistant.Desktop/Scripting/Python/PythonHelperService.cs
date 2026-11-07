using System.Collections.Immutable;
using System.IO;
using LLMDesktopAssistant.Desktop.Execution;
using LLMDesktopAssistant.Desktop.Scripting.Bash;
using LLMDesktopAssistant.Desktop.Scripting.Batch;
using LLMDesktopAssistant.Desktop.Scripting.PowerShell;
using LLMDesktopAssistant.LLM.Settings;
using LLMDesktopAssistant.Services;

namespace LLMDesktopAssistant.Desktop.Scripting.Python
{
	/// <summary>
	/// Builds the launch parameters of a python invocation, optionally preceded by a venv activation.
	/// </summary>
	/// <remarks>
	/// The activation script's extension picks the shell, because a venv script changes the environment of the shell
	/// that <em>reads</em> it: a .bat is <c>call</c>ed, a .ps1 and a .sh are dot-sourced, and running either as a child
	/// process would change nothing in the shell that then runs python.
	/// </remarks>
	[Service]
	public class PythonHelperService(
		IBashInterpreterLocator bashInterpreterLocator,
		IPowerShellInterpreterLocator powerShellInterpreterLocator,
		IBatchInterpreterLocator batchInterpreterLocator)
	{
		/// <summary>
		/// Builds the launch parameters of a python invocation, optionally preceded by a venv activation.
		/// </summary>
		/// <param name="envSettings">The chat's environment settings.</param>
		/// <param name="command">The command line to run inside the environment.</param>
		/// <param name="processName">The display name of the process.</param>
		/// <param name="runInTerminal">Whether the process runs in an interactive terminal.</param>
		/// <param name="useAddonEnvironment">
		/// Whether the scriptable addons' venv applies instead of the native tools' one.
		/// </param>
		public ProcessLaunchParameters CreateLaunchParameters(ChatEnvironmentSettings envSettings, string command,
			string? processName, bool runInTerminal, bool useAddonEnvironment)
		{
			var configuration = envSettings.EnsureAdditional<PythonEnvironmentConfiguration>();
			var venvPath = useAddonEnvironment
				? configuration.AddonVenvActivateScriptPath ?? configuration.VenvActivateScriptPath
				: configuration.VenvActivateScriptPath;

			var (fileName, arguments, verbatim) = string.IsNullOrWhiteSpace(venvPath)
				? PlatformShell(envSettings, command)
				: ActivationShell(venvPath.Trim(), command, envSettings);

			return new ProcessLaunchParameters
			{
				ProcessName = processName,
				RunInTerminal = runInTerminal,
				FileName = fileName,
				Arguments = arguments,
				VerbatimArguments = verbatim,
				WorkingDirectory = envSettings.GetEffectiveWorkingDirectories().GetWorkingDirectory()
			};
		}

		/// <summary>
		/// The shell of a machine with no venv: <c>cmd</c> on Windows and bash elsewhere.
		/// </summary>
		private (string FileName, ImmutableList<string> Arguments, bool Verbatim) PlatformShell(
			ChatEnvironmentSettings settings, string command)
		{
			if (!OperatingSystem.IsWindows())
				return BashShell(settings, command);

			var cmd = batchInterpreterLocator.Resolve();
			if (!cmd.Found)
				throw new InvalidOperationException(cmd.Problem);

			return (cmd.Path!, ["/d", "/s", "/c", $"\"{command}\""], true);
		}

		/// <summary>
		/// The shell that can activate the given script, with the activation folded into the same command line.
		/// </summary>
		private (string FileName, ImmutableList<string> Arguments, bool Verbatim) ActivationShell(string venvPath,
			string command, ChatEnvironmentSettings settings)
		{
			switch (Path.GetExtension(venvPath).ToLowerInvariant())
			{
				case ".bat":
				case ".cmd":
				{
					var cmd = batchInterpreterLocator.Resolve();
					if (!cmd.Found)
						throw new InvalidOperationException(cmd.Problem);

					// /s makes cmd treat the rest as one token and strip the outer pair itself.
					return (cmd.Path!, ["/d", "/s", "/c", $"\"call \"{venvPath}\" && {command}\""], true);
				}
				case ".ps1":
				{
					var powerShell = powerShellInterpreterLocator.Resolve(null);
					if (!powerShell.Found)
						throw new InvalidOperationException(powerShell.Problem);

					// Dot-sourced: an activation script sets variables in the calling scope and loses them in a child.
					return (powerShell.Path!,
						["-NoLogo", "-NoProfile", "-ExecutionPolicy", "Bypass", "-Command", $". \"{venvPath}\"; {command}"],
						false);
				}
				// A stock venv on Unix ships bin/activate with no extension at all, so "extensionless" is the POSIX shell.
				case ".sh":
				case "":
				{
					// A Windows venv ships the POSIX script too, and it writes a Windows path into PATH: a POSIX shell
					// cannot resolve it and reports "python: command not found". Only the batch and ps1 scripts work.
					if (OperatingSystem.IsWindows()
						&& Path.GetDirectoryName(venvPath) is { } directory
						&& File.Exists(Path.Combine(directory, "python.exe")))
					{
						throw new InvalidOperationException(
							$"The venv activation script '{venvPath}' is a POSIX script for a Windows venv; " +
							"pick activate.bat or Activate.ps1 next to it.");
					}

					return BashShell(settings, $". \"{venvPath}\" && {command}");
				}
				default:
					throw new InvalidOperationException(
						$"The venv activation script '{venvPath}' must be .bat, .cmd, .ps1 or .sh.");
			}
		}

		private (string FileName, ImmutableList<string> Arguments, bool Verbatim) BashShell(
			ChatEnvironmentSettings settings, string line)
		{
			var configured = settings.GetEffectiveAdditionalSettings()
				.OfType<BashEnvironmentConfiguration>().FirstOrDefault()?.ExecutablePath;

			var bash = bashInterpreterLocator.Resolve(configured);
			if (!bash.Found)
				throw new InvalidOperationException(bash.Problem);

			return (bash.Path!, ["-c", line], false);
		}
	}
}
