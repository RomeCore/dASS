using System.ComponentModel;
using LLMDesktopAssistant.Desktop.Scripting.ExternalProcess;
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
		ExternalProcessEngineRegistry registry,
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
			if (registry.ResolveLanguage(Language) is not { } language)
				return new ScriptEnvironmentCheckResult { Success = false, Message = $"'{Language}' is not registered." };

			var availability = await language.Engine
				.CheckAvailabilityAsync(settings, cancellationToken: cancellationToken)
				.ConfigureAwait(false);

			return new ScriptEnvironmentCheckResult
			{
				Success = availability.Available,
				Message = availability.Available
					? $"{availability.Version} ({availability.ExecutablePath})"
					: availability.Problem
			};
		}
	}
}
