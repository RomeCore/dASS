using System.Collections.Specialized;
using System.ComponentModel;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.Input;
using LLMDesktopAssistant.ApiKeys;
using LLMDesktopAssistant.Data.Connectors;
using LLMDesktopAssistant.Localization;
using LLMDesktopAssistant.Utils;

namespace LLMDesktopAssistant.LLM.Settings
{
	/// <summary>
	/// A selectable database connector type item for the settings UI.
	/// </summary>
	public class DatabaseConnectorTypeItem
	{
		/// <summary>
		/// Gets all available connector types.
		/// </summary>
		public static readonly ImmutableList<DatabaseConnectorTypeItem> All =
		[
			new() { Value = DatabaseConnectorType.SQLite, DisplayName = "SQLite" },
			new() { Value = DatabaseConnectorType.SQLServer, DisplayName = "SQL Server" },
			new() { Value = DatabaseConnectorType.PostgreSQL, DisplayName = "PostgreSQL" }
		];

		/// <summary>
		/// Gets the connector type value.
		/// </summary>
		public required DatabaseConnectorType Value { get; init; }

		/// <summary>
		/// Gets the display name of the connector type.
		/// </summary>
		public required string DisplayName { get; init; }

		/// <summary>
		/// Returns the item for the specified connector type value.
		/// Falls back to a generated item when the value is not part of <see cref="All"/>
		/// (e.g. a connector type that is not available on the current platform).
		/// </summary>
		/// <param name="value">The connector type value.</param>
		/// <returns>The matching item.</returns>
		public static DatabaseConnectorTypeItem FromValue(DatabaseConnectorType value) =>
			All.FirstOrDefault(i => i.Value == value)
			?? new DatabaseConnectorTypeItem { Value = value, DisplayName = value.ToString() };
	}

	/// <summary>
	/// ViewModel for the Database connections settings tab.
	/// Manages named database connections and the custom connection string.
	/// </summary>
	[ViewModelFor(typeof(ChatDatabaseSettingsView))]
	public class ChatDatabaseSettingsViewModel : ViewModelBase
	{
		private readonly IApiKeyManagerService _apiKeyManager;
		private readonly IDatabaseConnectionCache _cache;
		private readonly IDatabaseConnectionManager _connectionManager;
		private readonly HashSet<DatabaseConnectionSetting> _subscribedSettings = [];
		private DatabaseConnectionSettings? _subscribedEffectiveConnection;

		private bool _isCustomTesting;
		private bool? _customTestResult;
		private string? _customTestResultMessage;

		/// <summary>
		/// Gets the underlying database settings.
		/// </summary>
		public ChatDatabaseSettings DatabaseSettings { get; }

		/// <summary>
		/// Gets the effective database connection configuration resolved by the current inheritance level.
		/// </summary>
		public DatabaseConnectionSettings EffectiveDatabaseConnection => DatabaseSettings.GetEffectiveDatabaseConnection();

		private InheritanceLevelItem _selectedDatabaseConnectionInheritance;
		/// <summary>
		/// Gets or sets the inheritance level for the database connections group.
		/// </summary>
		public InheritanceLevelItem SelectedDatabaseConnectionInheritance
		{
			get => _selectedDatabaseConnectionInheritance;
			set
			{
				if (SetProperty(ref _selectedDatabaseConnectionInheritance, value) && value != null)
					DatabaseSettings.DatabaseConnectionInheritance = value.Value;
			}
		}

		/// <summary>
		/// Gets the database connector types available on the current platform
		/// (derived from the registered <see cref="IDatabaseConnector"/> services).
		/// </summary>
		public ImmutableList<DatabaseConnectorTypeItem> ConnectorTypes { get; }

		/// <summary>
		/// Gets or sets the connector type item selected for the custom connection.
		/// </summary>
		public DatabaseConnectorTypeItem SelectedCustomConnectorTypeItem
		{
			get => DatabaseConnectorTypeItem.FromValue(EffectiveDatabaseConnection.CustomConnectorType);
			set
			{
				if (value is not null)
					EffectiveDatabaseConnection.CustomConnectorType = value.Value;
			}
		}

		/// <summary>
		/// Gets the view models for the named database connections.
		/// </summary>
		public RangeObservableCollection<DatabaseConnectionItemViewModel> ConnectionItems { get; } = [];

		/// <summary>
		/// Gets the manager items representing all configured connections (named and custom)
		/// with their current connection state from the cache.
		/// </summary>
		public RangeObservableCollection<DatabaseConnectionManagerItem> ManagerItems { get; } = [];

