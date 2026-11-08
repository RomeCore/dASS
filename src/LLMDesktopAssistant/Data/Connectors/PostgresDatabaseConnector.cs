using System.Globalization;
using System.Text;
using LLMDesktopAssistant.Services;
using Npgsql;

namespace LLMDesktopAssistant.Data.Connectors
{
	/// <summary>
	/// A connector that creates connections to PostgreSQL databases.
	/// </summary>
	[Service(typeof(IDatabaseConnector))]
	public class PostgresDatabaseConnector : IDatabaseConnector
	{
		/// <inheritdoc/>
		public DatabaseConnectorType Type => DatabaseConnectorType.PostgreSQL;

		/// <inheritdoc/>
		public Task<IDatabaseConnection> ConnectAsync(string connectionString, CancellationToken cancellationToken = default)
		{
			var connection = new NpgsqlConnection(connectionString);
			return Task.FromResult<IDatabaseConnection>(new PostgresDatabaseConnection(connection));
		}

		private sealed class PostgresDatabaseConnection : IDatabaseConnection
		{
			private readonly NpgsqlConnection _connection;
			private bool _opened;

			public PostgresDatabaseConnection(NpgsqlConnection connection)
			{
				_connection = connection;
			}

			/// <inheritdoc/>
			public DatabaseConnectorType Type => DatabaseConnectorType.PostgreSQL;

			/// <inheritdoc/>
			public Task OpenAsync(CancellationToken cancellationToken = default)
				=> OpenIfNeededAsync(cancellationToken);

			/// <inheritdoc/>
			public async Task TestConnectionAsync(CancellationToken cancellationToken = default)
			{
				await _connection.OpenAsync(cancellationToken);
				await _connection.CloseAsync();
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
						command.Parameters.Add(new NpgsqlParameter($"p{i}", parameters[i] ?? DBNull.Value));
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
						SELECT table_type, table_name
						FROM information_schema.tables
						WHERE table_schema NOT IN ('pg_catalog', 'information_schema')
						ORDER BY table_name
						""";
					await using var reader = await command.ExecuteReaderAsync(cancellationToken);
					while (await reader.ReadAsync(cancellationToken))
					{
						var kind = reader.GetString(0) == "VIEW" ? "view" : "table";
						objects.Add((kind, reader.GetString(1)));
					}
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
						viewCommand.CommandText = """
							SELECT view_definition
							FROM information_schema.views
							WHERE table_schema NOT IN ('pg_catalog', 'information_schema') AND table_name = @name
							""";
						viewCommand.Parameters.AddWithValue("name", name);
						var sql = await viewCommand.ExecuteScalarAsync(cancellationToken) as string;
						sb.AppendLine("```sql");
						sb.AppendLine(sql ?? "unknown");
						sb.AppendLine("```");
						continue;
					}

					await using var columnCommand = _connection.CreateCommand();
					columnCommand.CommandText = """
						SELECT DISTINCT c.column_name, c.data_type, c.is_nullable, c.column_default,
						       CASE WHEN tc.constraint_type = 'PRIMARY KEY' THEN 1 ELSE 0 END
						FROM information_schema.columns c
						LEFT JOIN information_schema.key_column_usage kcu
						       ON kcu.table_schema = c.table_schema
						      AND kcu.table_name = c.table_name
						      AND kcu.column_name = c.column_name
						LEFT JOIN information_schema.table_constraints tc
						       ON tc.constraint_schema = kcu.constraint_schema
						      AND tc.constraint_name = kcu.constraint_name
						      AND tc.table_schema = kcu.table_schema
						      AND tc.table_name = kcu.table_name
						      AND tc.constraint_type = 'PRIMARY KEY'
						WHERE c.table_schema NOT IN ('pg_catalog', 'information_schema')
						  AND c.table_name = @name
						ORDER BY c.column_name
						""";
					columnCommand.Parameters.AddWithValue("name", name);
					await using var reader = await columnCommand.ExecuteReaderAsync(cancellationToken);

					sb.AppendLine("| Column | Type | NotNull | Default | PK |");
					sb.AppendLine("|---|---|---|---|---|");
					while (await reader.ReadAsync(cancellationToken))
					{
						sb.AppendLine($"| {reader.GetString(0)} | {reader.GetString(1)} | {reader.GetString(2)} | {reader.GetValue(3)} | {reader.GetInt32(4)} |");
					}
				}

				return sb.ToString();
			}

			/// <inheritdoc/>
			public async ValueTask DisposeAsync()
			{
				await _connection.CloseAsync();
				await _connection.DisposeAsync();
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
