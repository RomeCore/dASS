using System.ComponentModel;
using LLMDesktopAssistant.Desktop.Execution;
using LLMDesktopAssistant.LLM.Settings;
using LLMDesktopAssistant.Scripting;
using LLMDesktopAssistant.Services;
using LLMDesktopAssistant.Services.Instances;

namespace LLMDesktopAssistant.Desktop.Scripting.Bash
{
	/// <summary>
	/// The bash block of the chat environment: creates it, finds it, and checks it by running <c>--version</c>.
	/// </summary>
	/// <remarks>
	/// The check message on success names the executable and its version, so the user sees which bash discovery
	/// actually picked - the answer to "Git Bash is installed, why does it not work".
	/// </remarks>
	[Service(typeof(IScriptEngineEnvConfigurationProvider))]
	public class BashEnvironmentConfigurationProvider(
		IBashInterpreterLocator interpreterLocator,
		IProcessLauncher processLauncher,
		IExplorerOpener explorerOpener) : IScriptEngineEnvConfigurationProvider
	{
		/// <inheritdoc/>
		public ScriptLanguageType Language => ScriptLanguageType.Bash;

		/// <inheritdoc/>
		public AdditionalEnvironmentSetting CreateConfiguration() => new BashEnvironmentConfiguration();

		/// <inheritdoc/>
		public INotifyPropertyChanged CreateViewModel(AdditionalEnvironmentSetting configuration)
			=> new BashEnvironmentConfigurationViewModel((BashEnvironmentConfiguration)configuration,
				interpreterLocator, explorerOpener);

		/// <inheritdoc/>
		public AdditionalEnvironmentSetting? FindConfiguration(IEnumerable<AdditionalEnvironmentSetting> existingSettings)
			=> existingSettings.OfType<BashEnvironmentConfiguration>().FirstOrDefault();

		/// <inheritdoc/>
		public async Task<ScriptEnvironmentCheckResult> CheckConfigurationAsync(ChatEnvironmentSettings settings,
			AdditionalEnvironmentSetting configuration, CancellationToken cancellationToken = default)
		{
			var configured = (configuration as BashEnvironmentConfiguration)?.ExecutablePath;
			var interpreter = interpreterLocator.Resolve(configured);
			if (!interpreter.Found)
				return new ScriptEnvironmentCheckResult { Success = false, Message = interpreter.Problem };

			try
			{
				var parameters = new ProcessLaunchParameters
				{
					ProcessName = "Bash Check",
					FileName = interpreter.Path!,
					Arguments = ["--version"],
					WorkingDirectory = settings.GetEffectiveWorkingDirectories().GetWorkingDirectory(),
					TimeOut = TimeSpan.FromSeconds(10)
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
