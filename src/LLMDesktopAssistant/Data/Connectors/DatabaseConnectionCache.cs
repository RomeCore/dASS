using System.Collections.Immutable;
using LLMDesktopAssistant.Services;
using LLMDesktopAssistant.Utils;

namespace LLMDesktopAssistant.Data.Connectors
{
	/// <summary>
	/// Default implementation of <see cref="IDatabaseConnectionCache"/> backed by an
	/// <see cref="AsyncCache{TKey, TValue}"/> keyed by the connector type and the
	/// connection string.
	/// </summary>
	/// <remarks>
	/// Every cached connection is wrapped into a <see cref="SerializedConnection"/>: the same underlying
	/// connection is shared by several chats, and ADO.NET connections are not safe for concurrent commands.
	/// Leases only count users; they never close a connection that others still hold.
	/// </remarks>
	[Service(typeof(IDatabaseConnectionCache))]
	public class DatabaseConnectionCache : IDatabaseConnectionCache
	{
		private readonly ImmutableList<IDatabaseConnector> _connectors;
		private readonly ImmutableArray<DatabaseConnectorType> _supportedConnectors;
		private readonly AsyncCache<(DatabaseConnectorType Type, string ConnectionString), ConnectionEntry> _cache;

		/// <summary>
		/// Initializes a new instance of the <see cref="DatabaseConnectionCache"/> class.
		/// </summary>
		/// <param name="connectors">The registered database connectors.</param>
		public DatabaseConnectionCache(IEnumerable<IDatabaseConnector> connectors)
		{
			_connectors = connectors.ToImmutableList();
			_supportedConnectors = _connectors.Select(c => c.Type).Distinct().OrderBy(t => (int)t).ToImmutableArray();
			_cache = new AsyncCache<(DatabaseConnectorType, string), ConnectionEntry>(CreateEntryAsync,
				slidingExpirationTime: TimeSpan.FromHours(1),
				cleanupInterval: TimeSpan.FromMinutes(10));
			_cache.ItemRemoved += (_, _) => ConnectionsChanged?.Invoke(this, EventArgs.Empty);
		}

		/// <inheritdoc/>
		public event EventHandler? ConnectionsChanged;

		/// <inheritdoc/>
		public IEnumerable<DatabaseConnectorType> SupportedConnectors => _supportedConnectors;

		/// <inheritdoc/>
		public IReadOnlyCollection<(DatabaseConnectorType Type, string ConnectionString)> ActiveConnections =>
			_cache.Keys.ToImmutableArray();

		/// <inheritdoc/>
		public async Task<IDatabaseConnectionLease> AcquireAsync(
			DatabaseConnectorType type,
			string connectionString,
			CancellationToken cancellationToken = default)
		{
			var key = (type, connectionString);
			var entry = await _cache.GetAsync(key, cancellationToken);
			entry.AddReference();
			ConnectionsChanged?.Invoke(this, EventArgs.Empty);
			return new Lease(this, key, entry);
		}

		/// <inheritdoc/>
		public async Task TestAsync(DatabaseConnectorType type, string connectionString, CancellationToken cancellationToken = default)
		{
			var key = (type, connectionString);
			if (_cache.TryGet(key, out _))
				return;

			var connector = GetConnector(type);
			await using var connection = await connector.ConnectAsync(connectionString, cancellationToken);
			await connection.TestConnectionAsync(cancellationToken);
		}

		/// <inheritdoc/>
		public async Task DisconnectAsync(DatabaseConnectorType type, string connectionString)
		{
			await _cache.TryRemoveAsync((type, connectionString));
			ConnectionsChanged?.Invoke(this, EventArgs.Empty);
		}

		/// <inheritdoc/>
		public async Task DisconnectAllAsync()
		{
			foreach (var key in _cache.Keys)
				await _cache.TryRemoveAsync(key);
			ConnectionsChanged?.Invoke(this, EventArgs.Empty);
		}

		/// <inheritdoc/>
		public async ValueTask DisposeAsync()
		{
			await DisconnectAllAsync();
			_cache.Dispose();
		}

		private IDatabaseConnector GetConnector(DatabaseConnectorType type)
		{
			return _connectors.FirstOrDefault(c => c.Type == type)
				?? throw new NotSupportedException($"No connector registered for database type '{type}'.");
		}

		private async Task<ConnectionEntry> CreateEntryAsync(
			(DatabaseConnectorType Type, string ConnectionString) key,
			CancellationToken cancellationToken)
		{
			var connection = await GetConnector(key.Type).ConnectAsync(key.ConnectionString, cancellationToken);
			return new ConnectionEntry(new SerializedConnection(connection));
		}

		/// <summary>
		/// One cached connection plus the number of leases currently using it.
		/// </summary>
		private sealed class ConnectionEntry(IDatabaseConnection connection) : IAsyncDisposable
		{
			private int _references;
			private bool _disposed;

