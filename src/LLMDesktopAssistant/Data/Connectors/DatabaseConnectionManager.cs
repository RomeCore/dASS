using LLMDesktopAssistant.ApiKeys;
using LLMDesktopAssistant.LLM.Domain;
using LLMDesktopAssistant.LLM.Services;

namespace LLMDesktopAssistant.Data.Connectors
{
	/// <summary>
	/// Default implementation of <see cref="IDatabaseConnectionManager"/>. Switching the
	/// active database only updates the chat settings; the actual connections are opened
	/// lazily on first use and cached by the application-wide <see cref="IDatabaseConnectionCache"/>.
	/// </summary>
	[ChatService(typeof(IDatabaseConnectionManager))]
	public class DatabaseConnectionManager : IDatabaseConnectionManager
	{
		private readonly IChatSettingsService _chatSettings;
		private readonly IApiKeyManagerService _apiKeyManager;
		private readonly IDatabaseConnectionCache _cache;

		/// <summary>
		/// Initializes a new instance of the <see cref="DatabaseConnectionManager"/> class.
		/// </summary>
		/// <param name="chatSettings">The settings service of the chat.</param>
		/// <param name="apiKeyManager">The API key manager used to resolve encrypted connection strings.</param>
		/// <param name="cache">The application-wide cache of database connections.</param>
		public DatabaseConnectionManager(IChatSettingsService chatSettings, IApiKeyManagerService apiKeyManager, IDatabaseConnectionCache cache)
		{
			_chatSettings = chatSettings;
			_apiKeyManager = apiKeyManager;
			_cache = cache;
		}

		/// <inheritdoc/>
		public IEnumerable<DatabaseConnectorType> SupportedConnectors => _cache.SupportedConnectors;

		/// <inheritdoc/>
		public (DatabaseConnectorType Type, string ConnectionString, string Description)? ActiveConnection => ResolveActive();

		/// <inheritdoc/>
		public bool IsActiveConfigured
		{
			get
			{
				var active = ResolveActive();
				return active is not null && !string.IsNullOrWhiteSpace(active.Value.ConnectionString);
			}
		}

		/// <inheritdoc/>
		public string Activate(string nameOrConnectionString, DatabaseConnectorType? connectorType = null)
		{
			var settings = _chatSettings.Settings.Databases.GetEffectiveDatabaseConnection();
			var named = settings.Items.FirstOrDefault(i => i.IsEnabled &&
				string.Equals(i.Name, nameOrConnectionString, StringComparison.OrdinalIgnoreCase));

			if (named is not null)
			{
				settings.IsCustomActive = false;

				// Prevent activating multiple named connections with the same name.
				bool onceFlag = true;
				foreach (var item in settings.Items)
				{
					item.IsActive = item == named && onceFlag;
					if (item.IsActive)
						onceFlag = false;
				}

				return named.Name!;
			}

			settings.IsCustomActive = true;
			settings.CustomConnectionString = nameOrConnectionString;
			settings.CustomConnectorType = connectorType ?? DatabaseConnectorType.SQLite;
			foreach (var item in settings.Items)
				item.IsActive = false;

			return $"custom connection (string: {nameOrConnectionString}, type: {settings.CustomConnectorType})";
		}

		/// <inheritdoc/>
		public async Task<IDatabaseConnectionLease> AcquireCurrentAsync(CancellationToken cancellationToken = default)
		{
			var active = ResolveActive()
				?? throw new InvalidOperationException("No active database connection. Call db-switch first.");

			var (type, connectionString, _) = active;
			if (string.IsNullOrWhiteSpace(connectionString))
				throw new InvalidOperationException($"Connection string for '{active.Description}' is empty.");

			return await _cache.AcquireAsync(type, connectionString, cancellationToken);
		}

		/// <inheritdoc/>
		public async Task OpenCurrentAsync(CancellationToken cancellationToken = default)
		{
			var lease = await AcquireCurrentAsync(cancellationToken);

			try
			{
				await (await lease.EnsureAsync(cancellationToken)).OpenAsync(cancellationToken);
			}
			catch
			{
				// A connection that cannot be opened must not stay cached for everyone else.
				await lease.CloseAsync(force: true, CancellationToken.None);
				throw;
			}
			finally
			{
				await lease.DisposeAsync();
			}
		}

		/// <inheritdoc/>
		public async Task DisconnectAsync()
		{
			var active = ResolveActive();
			if (active is null || string.IsNullOrWhiteSpace(active.Value.ConnectionString))
				return;

			await _cache.DisconnectAsync(active.Value.Type, active.Value.ConnectionString);
		}

		/// <inheritdoc/>
		public Task DisconnectAllAsync()
		{
			return _cache.DisconnectAllAsync();
		}

		private (DatabaseConnectorType Type, string ConnectionString, string Description)? ResolveActive()
		{
			var settings = _chatSettings.Settings.Databases.GetEffectiveDatabaseConnection();

			if (settings.IsCustomActive && !string.IsNullOrWhiteSpace(settings.CustomConnectionString))
				return (settings.CustomConnectorType, settings.CustomConnectionString, $"custom ({settings.CustomConnectorType})");

			var named = settings.Items.FirstOrDefault(i => i.IsEnabled && i.IsActive);
			if (named is not null)
			{
				var connectionString = named.GetConnectionString(_apiKeyManager);
				if (!string.IsNullOrWhiteSpace(connectionString))
					return (named.ConnectorType, connectionString, named.Name!);
			}

			return null;
		}
	}
}
