using LLMDesktopAssistant.Data.Connectors;

namespace LLMDesktopAssistant.Tests.Database
{
	public class DatabaseConnectorTests
	{
		private static async Task<IDatabaseConnection> CreateConnectionAsync()
		{
			var connector = new SqliteDatabaseConnector();
			return await connector.ConnectAsync("Data Source=:memory:");
		}

		[Fact]
		public async Task ExecuteQuery_Select_ReturnsColumnsAndRows()
		{
			await using var connection = await CreateConnectionAsync();
			await connection.ExecuteQueryAsync("CREATE TABLE users (id INTEGER PRIMARY KEY, name TEXT NOT NULL)");
			await connection.ExecuteQueryAsync("INSERT INTO users (name) VALUES ('Alice'), ('Bob')");

			var result = await connection.ExecuteQueryAsync("SELECT id, name FROM users ORDER BY id");

			Assert.Equal(new[] { "id", "name" }, result.Columns);
			Assert.Equal(2, result.Rows.Length);
			Assert.Equal(new[] { "1", "Alice" }, result.Rows[0]);
			Assert.Equal(new[] { "2", "Bob" }, result.Rows[1]);
			Assert.False(result.Truncated);
		}

		[Fact]
		public async Task ExecuteQuery_Modification_ReturnsRowsAffected()
		{
			await using var connection = await CreateConnectionAsync();
			await connection.ExecuteQueryAsync("CREATE TABLE users (id INTEGER PRIMARY KEY, name TEXT NOT NULL)");

			var insert = await connection.ExecuteQueryAsync("INSERT INTO users (name) VALUES ('Alice'), ('Bob'), ('Carol')");
			Assert.Empty(insert.Columns);
			Assert.Equal(3, insert.RowsAffected);

			var update = await connection.ExecuteQueryAsync("UPDATE users SET name = 'A' WHERE id = 1");
			Assert.Equal(1, update.RowsAffected);
		}

		[Fact]
		public async Task ExecuteQuery_Select_NullValueIsNullNotTheText()
		{
			await using var connection = await CreateConnectionAsync();
			await connection.ExecuteQueryAsync("CREATE TABLE items (id INTEGER PRIMARY KEY, note TEXT)");
			await connection.ExecuteQueryAsync("INSERT INTO items (note) VALUES (NULL), ('NULL')");

			var result = await connection.ExecuteQueryAsync("SELECT id, note FROM items ORDER BY id");

			Assert.Equal(new string?[] { "1", null }, result.Rows[0]);
			Assert.Equal(new string?[] { "2", "NULL" }, result.Rows[1]);
		}

		[Fact]
		public async Task ExecuteQuery_Parameters_AreBoundInOrder()
		{
			await using var connection = await CreateConnectionAsync();
			await connection.ExecuteQueryAsync("CREATE TABLE users (id INTEGER PRIMARY KEY, name TEXT NOT NULL)");
			await connection.ExecuteQueryAsync("INSERT INTO users (name) VALUES ('Alice'), ('Bob')");

			var result = await connection.ExecuteQueryAsync("SELECT name FROM users WHERE id = @p0", [(object?)2L]);
			Assert.Equal("Bob", result.Rows[0][0]);

			var byName = await connection.ExecuteQueryAsync("SELECT id FROM users WHERE name = @p0", [(object?)"Alice"]);
			Assert.Equal("1", byName.Rows[0][0]);
		}

		[Fact]
		public async Task ExecuteQuery_NullParameter_IsSentAsNull()
		{
			await using var connection = await CreateConnectionAsync();
			await connection.ExecuteQueryAsync("CREATE TABLE items (id INTEGER PRIMARY KEY, note TEXT)");
			await connection.ExecuteQueryAsync("INSERT INTO items (note) VALUES (NULL), ('x')");

			var result = await connection.ExecuteQueryAsync("SELECT id FROM items WHERE note IS @p0", [(object?)null]);

			Assert.Single(result.Rows);
			Assert.Equal("1", result.Rows[0][0]);
		}

		[Fact]
		public async Task ExecuteQuery_NoRowLimit_ReturnsEverything()
		{
			await using var connection = await CreateConnectionAsync();
			await InsertNumbersAsync(connection, 700);

			var result = await connection.ExecuteQueryAsync("SELECT n FROM numbers");

			Assert.Equal(700, result.Rows.Length);
			Assert.False(result.Truncated);
		}

		[Fact]
		public async Task ExecuteQuery_RowLimit_TruncatesAndReports()
		{
			await using var connection = await CreateConnectionAsync();
			await InsertNumbersAsync(connection, 700);

			var result = await connection.ExecuteQueryAsync("SELECT n FROM numbers", maxRows: 500);

			Assert.True(result.Truncated);
			Assert.Equal(500, result.Rows.Length);
		}

		[Fact]
		public async Task ExecuteQuery_RowLimitLargerThanResult_IsNotTruncated()
		{
			await using var connection = await CreateConnectionAsync();
			await InsertNumbersAsync(connection, 10);

			var result = await connection.ExecuteQueryAsync("SELECT n FROM numbers", maxRows: 500);

			Assert.False(result.Truncated);
			Assert.Equal(10, result.Rows.Length);
		}

		[Fact]
		public async Task Open_MissingDirectory_Throws()
		{
			var connector = new SqliteDatabaseConnector();
			var missing = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString(), "nope.db");
			await using var connection = await connector.ConnectAsync($"Data Source={missing}");

			await Assert.ThrowsAnyAsync<Exception>(() => connection.OpenAsync());
		}

		[Fact]
		public async Task Open_ThenQuery_Succeeds()
		{
			await using var connection = await CreateConnectionAsync();

			await connection.OpenAsync();

			Assert.Single((await connection.ExecuteQueryAsync("SELECT 1 AS one")).Rows);
		}

		[Fact]
		public async Task GetSchema_ReturnsTablesAndColumns()
		{
			await using var connection = await CreateConnectionAsync();
			await connection.ExecuteQueryAsync("CREATE TABLE users (id INTEGER PRIMARY KEY, name TEXT NOT NULL)");
			await connection.ExecuteQueryAsync("CREATE VIEW user_names AS SELECT name FROM users");

			var schema = await connection.GetSchemaAsync();

			Assert.Contains("## users (table)", schema);
			Assert.Contains("| id | INTEGER |", schema);
			Assert.Contains("## user_names (view)", schema);
		}

		[Fact]
		public async Task GetSchema_EmptyDatabase_ReturnsNoTablesMessage()
		{
			await using var connection = await CreateConnectionAsync();

			var schema = await connection.GetSchemaAsync();

			Assert.Contains("No tables or views found.", schema);
		}

		private static async Task InsertNumbersAsync(IDatabaseConnection connection, int count)
		{
			await connection.ExecuteQueryAsync("CREATE TABLE numbers (n INTEGER)");

			for (int batch = 0; batch < count; batch += 100)
			{
				var size = Math.Min(100, count - batch);
				var values = string.Join(", ", Enumerable.Range(batch + 1, size).Select(i => $"({i})"));
				await connection.ExecuteQueryAsync($"INSERT INTO numbers VALUES {values}");
			}
		}
	}
}