		/// <summary>
		/// Gets a value indicating whether no connections are currently configured.
		/// </summary>
		public bool IsManagerEmpty => ManagerItems.Count == 0;

		public IRelayCommand AddDatabaseConnectionCommand { get; }
		public IRelayCommand<DatabaseConnectionItemViewModel> RemoveDatabaseConnectionCommand { get; }
		public IRelayCommand<DatabaseConnectionItemViewModel> MoveDatabaseConnectionUpCommand { get; }
		public IRelayCommand<DatabaseConnectionItemViewModel> MoveDatabaseConnectionDownCommand { get; }
		public IRelayCommand SetCustomActiveCommand { get; }
		public IRelayCommand<DatabaseConnectionItemViewModel> SetActiveDatabaseConnectionCommand { get; }
		public IRelayCommand<DatabaseConnectionItemViewModel> BrowseSqliteFileCommand { get; }
		public IAsyncRelayCommand TestCustomConnectionCommand { get; }
		public IAsyncRelayCommand ConnectCustomCommand { get; }
		public IAsyncRelayCommand<DatabaseConnectionManagerItem> DisconnectManagerItemCommand { get; }
		public IAsyncRelayCommand DisconnectAllCommand { get; }

		/// <summary>
		/// Gets a value indicating whether the custom connection is currently being tested.
		/// </summary>
		public bool IsCustomTesting
		{
			get => _isCustomTesting;
			private set => SetProperty(ref _isCustomTesting, value);
		}

		/// <summary>
		/// Gets the result of the last custom connection test, or <see langword="null"/> if no test has been performed yet.
		/// </summary>
		public bool? CustomTestResult
		{
			get => _customTestResult;
			private set
			{
				if (SetProperty(ref _customTestResult, value))
				{
					RaisePropertyChanged(nameof(CustomTestResultText));
					RaisePropertyChanged(nameof(IsCustomTestSuccessful));
					RaisePropertyChanged(nameof(IsCustomTestFailed));
				}
			}
		}

		/// <summary>
		/// Gets the localized message to display for the last custom connection test result.
		/// </summary>
		public string? CustomTestResultText
		{
			get
			{
				if (CustomTestResult is null)
					return null;
				if (CustomTestResult == true)
					return LocalizationManager.LocalizeStatic("db.test.success");
				return string.IsNullOrWhiteSpace(_customTestResultMessage)
					? LocalizationManager.LocalizeStatic("db.test.error")
					: _customTestResultMessage;
			}
		}

		/// <summary>
		/// Gets a value indicating whether the last custom connection test finished successfully.
		/// </summary>
		public bool IsCustomTestSuccessful => CustomTestResult == true;

		/// <summary>
		/// Gets a value indicating whether the last custom connection test failed.
		/// </summary>
		public bool IsCustomTestFailed => CustomTestResult == false;

		/// <summary>
		/// Initializes a new instance of the <see cref="ChatDatabaseSettingsViewModel"/> class.
		/// </summary>
		/// <param name="settings">The database settings to edit.</param>
		/// <param name="apiKeyManager">The API key manager used to resolve encrypted connection strings.</param>
		/// <param name="cache">The database connection cache providing the supported connector types and connection tests.</param>
		/// <param name="connectionManager">The connection manager used to activate and disconnect connections.</param>
		public ChatDatabaseSettingsViewModel(ChatDatabaseSettings settings,
			IApiKeyManagerService apiKeyManager,
			IDatabaseConnectionCache cache,
			IDatabaseConnectionManager connectionManager)
		{
			DatabaseSettings = settings;
			_apiKeyManager = apiKeyManager;
			_cache = cache;
			_connectionManager = connectionManager;

			_selectedDatabaseConnectionInheritance = InheritanceLevelItem.AllProfile.First(i => i.Value == settings.DatabaseConnectionInheritance);
			ConnectorTypes = cache.SupportedConnectors
				.Select(DatabaseConnectorTypeItem.FromValue)
				.ToImmutableList();

			settings.PropertyChanged += DatabaseSettings_PropertyChanged;
			cache.ConnectionsChanged += Cache_ConnectionsChanged;
			_subscribedEffectiveConnection = EffectiveDatabaseConnection;
			_subscribedEffectiveConnection.PropertyChanged += EffectiveConnection_PropertyChanged;

			RebuildConnectionItems();
			RebuildManagerItems();

			AddDatabaseConnectionCommand = new RelayCommand(AddDatabaseConnection);
			RemoveDatabaseConnectionCommand = new RelayCommand<DatabaseConnectionItemViewModel>(RemoveDatabaseConnection);
			MoveDatabaseConnectionUpCommand = new RelayCommand<DatabaseConnectionItemViewModel>(MoveDatabaseConnectionUp);
			MoveDatabaseConnectionDownCommand = new RelayCommand<DatabaseConnectionItemViewModel>(MoveDatabaseConnectionDown);
			SetCustomActiveCommand = new RelayCommand(SetCustomActive);
			SetActiveDatabaseConnectionCommand = new RelayCommand<DatabaseConnectionItemViewModel>(SetActiveDatabaseConnection);
			BrowseSqliteFileCommand = new AsyncRelayCommand<DatabaseConnectionItemViewModel>(BrowseSqliteFile);
			TestCustomConnectionCommand = new AsyncRelayCommand(TestCustomConnectionAsync);
			ConnectCustomCommand = new AsyncRelayCommand(ConnectCustomAsync);
			DisconnectManagerItemCommand = new AsyncRelayCommand<DatabaseConnectionManagerItem>(DisconnectManagerItemAsync);
			DisconnectAllCommand = new AsyncRelayCommand(DisconnectAllAsync);
		}

