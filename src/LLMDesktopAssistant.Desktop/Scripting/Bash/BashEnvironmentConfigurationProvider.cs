using System.ComponentModel;
using LLMDesktopAssistant.Desktop.Scripting.ExternalProcess;
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
		ExternalProcessEngineRegistry registry,
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
