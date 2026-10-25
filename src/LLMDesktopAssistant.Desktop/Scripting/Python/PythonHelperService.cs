using System.Collections.Immutable;
using System.IO;
using LLMDesktopAssistant.Desktop.Execution;
using LLMDesktopAssistant.LLM.Domain;
using LLMDesktopAssistant.LLM.Settings;
using LLMDesktopAssistant.Services;

namespace LLMDesktopAssistant.Desktop.Scripting.Python
{
	[Service]
	public class PythonHelperService
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
			var workDir = envSettings.GetEffectiveWorkingDirectories().GetWorkingDirectory();
			var pythonConfig = envSettings.EnsureAdditional<PythonEnvironmentConfiguration>();
			var venvPath = useAddonEnvironment
				? (pythonConfig.AddonVenvActivateScriptPath ?? pythonConfig.VenvActivateScriptPath)
				: pythonConfig.VenvActivateScriptPath;

			string interpreter = OperatingSystem.IsWindows() ? "cmd.exe" : "/bin/bash";

			if (!string.IsNullOrWhiteSpace(venvPath))
			{
				var venvExtension = Path.GetExtension(venvPath);
				switch (venvExtension)
				{
					case ".bat":
						interpreter = "cmd.exe";
						command = $"call \"{venvPath}\" && {command}";
						break;
					case ".sh":
						interpreter = "/bin/bash";
						command = $"\"{venvPath}\" && {command}";
						break;
					case ".ps1":
						interpreter = "powershell";
						command = $"-ExecutionPolicy Bypass -File \"{venvPath}\"; {command}";
						break;
					default:
						if (OperatingSystem.IsWindows())
							command = $"call \"{venvPath}\" && {command}";
						else
							command = $"\"{venvPath}\" && {command}";
						break;
				}
			}

			ImmutableList<string> arguments = interpreter switch
			{
				"cmd.exe" => ["/c", $"\"{command}\""],
				"/bin/bash" => ["-c", command],
				"powershell" => ["-Command", command],
				_ => throw new InvalidOperationException("Unsupported interpreter")
			};
			bool verbatimArguments = interpreter == "cmd.exe";

			return new ProcessLaunchParameters
			{
				ProcessName = processName,
				RunInTerminal = runInTerminal,
				FileName = interpreter,
				Arguments = arguments,
				VerbatimArguments = verbatimArguments,
				WorkingDirectory = workDir
			};
		}
	}
}
