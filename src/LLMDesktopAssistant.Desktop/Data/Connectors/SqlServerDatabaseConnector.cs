using System.Globalization;
using System.Text;
using LLMDesktopAssistant.Data.Connectors;
using LLMDesktopAssistant.Services;
using Microsoft.Data.SqlClient;

namespace LLMDesktopAssistant.Desktop.Data.Connectors
{
	/// <summary>
	/// A connector that creates connections to SQL Server databases.
	/// </summary>
	[Service(typeof(IDatabaseConnector))]
	public class SqlServerDatabaseConnector : IDatabaseConnector
	{
		/// <inheritdoc/>
		public DatabaseConnectorType Type => DatabaseConnectorType.SQLServer;

		/// <inheritdoc/>
		public Task<IDatabaseConnection> ConnectAsync(string connectionString, CancellationToken cancellationToken = default)
		{
			var connection = new SqlConnection(connectionString);
			return Task.FromResult<IDatabaseConnection>(new SqlServerDatabaseConnection(connection));
		}

		private sealed class SqlServerDatabaseConnection : IDatabaseConnection
		{
			private readonly SqlConnection _connection;
			private bool _opened;

			public SqlServerDatabaseConnection(SqlConnection connection)
			{
				_connection = connection;
			}

			/// <inheritdoc/>
			public DatabaseConnectorType Type => DatabaseConnectorType.SQLServer;

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
						command.Parameters.Add(new SqlParameter($"@p{i}", parameters[i] ?? DBNull.Value));
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

				var tables = new List<(string Name, string Type)>();
				var columns = new List<(string Table, string Name, string DataType, string IsNullable, string? Default)>();
				await using (var command = _connection.CreateCommand())
				{
					command.CommandText = """
						SELECT t.TABLE_NAME, t.TABLE_TYPE, c.COLUMN_NAME, c.DATA_TYPE, c.IS_NULLABLE, c.COLUMN_DEFAULT
						FROM INFORMATION_SCHEMA.TABLES t
						LEFT JOIN INFORMATION_SCHEMA.COLUMNS c
							ON t.TABLE_NAME = c.TABLE_NAME AND t.TABLE_SCHEMA = c.TABLE_SCHEMA
						WHERE t.TABLE_TYPE IN ('BASE TABLE', 'VIEW')
						ORDER BY t.TABLE_NAME, c.ORDINAL_POSITION
						""";
					await using var reader = await command.ExecuteReaderAsync(cancellationToken);
					while (await reader.ReadAsync(cancellationToken))
					{
						var tableName = reader.GetString(0);
						var tableType = reader.GetString(1);
						if (!tables.Any(t => t.Name == tableName))
							tables.Add((tableName, tableType));
						if (!reader.IsDBNull(2))
						{
							columns.Add((
								tableName,
								reader.GetString(2),
								reader.GetString(3),
								reader.GetString(4),
								reader.IsDBNull(5) ? null : reader.GetString(5)));
						}
					}
				}

				if (tables.Count == 0)
				{
					sb.AppendLine("No tables or views found.");
					return sb.ToString();
				}

				foreach (var (name, type) in tables)
				{
					sb.AppendLine();
					sb.AppendLine($"## {name} ({type})");
					sb.AppendLine("| Column | Type | Nullable | Default |");
					sb.AppendLine("|---|---|---|---|");

					var tableColumns = columns.Where(c => c.Table == name).ToList();
					if (tableColumns.Count == 0)
					{
						sb.AppendLine("| *(no columns)* | | | |");
						continue;
					}

					foreach (var column in tableColumns)
						sb.AppendLine($"| {column.Name} | {column.DataType} | {column.IsNullable} | {column.Default ?? "NULL"} |");
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
