using System.ComponentModel;
using LLMDesktopAssistant.Desktop.Scripting.ExternalProcess;
using LLMDesktopAssistant.LLM.Settings;
using LLMDesktopAssistant.Scripting;
using LLMDesktopAssistant.Services;
using LLMDesktopAssistant.Services.Instances;

namespace LLMDesktopAssistant.Desktop.Scripting.Python
{
	[Service(typeof(IScriptEngineEnvConfigurationProvider))]
	public class PythonEnvironmentConfigurationProvider(
		IExplorerOpener explorerOpener,
		ExternalProcessEngineRegistry registry
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
			if (registry.ResolveLanguage(Language) is not { } language)
				return new ScriptEnvironmentCheckResult { Success = false, Message = $"'{Language}' is not registered." };

			// This block edits the native tools' venv; the scriptable addons have one of their own.
			var availability = await language.Engine
				.CheckAvailabilityAsync(settings, useAddonEnvironment: false, cancellationToken)
				.ConfigureAwait(false);

			return new ScriptEnvironmentCheckResult
			{
				Success = availability.Available,
				Message = availability switch
				{
					{ Available: false } => availability.Problem,
					{ ExecutablePath: { } path } => $"{availability.Version} ({path})",
					_ => availability.Version
				}
			};
		}
	}
}