		private void DatabaseSettings_PropertyChanged(object? sender, PropertyChangedEventArgs e)
		{
			if (e.PropertyName == nameof(ChatDatabaseSettings.DatabaseConnectionInheritance))
			{
				_selectedDatabaseConnectionInheritance = InheritanceLevelItem.AllProfile.First(i => i.Value == DatabaseSettings.DatabaseConnectionInheritance);
				RaisePropertyChanged(nameof(SelectedDatabaseConnectionInheritance));
				RaisePropertyChanged(nameof(EffectiveDatabaseConnection));
				RaisePropertyChanged(nameof(SelectedCustomConnectorTypeItem));

				if (_subscribedEffectiveConnection is not null)
					_subscribedEffectiveConnection.PropertyChanged -= EffectiveConnection_PropertyChanged;
				_subscribedEffectiveConnection = EffectiveDatabaseConnection;
				_subscribedEffectiveConnection.PropertyChanged += EffectiveConnection_PropertyChanged;

				RebuildConnectionItems();
				RebuildManagerItems();
			}
		}

		private void Cache_ConnectionsChanged(object? sender, EventArgs e) => RebuildManagerItems();

		private void EffectiveConnection_PropertyChanged(object? sender, PropertyChangedEventArgs e) => RebuildManagerItems();

		private void Setting_PropertyChanged(object? sender, PropertyChangedEventArgs e) => RebuildManagerItems();

		/// <summary>
		/// Rebuilds the manager list from the connections that are currently open in the cache.
		/// Each cached connection is displayed with the name of the matching configured
		/// connection, or as an anonymous custom connection when no match is found.
		/// </summary>
		private void RebuildManagerItems()
		{
			foreach (var item in ManagerItems)
				item.Dispose();
			ManagerItems.Clear();

			foreach (var setting in _subscribedSettings)
				setting.PropertyChanged -= Setting_PropertyChanged;
			_subscribedSettings.Clear();

			var settings = EffectiveDatabaseConnection;
			foreach (var setting in settings.Items)
			{
				setting.PropertyChanged += Setting_PropertyChanged;
				_subscribedSettings.Add(setting);
			}

			var anonymousCounts = new Dictionary<string, int>();

			foreach (var (type, connectionString) in _cache.ActiveConnections)
			{
				// Resolve a friendly name from the configured connections by connection string.
				var named = settings.Items.FirstOrDefault(i => i.IsEnabled
					&& i.ConnectorType == type
					&& string.Equals(i.GetConnectionString(_apiKeyManager), connectionString, StringComparison.OrdinalIgnoreCase));

				if (named is not null && !string.IsNullOrWhiteSpace(named.Name))
				{
					ManagerItems.Add(new DatabaseConnectionManagerItem(named.Name, type, connectionString, isAnonymous: false));
					continue;
				}

				var key = type.ToString();
				anonymousCounts.TryGetValue(key, out var count);
				anonymousCounts[key] = count + 1;
				var displayName = count == 0
					? $"{LocalizationManager.LocalizeStatic("db.custom_connection")}"
					: $"{LocalizationManager.LocalizeStatic("db.custom_connection")} #{count + 1}";
				ManagerItems.Add(new DatabaseConnectionManagerItem(displayName, type, connectionString, isAnonymous: true));
			}

			RaisePropertyChanged(nameof(IsManagerEmpty));
		}

		private void RebuildConnectionItems()
		{
			foreach (var item in ConnectionItems)
				item.Dispose();
			ConnectionItems.Clear();

			var items = EffectiveDatabaseConnection.Items;
			foreach (var setting in items)
				ConnectionItems.Add(CreateItemViewModel(setting));

			items.CollectionChanged += Items_CollectionChanged;
		}

