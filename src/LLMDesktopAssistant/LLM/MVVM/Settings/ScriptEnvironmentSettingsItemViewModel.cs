using System.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LLMDesktopAssistant.Localization;
using LLMDesktopAssistant.Scripting;

namespace LLMDesktopAssistant.LLM.Settings
{
	/// <summary>
	/// Wraps a script engine environment configuration view model and provides
	/// a unified command for validating the underlying environment configuration.
	/// </summary>
	public class ScriptEnvironmentSettingsItemViewModel : ViewModelBase
	{
		private readonly IScriptEngineEnvConfigurationProvider _provider;
		private readonly ChatEnvironmentSettings _settings;
		private readonly AdditionalEnvironmentSetting _configuration;

		private ScriptEnvironmentCheckResult? _checkResult;
		private bool _isChecking;

		/// <summary>
		/// Gets the wrapped environment configuration view model.
		/// </summary>
		public INotifyPropertyChanged Content { get; }

		/// <summary>
		/// Gets the command that runs the environment configuration check.
		/// </summary>
		public IAsyncRelayCommand CheckCommand { get; }

		/// <summary>
		/// Gets a value indicating whether an environment check is currently in progress.
		/// </summary>
		public bool IsChecking
		{
			get => _isChecking;
			private set => SetProperty(ref _isChecking, value);
		}

		/// <summary>
		/// Gets the result of the last environment check, or <see langword="null"/> if no check has been performed yet.
		/// </summary>
		public ScriptEnvironmentCheckResult? CheckResult
		{
			get => _checkResult;
			private set
			{
				if (SetProperty(ref _checkResult, value))
				{
					RaisePropertyChanged(nameof(CheckResultText));
					RaisePropertyChanged(nameof(IsCheckSuccessful));
					RaisePropertyChanged(nameof(IsCheckFailed));
				}
			}
		}

		/// <summary>
		/// Gets the localized message to display for the last check result.
		/// </summary>
		public string? CheckResultText
		{
			get
			{
				if (CheckResult is null)
					return null;

				// A provider may answer a success with details worth reading (which bash, which version), so its message
				// wins over the generic "ready" when there is one.
				if (!string.IsNullOrWhiteSpace(CheckResult.Message))
					return CheckResult.Message;

				return LocalizationManager.LocalizeStatic(CheckResult.Success ? "env.check.success" : "env.check.error");
			}
		}

		/// <summary>
		/// Gets a value indicating whether the last check finished successfully.
		/// </summary>
		public bool IsCheckSuccessful => CheckResult is { Success: true };

		/// <summary>
		/// Gets a value indicating whether the last check failed.
		/// </summary>
		public bool IsCheckFailed => CheckResult is { Success: false };

		/// <summary>
		/// Initializes a new instance of the <see cref="ScriptEnvironmentSettingsItemViewModel"/> class.
		/// </summary>
		/// <param name="provider">The configuration provider used to check the environment.</param>
		/// <param name="settings">The chat environment settings the check is performed against.</param>
		/// <param name="configuration">The additional environment configuration to check.</param>
		/// <param name="content">The wrapped view model that edits the configuration.</param>
		public ScriptEnvironmentSettingsItemViewModel(IScriptEngineEnvConfigurationProvider provider,
			ChatEnvironmentSettings settings, AdditionalEnvironmentSetting configuration, INotifyPropertyChanged content)
		{
			_provider = provider;
			_settings = settings;
			_configuration = configuration;
			Content = content;
			CheckCommand = new AsyncRelayCommand(CheckAsync);
		}

		private async Task CheckAsync(CancellationToken cancellationToken)
		{
			IsChecking = true;
			CheckResult = null;
			try
			{
				CheckResult = await _provider.CheckConfigurationAsync(_settings, _configuration, cancellationToken);
			}
			catch (OperationCanceledException)
			{
				CheckResult = null;
			}
			catch (Exception ex)
			{
				CheckResult = new ScriptEnvironmentCheckResult { Success = false, Message = ex.Message };
			}
			finally
			{
				IsChecking = false;
			}
		}
	}
}
