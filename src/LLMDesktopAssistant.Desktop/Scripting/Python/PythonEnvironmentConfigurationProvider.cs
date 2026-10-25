using System.ComponentModel;
using LLMDesktopAssistant.Desktop.Execution;
using LLMDesktopAssistant.LLM.Settings;
using LLMDesktopAssistant.Scripting;
using LLMDesktopAssistant.Services;
using LLMDesktopAssistant.Services.Instances;

namespace LLMDesktopAssistant.Desktop.Scripting.Python
{
	[Service(typeof(IScriptEngineEnvConfigurationProvider))]
	public class PythonEnvironmentConfigurationProvider(
		IExplorerOpener explorerOpener,
		IProcessLauncher processLauncher,
		PythonHelperService pythonHelperService
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
			try
			{
				var launchParameters = pythonHelperService.CreateLaunchParameters(settings, "python -V",
					"Python Check", false, useAddonEnvironment: false);
				var process = processLauncher.Launch(launchParameters, cancellationToken);
				var exitCode = await process;

				if (exitCode == 0)
					return new ScriptEnvironmentCheckResult
					{
						Success = true,
						Message = null
					};
				else
					return new ScriptEnvironmentCheckResult
					{
						Success = false,
						Message = process.Output
					};
			}
			catch (Exception ex)
			{
				return new ScriptEnvironmentCheckResult
				{
					Success = false,
					Message = ex.Message
				};
			}
		}
	}
}