		private void Items_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
		{
			switch (e.Action)
			{
				case NotifyCollectionChangedAction.Add:
					if (e.NewItems is not null)
						foreach (DatabaseConnectionSetting setting in e.NewItems)
							ConnectionItems.Add(CreateItemViewModel(setting));
					break;

				case NotifyCollectionChangedAction.Remove:
					if (e.OldItems is not null)
						foreach (DatabaseConnectionSetting setting in e.OldItems)
						{
							var item = ConnectionItems.FirstOrDefault(i => i.Setting == setting);
							if (item is not null)
							{
								item.Dispose();
								ConnectionItems.Remove(item);
							}
						}
					break;

				case NotifyCollectionChangedAction.Reset:
					foreach (var item in ConnectionItems)
						item.Dispose();
					ConnectionItems.Clear();
					if (sender is RangeObservableCollection<DatabaseConnectionSetting> items)
						foreach (var setting in items)
							ConnectionItems.Add(CreateItemViewModel(setting));
					break;
			}

			RebuildManagerItems();
		}

		private DatabaseConnectionItemViewModel CreateItemViewModel(DatabaseConnectionSetting setting)
		{
			return new DatabaseConnectionItemViewModel(setting, _apiKeyManager, _cache, _connectionManager);
		}

		private void SetCustomActive()
		{
			var connection = EffectiveDatabaseConnection;
			connection.IsCustomActive = true;
			foreach (var item in connection.Items)
				item.IsActive = false;
		}

		private void SetActiveDatabaseConnection(DatabaseConnectionItemViewModel? item)
		{
			if (item is null)
				return;
			var connection = EffectiveDatabaseConnection;
			connection.IsCustomActive = false;
			foreach (var connectionItem in connection.Items)
				connectionItem.IsActive = connectionItem == item.Setting;
		}

		private void AddDatabaseConnection()
		{
			var items = EffectiveDatabaseConnection.Items;
			var setting = new DatabaseConnectionSetting
			{
				Name = LocalizationManager.LocalizeStatic("db.new_connection"),
				ConnectorType = DatabaseConnectorType.SQLite,
				IsEnabled = true,
				IsActive = !items.Any(i => i.IsActive && i.IsEnabled)
			};
			items.Add(setting);
		}

		private void RemoveDatabaseConnection(DatabaseConnectionItemViewModel? item)
		{
			if (item is null)
				return;
			EffectiveDatabaseConnection.Items.Remove(item.Setting);
		}

		private void MoveDatabaseConnectionUp(DatabaseConnectionItemViewModel? item)
		{
			if (item is null)
				return;
			var items = EffectiveDatabaseConnection.Items;
			var index = items.IndexOf(item.Setting);
			if (index > 0)
				items.Move(index, index - 1);
		}

		private void MoveDatabaseConnectionDown(DatabaseConnectionItemViewModel? item)
		{
			if (item is null)
				return;
			var items = EffectiveDatabaseConnection.Items;
			var index = items.IndexOf(item.Setting);
			if (index < items.Count - 1)
				items.Move(index, index + 1);
		}

		private async Task BrowseSqliteFile(DatabaseConnectionItemViewModel? item)
		{
			if (item is null)
				return;

			var result = await App.MainTopLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
			{
				Title = LocalizationManager.LocalizeStatic("db.select_sqlite_file"),
				AllowMultiple = false,
				FileTypeFilter = [new FilePickerFileType("SQLite") { Patterns = ["*.db", "*.sqlite", "*.sqlite3"] }]
			});

			if (result.Count > 0)
				item.Setting.RawConnectionString = $"Data Source={result[0].Path.LocalPath}";
		}

		private async Task ConnectCustomAsync(CancellationToken cancellationToken)
		{
			IsCustomTesting = true;
			CustomTestResult = null;
			_customTestResultMessage = null;
			try
			{
				var connection = EffectiveDatabaseConnection;
				if (string.IsNullOrWhiteSpace(connection.CustomConnectionString))
					throw new InvalidOperationException(LocalizationManager.LocalizeStatic("db.test.empty_connection_string"));

				_connectionManager.Activate(connection.CustomConnectionString, connection.CustomConnectorType);
				await _connectionManager.OpenCurrentAsync(cancellationToken);
				CustomTestResult = true;
			}
			catch (OperationCanceledException)
			{
				_customTestResultMessage = null;
				CustomTestResult = null;
			}
			catch (Exception ex)
			{
				_customTestResultMessage = ex.Message;
				CustomTestResult = false;
			}
			finally
			{
				IsCustomTesting = false;
			}
		}

