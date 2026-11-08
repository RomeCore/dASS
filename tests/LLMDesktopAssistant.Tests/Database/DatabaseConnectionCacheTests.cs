using LLMDesktopAssistant.Data.Connectors;

namespace LLMDesktopAssistant.Tests.Database
{
	public class DatabaseConnectionCacheTests
	{
		private const string connectionString = "Data Source=:memory:";
		private static readonly (DatabaseConnectorType Type, string ConnectionString) key =
			(DatabaseConnectorType.SQLite, connectionString);

		private static DatabaseConnectionCache CreateCache()
			=> new([new SqliteDatabaseConnector()]);

		[Fact]
		public async Task Acquire_ReportsTheConnectionAsActive()
		{
			await using var cache = CreateCache();
			await using var lease = await cache.AcquireAsync(DatabaseConnectorType.SQLite, connectionString);

			Assert.Contains(key, cache.ActiveConnections);
			Assert.Equal(DatabaseConnectorType.SQLite, lease.Type);
			Assert.Equal(connectionString, lease.ConnectionString);
		}

		[Fact]
		public async Task Acquire_SameKey_SharesOneConnection()
		{
			await using var cache = CreateCache();
			await using var first = await cache.AcquireAsync(DatabaseConnectorType.SQLite, connectionString);
			await using var second = await cache.AcquireAsync(DatabaseConnectorType.SQLite, connectionString);

			Assert.Same(await first.EnsureAsync(), await second.EnsureAsync());
		}

		[Fact]
		public async Task Close_WhenAnotherLeaseHolds_KeepsTheConnection()
		{
			await using var cache = CreateCache();
			await using var first = await cache.AcquireAsync(DatabaseConnectorType.SQLite, connectionString);
			await using var second = await cache.AcquireAsync(DatabaseConnectorType.SQLite, connectionString);

			Assert.False(await first.CloseAsync(force: false));
			Assert.Contains(key, cache.ActiveConnections);

			var connection = await second.EnsureAsync();
			Assert.Single((await connection.ExecuteQueryAsync("SELECT 1 AS one")).Rows);
		}

		[Fact]
		public async Task Close_WhenLastLease_ClosesTheConnection()
		{
			await using var cache = CreateCache();
			var lease = await cache.AcquireAsync(DatabaseConnectorType.SQLite, connectionString);

			Assert.True(await lease.CloseAsync(force: false));
			Assert.DoesNotContain(key, cache.ActiveConnections);
		}

		[Fact]
		public async Task Close_Force_ClosesEvenWhenShared()
		{
			await using var cache = CreateCache();
			await using var first = await cache.AcquireAsync(DatabaseConnectorType.SQLite, connectionString);
			await using var second = await cache.AcquireAsync(DatabaseConnectorType.SQLite, connectionString);
			var shared = await second.EnsureAsync();

			Assert.True(await first.CloseAsync(force: true));
			Assert.DoesNotContain(key, cache.ActiveConnections);

			// A forced close costs the other lease a reconnect, not a failure.
			var reopened = await second.EnsureAsync();
			Assert.NotSame(shared, reopened);
			Assert.Single((await reopened.ExecuteQueryAsync("SELECT 1 AS one")).Rows);
		}

		[Fact]
		public async Task Release_WithoutClose_LeavesTheConnectionCached()
		{
			await using var cache = CreateCache();

			var lease = await cache.AcquireAsync(DatabaseConnectorType.SQLite, connectionString);
			await lease.DisposeAsync();

			Assert.Contains(key, cache.ActiveConnections);
		}

		[Fact]
		public async Task ClosedLease_RefusesToEnsure()
		{
			await using var cache = CreateCache();
			var lease = await cache.AcquireAsync(DatabaseConnectorType.SQLite, connectionString);
			await lease.CloseAsync(force: true);

			await Assert.ThrowsAsync<InvalidOperationException>(() => lease.EnsureAsync());
		}
	}
}
