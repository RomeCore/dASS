using System.ComponentModel;
using LLMDesktopAssistant.LLM.Settings;
using LLMDesktopAssistant.Scripting;
using LLMDesktopAssistant.Services;
using LLMDesktopAssistant.Services.Instances;

namespace LLMDesktopAssistant.Desktop.Scripting.Python
{
	[Service(typeof(IScriptEngineEnvConfigurationProvider))]
	public class PythonEnvironmentConfigurationProvider(
		IExplorerOpener explorerOpener,
		IPythonInterpreterLocator interpreterLocator
	) : IScriptEngineEnvConfigurationProvider
	{
		public ScriptLanguageType Language => ScriptLanguageType.Python;

		public AdditionalEnvironmentSetting CreateConfiguration()
		{
			return new PythonEnvironmentConfiguration();
		}

		public INotifyPropertyChanged CreateViewModel(AdditionalEnvironmentSetting configuration)
		{
			return new PythonEnvironmentConfigurationViewModel((PythonEnvironmentConfiguration)configuration, explorerOpener);
		}

		public AdditionalEnvironmentSetting? FindConfiguration(IEnumerable<AdditionalEnvironmentSetting> existingSettings)
		{
			return existingSettings.OfType<PythonEnvironmentConfiguration>().FirstOrDefault();
		}

		public async Task<ScriptEnvironmentCheckResult> CheckConfigurationAsync(ChatEnvironmentSettings settings,
			AdditionalEnvironmentSetting configuration, CancellationToken cancellationToken = default)
		{
			// Says which interpreter it found, like the bash and PowerShell checks do.
			var resolution = await interpreterLocator.ResolveAsync(settings, useAddonEnvironment: false, cancellationToken);

			return new ScriptEnvironmentCheckResult
			{
				Success = resolution.Found,
				Message = resolution.Found ? resolution.Version : resolution.Problem
			};
		}
	}
}