		private async Task TestCustomConnectionAsync(CancellationToken cancellationToken)
		{
			IsCustomTesting = true;
			CustomTestResult = null;
			_customTestResultMessage = null;
			try
			{
				var connection = EffectiveDatabaseConnection;
				if (string.IsNullOrWhiteSpace(connection.CustomConnectionString))
					throw new InvalidOperationException(LocalizationManager.LocalizeStatic("db.test.empty_connection_string"));

				await _cache.TestAsync(connection.CustomConnectorType, connection.CustomConnectionString, cancellationToken);
				CustomTestResult = true;
			}
			catch (OperationCanceledException)
			{
				_customTestResultMessage = null;
				CustomTestResult = null;
			}
			catch (Exception ex)
			{
				_customTestResultMessage = ex.Message;
				CustomTestResult = false;
			}
			finally
			{
				IsCustomTesting = false;
			}
		}

		private async Task DisconnectManagerItemAsync(DatabaseConnectionManagerItem? item, CancellationToken cancellationToken)
		{
			if (item is null)
				return;

			item.SetBusy(true);
			try
			{
				await _cache.DisconnectAsync(item.ConnectorType, item.ConnectionString);
			}
			finally
			{
				item.SetBusy(false);
			}
		}

		private async Task DisconnectAllAsync()
		{
			await _connectionManager.DisconnectAllAsync();
		}

		/// <inheritdoc/>
		protected override void Dispose(bool disposing)
		{
			base.Dispose(disposing);

			if (disposing)
			{
				DatabaseSettings.PropertyChanged -= DatabaseSettings_PropertyChanged;
				_cache.ConnectionsChanged -= Cache_ConnectionsChanged;
				if (_subscribedEffectiveConnection is not null)
					_subscribedEffectiveConnection.PropertyChanged -= EffectiveConnection_PropertyChanged;
				foreach (var setting in _subscribedSettings)
					setting.PropertyChanged -= Setting_PropertyChanged;
				_subscribedSettings.Clear();
				foreach (var item in ConnectionItems)
					item.Dispose();
				foreach (var item in ManagerItems)
					item.Dispose();
			}
		}
	}

	/// <summary>
	/// Represents a single connection that is currently open in the
	/// <see cref="IDatabaseConnectionCache"/>. When the connection string matches a
	/// configured connection, its name is shown; otherwise the item is displayed
	/// as an anonymous custom connection.
	/// </summary>
	public class DatabaseConnectionManagerItem : ViewModelBase
	{
		private bool _isBusy;

		/// <summary>
		/// Initializes a new instance of the <see cref="DatabaseConnectionManagerItem"/> class.
		/// </summary>
		/// <param name="displayName">The display name of the connection.</param>
		/// <param name="connectorType">The connector type of the connection.</param>
		/// <param name="connectionString">The connection string of the open connection.</param>
		/// <param name="isAnonymous">Whether the connection is anonymous (no matching configured connection).</param>
		public DatabaseConnectionManagerItem(string displayName, DatabaseConnectorType connectorType,
			string connectionString, bool isAnonymous)
		{
			DisplayName = displayName;
			ConnectorType = connectorType;
			ConnectionString = connectionString;
			IsAnonymous = isAnonymous;
		}

		/// <summary>
		/// Gets the display name of the connection.
		/// </summary>
		public string DisplayName { get; }

		/// <summary>
		/// Gets the display name of the connector type.
		/// </summary>
		public string TypeDisplayName => ConnectorType.ToString();

		/// <summary>
		/// Gets the connector type of the connection.
		/// </summary>
		public DatabaseConnectorType ConnectorType { get; }

		/// <summary>
		/// Gets the connection string of the open connection.
		/// </summary>
		public string ConnectionString { get; }

		/// <summary>
		/// Gets a value indicating whether the connection is anonymous
		/// (no matching configured connection was found).
		/// </summary>
		public bool IsAnonymous { get; }

		/// <summary>
		/// Gets the truncated connection string shown for anonymous connections.
		/// </summary>
		public string? Subtitle => IsAnonymous
			? (ConnectionString.Length > 100 ? ConnectionString[..100] + "..." : ConnectionString)
			: null;

		/// <summary>
		/// Gets a value indicating whether a disconnect operation is in progress.
		/// </summary>
		public bool IsBusy
		{
			get => _isBusy;
			private set => SetProperty(ref _isBusy, value);
		}

		/// <summary>
		/// Sets the busy state of the item.
		/// </summary>
		/// <param name="value">The new busy state.</param>
		public void SetBusy(bool value) => IsBusy = value;
	}
}
