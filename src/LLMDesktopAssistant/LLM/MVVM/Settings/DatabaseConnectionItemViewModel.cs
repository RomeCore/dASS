using CommunityToolkit.Mvvm.Input;
using LLMDesktopAssistant.ApiKeys;
using LLMDesktopAssistant.Data.Connectors;
using LLMDesktopAssistant.Localization;

namespace LLMDesktopAssistant.LLM.Settings
{
	/// <summary>
	/// Wraps a <see cref="DatabaseConnectionSetting"/> and provides the connection test command
	/// with its status for the settings UI.
	/// </summary>
	public class DatabaseConnectionItemViewModel : ViewModelBase
	{
		private readonly IApiKeyManagerService _apiKeyManager;
		private readonly IDatabaseConnectionCache _cache;
		private readonly IDatabaseConnectionManager _connectionManager;

		private bool _isTesting;
		private bool? _testResult;
		private string? _testResultMessage;

		/// <summary>
		/// Gets the wrapped database connection setting.
		/// </summary>
		public DatabaseConnectionSetting Setting { get; }

		/// <summary>
		/// Gets or sets the connector type item selected for the connection.
		/// </summary>
		public DatabaseConnectorTypeItem SelectedConnectorTypeItem
		{
			get => DatabaseConnectorTypeItem.FromValue(Setting.ConnectorType);
			set
			{
				if (value is not null)
					Setting.ConnectorType = value.Value;
			}
		}

		/// <summary>
		/// Gets the command that tests the connection.
		/// </summary>
		public IAsyncRelayCommand TestCommand { get; }

		/// <summary>
		/// Gets the command that activates and opens the connection.
		/// </summary>
		public IAsyncRelayCommand ConnectCommand { get; }

		/// <summary>
		/// Gets a value indicating whether the connection is currently being tested.
		/// </summary>
		public bool IsTesting
		{
			get => _isTesting;
			private set => SetProperty(ref _isTesting, value);
		}

		/// <summary>
		/// Gets the result of the last connection test, or <see langword="null"/> if no test has been performed yet.
		/// </summary>
		public bool? TestResult
		{
			get => _testResult;
			private set
			{
				if (SetProperty(ref _testResult, value))
				{
					RaisePropertyChanged(nameof(TestResultText));
					RaisePropertyChanged(nameof(IsTestSuccessful));
					RaisePropertyChanged(nameof(IsTestFailed));
				}
			}
		}

		/// <summary>
		/// Gets the localized message to display for the last test result.
		/// </summary>
		public string? TestResultText
		{
			get
			{
				if (TestResult is null)
					return null;
				if (TestResult == true)
					return LocalizationManager.LocalizeStatic("db.test.success");
				return string.IsNullOrWhiteSpace(_testResultMessage)
					? LocalizationManager.LocalizeStatic("db.test.error")
					: _testResultMessage;
			}
		}

		/// <summary>
		/// Gets a value indicating whether the last test finished successfully.
		/// </summary>
		public bool IsTestSuccessful => TestResult == true;

		/// <summary>
		/// Gets a value indicating whether the last test failed.
		/// </summary>
		public bool IsTestFailed => TestResult == false;

		/// <summary>
		/// Initializes a new instance of the <see cref="DatabaseConnectionItemViewModel"/> class.
		/// </summary>
		/// <param name="setting">The database connection setting to wrap.</param>
		/// <param name="apiKeyManager">The API key manager used to resolve encrypted connection strings.</param>
		/// <param name="cache">The cache used to test the connection.</param>
		/// <param name="connectionManager">The connection manager used to activate the connection.</param>
		public DatabaseConnectionItemViewModel(DatabaseConnectionSetting setting,
			IApiKeyManagerService apiKeyManager, IDatabaseConnectionCache cache,
			IDatabaseConnectionManager connectionManager)
		{
			Setting = setting;
			_apiKeyManager = apiKeyManager;
			_cache = cache;
			_connectionManager = connectionManager;
			TestCommand = new AsyncRelayCommand(TestAsync);
			ConnectCommand = new AsyncRelayCommand(ConnectAsync);
		}

		private async Task ConnectAsync(CancellationToken cancellationToken)
		{
			IsTesting = true;
			TestResult = null;
			_testResultMessage = null;
			try
			{
				if (string.IsNullOrWhiteSpace(Setting.Name))
					throw new InvalidOperationException(LocalizationManager.LocalizeStatic("db.test.empty_name"));

				var connectionString = Setting.GetConnectionString(_apiKeyManager);
				if (string.IsNullOrWhiteSpace(connectionString))
					throw new InvalidOperationException(LocalizationManager.LocalizeStatic("db.test.empty_connection_string"));

				_connectionManager.Activate(Setting.Name);
				await _connectionManager.OpenCurrentAsync(cancellationToken);
				TestResult = true;
			}
			catch (OperationCanceledException)
			{
				_testResultMessage = null;
				TestResult = null;
			}
			catch (Exception ex)
			{
				_testResultMessage = ex.Message;
				TestResult = false;
			}
			finally
			{
				IsTesting = false;
			}
		}

		private async Task TestAsync(CancellationToken cancellationToken)
		{
			IsTesting = true;
			_testResultMessage = null;
			TestResult = null;
			try
			{
				var connectionString = Setting.GetConnectionString(_apiKeyManager);
				if (string.IsNullOrWhiteSpace(connectionString))
					throw new InvalidOperationException(LocalizationManager.LocalizeStatic("db.test.empty_connection_string"));

				await _cache.TestAsync(Setting.ConnectorType, connectionString, cancellationToken);
				TestResult = true;
			}
			catch (OperationCanceledException)
			{
				_testResultMessage = null;
				TestResult = null;
			}
			catch (Exception ex)
			{
				_testResultMessage = ex.Message;
				TestResult = false;
			}
			finally
			{
				IsTesting = false;
			}
		}
	}
}
