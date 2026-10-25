using System.ComponentModel;
using LLMDesktopAssistant.Desktop.Execution;
using LLMDesktopAssistant.LLM.Settings;
using LLMDesktopAssistant.Scripting;
using LLMDesktopAssistant.Services;
using LLMDesktopAssistant.Services.Instances;

namespace LLMDesktopAssistant.Desktop.Scripting.PowerShell
{
	/// <summary>
	/// The PowerShell block of the chat environment: creates it, finds it, and checks it by asking the interpreter for
	/// its version.
	/// </summary>
	/// <remarks>
	/// The check reports the version and the path, because "which PowerShell did discovery pick" is the question
	/// behind almost every difference between a script that works and one that does not - and the answer is not
	/// visible anywhere else in the UI.
	/// </remarks>
	[Service(typeof(IScriptEngineEnvConfigurationProvider))]
	public class PowerShellEnvironmentConfigurationProvider(
		IPowerShellInterpreterLocator interpreterLocator,
		IProcessLauncher processLauncher,
		IExplorerOpener explorerOpener) : IScriptEngineEnvConfigurationProvider
	{
		/// <inheritdoc/>
		public ScriptLanguageType Language => ScriptLanguageType.PowerShell;

		/// <inheritdoc/>
		public AdditionalEnvironmentSetting CreateConfiguration() => new PowerShellEnvironmentConfiguration();

		/// <inheritdoc/>
		public INotifyPropertyChanged CreateViewModel(AdditionalEnvironmentSetting configuration)
			=> new PowerShellEnvironmentConfigurationViewModel((PowerShellEnvironmentConfiguration)configuration,
				interpreterLocator, explorerOpener);

		/// <inheritdoc/>
		public AdditionalEnvironmentSetting? FindConfiguration(IEnumerable<AdditionalEnvironmentSetting> existingSettings)
			=> existingSettings.OfType<PowerShellEnvironmentConfiguration>().FirstOrDefault();

		/// <inheritdoc/>
		public async Task<ScriptEnvironmentCheckResult> CheckConfigurationAsync(ChatEnvironmentSettings settings,
			AdditionalEnvironmentSetting configuration, CancellationToken cancellationToken = default)
		{
			var configured = (configuration as PowerShellEnvironmentConfiguration)?.ExecutablePath;
			var interpreter = interpreterLocator.Resolve(configured);
			if (!interpreter.Found)
				return new ScriptEnvironmentCheckResult { Success = false, Message = interpreter.Problem };

			try
			{
				var parameters = new ProcessLaunchParameters
				{
					ProcessName = "PowerShell Check",
					FileName = interpreter.Path!,
					// -Command keeps the check independent of the execution policy, which only guards script files.
					Arguments = ["-NoLogo", "-NoProfile", "-Command", "$PSVersionTable.PSVersion.ToString()"],
					WorkingDirectory = settings.GetEffectiveWorkingDirectories().GetWorkingDirectory(),
					TimeOut = TimeSpan.FromSeconds(30)
				};

				var process = processLauncher.Launch(parameters, cancellationToken);
				var exitCode = await process;
				var version = process.PlainOutput?.StdOut?.FirstOrDefault()?.Trim();

				return new ScriptEnvironmentCheckResult
				{
					Success = exitCode == 0,
					Message = exitCode == 0
						? $"{version} ({interpreter.Path})"
						: process.Output
				};
			}
			catch (Exception ex)
			{
				return new ScriptEnvironmentCheckResult { Success = false, Message = ex.Message };
			}
		}
	}
}