			/// <summary>
			/// Gets the connection shared by every lease of this entry.
			/// </summary>
			public IDatabaseConnection Connection { get; } = connection;

			/// <summary>
			/// Registers one more lease as a user of the connection.
			/// </summary>
			public void AddReference() => Interlocked.Increment(ref _references);

			/// <summary>
			/// Drops one lease.
			/// </summary>
			/// <returns>The number of leases still holding the connection.</returns>
			public int Release() => Interlocked.Decrement(ref _references);

			/// <inheritdoc/>
			public async ValueTask DisposeAsync()
			{
				if (_disposed)
					return;

				_disposed = true;
				await Connection.DisposeAsync();
			}
		}

		/// <summary>
		/// A lease handed out by the cache; the only place that decides whether a close is allowed.
		/// </summary>
		private sealed class Lease(
			DatabaseConnectionCache cache,
			(DatabaseConnectorType Type, string ConnectionString) key,
			ConnectionEntry entry) : IDatabaseConnectionLease
		{
			private ConnectionEntry _entry = entry;
			private bool _closed;

			/// <inheritdoc/>
			public DatabaseConnectorType Type => key.Type;

			/// <inheritdoc/>
			public string ConnectionString => key.ConnectionString;

			/// <inheritdoc/>
			public async Task<IDatabaseConnection> EnsureAsync(CancellationToken cancellationToken = default)
			{
				ThrowIfClosed();

				var current = await cache._cache.GetAsync(key, cancellationToken);
				if (!ReferenceEquals(current, _entry))
				{
					// The connection was closed or expired while we held it: claim the new one and
					// let go of the old, so a forced close costs a reconnect instead of a failure.
					current.AddReference();
					var previous = _entry;
					_entry = current;
					previous.Release();
				}

				return _entry.Connection;
			}

			/// <inheritdoc/>
			public Task TestAsync(CancellationToken cancellationToken = default)
			{
				ThrowIfClosed();
				return cache.TestAsync(key.Type, key.ConnectionString, cancellationToken);
			}

			/// <inheritdoc/>
			public async Task<bool> CloseAsync(bool force, CancellationToken cancellationToken = default)
			{
				if (_closed)
					return false;

				_closed = true;
				var remaining = _entry.Release();

				if (!force && remaining > 0)
					return false;

				await cache._cache.TryRemoveAsync(key);
				ConnectionsChanged();
				return true;

				void ConnectionsChanged() => cache.ConnectionsChanged?.Invoke(cache, EventArgs.Empty);
			}

			/// <inheritdoc/>
			public ValueTask DisposeAsync()
			{
				if (!_closed)
				{
					_closed = true;
					_entry.Release();
				}

				return default;
			}

			private void ThrowIfClosed()
			{
				if (_closed)
					throw new InvalidOperationException("The database connection lease is closed.");
			}
		}

		/// <summary>
		/// Serializes commands on one cached connection: two chats share it, and ADO.NET connections
		/// do not allow concurrent commands.
		/// </summary>
		private sealed class SerializedConnection(IDatabaseConnection inner) : IDatabaseConnection
		{
			private readonly SemaphoreSlim _gate = new(1, 1);

			/// <inheritdoc/>
			public DatabaseConnectorType Type => inner.Type;

			/// <inheritdoc/>
			public async Task OpenAsync(CancellationToken cancellationToken = default)
			{
				await _gate.WaitAsync(cancellationToken);
				try
				{
					await inner.OpenAsync(cancellationToken);
				}
				finally
				{
					_gate.Release();
				}
			}

			/// <inheritdoc/>
			public async Task TestConnectionAsync(CancellationToken cancellationToken = default)
			{
				await _gate.WaitAsync(cancellationToken);
				try
				{
					await inner.TestConnectionAsync(cancellationToken);
				}
				finally
				{
					_gate.Release();
				}
			}

			/// <inheritdoc/>
			public async Task<DatabaseQueryResult> ExecuteQueryAsync(
				string query,
				IReadOnlyList<object?>? parameters = null,
				int? maxRows = null,
				CancellationToken cancellationToken = default)
			{
				await _gate.WaitAsync(cancellationToken);
				try
				{
					return await inner.ExecuteQueryAsync(query, parameters, maxRows, cancellationToken);
				}
				finally
				{
					_gate.Release();
				}
			}

			/// <inheritdoc/>
			public async Task<string> GetSchemaAsync(CancellationToken cancellationToken = default)
			{
				await _gate.WaitAsync(cancellationToken);
				try
				{
					return await inner.GetSchemaAsync(cancellationToken);
				}
				finally
				{
					_gate.Release();
				}
			}

			/// <inheritdoc/>
			public async ValueTask DisposeAsync()
			{
				await inner.DisposeAsync();
				_gate.Dispose();
			}
		}
	}
}
