using System.Globalization;
using System.Text;
using LLMDesktopAssistant.Services;
using Microsoft.Data.Sqlite;

namespace LLMDesktopAssistant.Data.Connectors
{
	/// <summary>
	/// A connector that creates connections to SQLite databases.
	/// </summary>
	[Service(typeof(IDatabaseConnector))]
	public class SqliteDatabaseConnector : IDatabaseConnector
	{
		/// <inheritdoc/>
		public DatabaseConnectorType Type => DatabaseConnectorType.SQLite;

		/// <inheritdoc/>
		public Task<IDatabaseConnection> ConnectAsync(string connectionString, CancellationToken cancellationToken = default)
		{
			var connection = new SqliteConnection(connectionString);
			return Task.FromResult<IDatabaseConnection>(new SqliteDatabaseConnection(connection));
		}

		private sealed class SqliteDatabaseConnection : IDatabaseConnection
		{
			private readonly SqliteConnection _connection;
			private bool _opened;

			public SqliteDatabaseConnection(SqliteConnection connection)
			{
				_connection = connection;
			}

			/// <inheritdoc/>
			public DatabaseConnectorType Type => DatabaseConnectorType.SQLite;

			/// <inheritdoc/>
			public Task OpenAsync(CancellationToken cancellationToken = default)
				=> OpenIfNeededAsync(cancellationToken);

			/// <inheritdoc/>
			public async Task TestConnectionAsync(CancellationToken cancellationToken = default)
			{
				await _connection.OpenAsync(cancellationToken);
				await _connection.CloseAsync();
				SqliteConnection.ClearPool(_connection);
			}

			/// <inheritdoc/>
			public async Task<DatabaseQueryResult> ExecuteQueryAsync(
				string query,
				IReadOnlyList<object?>? parameters = null,
				int? maxRows = null,
				CancellationToken cancellationToken = default)
			{
				await OpenIfNeededAsync(cancellationToken);

				await using var command = _connection.CreateCommand();
				command.CommandText = query;

				if (parameters is { Count: > 0 })
				{
					for (int i = 0; i < parameters.Count; i++)
						command.Parameters.AddWithValue($"@p{i}", parameters[i] ?? DBNull.Value);
				}

				await using var reader = await command.ExecuteReaderAsync(cancellationToken);

				var columns = new string[reader.FieldCount];
				for (int i = 0; i < columns.Length; i++)
					columns[i] = reader.GetName(i);

				if (columns.Length == 0)
					return new DatabaseQueryResult { Columns = [], Rows = [], RowsAffected = reader.RecordsAffected };

				var rows = new List<string?[]>();
				bool truncated = false;
				while (await reader.ReadAsync(cancellationToken))
				{
					if (maxRows is { } limit && limit > 0 && rows.Count >= limit)
					{
						truncated = true;
						break;
					}

					var row = new string?[columns.Length];
					for (int i = 0; i < columns.Length; i++)
						row[i] = reader.IsDBNull(i)
							? null
							: Convert.ToString(reader.GetValue(i), CultureInfo.InvariantCulture);
					rows.Add(row);
				}

				return new DatabaseQueryResult { Columns = columns, Rows = rows.ToArray(), RowsAffected = rows.Count, Truncated = truncated };
			}

			/// <inheritdoc/>
			public async Task<string> GetSchemaAsync(CancellationToken cancellationToken = default)
			{
				await OpenIfNeededAsync(cancellationToken);

				var sb = new StringBuilder();
				sb.AppendLine("# Database schema");

				var objects = new List<(string Kind, string Name)>();
				await using (var command = _connection.CreateCommand())
				{
					command.CommandText = """
						SELECT type, name FROM sqlite_master
						WHERE type IN ('table', 'view') AND name NOT LIKE 'sqlite_%'
						ORDER BY name
						""";
					await using var reader = await command.ExecuteReaderAsync(cancellationToken);
					while (await reader.ReadAsync(cancellationToken))
						objects.Add((reader.GetString(0), reader.GetString(1)));
				}

				if (objects.Count == 0)
				{
					sb.AppendLine("No tables or views found.");
					return sb.ToString();
				}

				foreach (var (kind, name) in objects)
				{
					sb.AppendLine();
					sb.AppendLine($"## {name} ({kind})");

					if (kind == "view")
					{
						await using var viewCommand = _connection.CreateCommand();
						viewCommand.CommandText = "SELECT sql FROM sqlite_master WHERE name = $name";
						viewCommand.Parameters.AddWithValue("$name", name);
						var sql = await viewCommand.ExecuteScalarAsync(cancellationToken) as string;
						sb.AppendLine("```sql");
						sb.AppendLine(sql ?? "unknown");
						sb.AppendLine("```");
						continue;
					}

					// PRAGMA does not support parameters in Microsoft.Data.Sqlite,
					// so the identifier is interpolated with escaped double quotes.
					await using var columnCommand = _connection.CreateCommand();
					columnCommand.CommandText = $"PRAGMA table_info(\"{name.Replace("\"", "\"\"")}\")";
					await using var reader = await columnCommand.ExecuteReaderAsync(cancellationToken);

					sb.AppendLine("| Column | Type | NotNull | Default | PK |");
					sb.AppendLine("|---|---|---|---|---|");
					while (await reader.ReadAsync(cancellationToken))
					{
						sb.AppendLine($"| {reader.GetString(1)} | {reader.GetString(2)} | {reader.GetInt32(3)} | {reader.GetValue(4)} | {reader.GetInt32(5)} |");
					}
				}

				return sb.ToString();
			}

			/// <inheritdoc/>
			public async ValueTask DisposeAsync()
			{
				await _connection.CloseAsync();
				await _connection.DisposeAsync();
				SqliteConnection.ClearPool(_connection);
			}

			private async Task OpenIfNeededAsync(CancellationToken cancellationToken)
			{
				if (!_opened)
				{
					await _connection.OpenAsync(cancellationToken);
					_opened = true;
				}
			}
		}
	}
}
