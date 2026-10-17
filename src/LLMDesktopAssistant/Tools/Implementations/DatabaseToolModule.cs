using System.ComponentModel;
using System.Text;
using System.Text.Json.Nodes;
using LLMDesktopAssistant.Data.Connectors;
using LLMDesktopAssistant.LLM.Services;
using LLMDesktopAssistant.Localization;
using LLMDesktopAssistant.Tools.Execution.Domain;
using LLMDesktopAssistant.Tools.Execution.Support;
using Material.Icons;
using DomainPreviewResult = LLMDesktopAssistant.Tools.Execution.Domain.PreviewToolExecutionResult;

namespace LLMDesktopAssistant.Tools.Implementations
{
	/// <summary>
	/// Tool module providing database interaction tools: listing configured connections,
	/// connecting to a database, inspecting its schema and executing SQL statements.
	/// </summary>
	[ToolModule]
	public class DatabaseToolModule : ToolModule
	{
		private static readonly HashSet<string> ReadKeywords = new(StringComparer.OrdinalIgnoreCase)
		{
			"SELECT", "WITH", "PRAGMA", "EXPLAIN", "SHOW", "DESCRIBE"
		};

		private readonly IChatSettingsService _chatSettings;
		private readonly IDatabaseConnectionManager _connectionManager;

		/// <summary>
		/// Initializes a new instance of the <see cref="DatabaseToolModule"/> class.
		/// </summary>
		/// <param name="chatSettings">The settings service of the chat.</param>
		/// <param name="connectionManager">The manager of the active database connection of the chat.</param>
		public DatabaseToolModule(IChatSettingsService chatSettings, IDatabaseConnectionManager connectionManager)
		{
			_chatSettings = chatSettings;
			_connectionManager = connectionManager;

			AddTool(new ToolInitializationInfo
			{
				Executor = ListAsync,
				Name = "db-list",
				Description = """
					Lists the database connections configured in the chat settings (named connections and the custom one) and the currently active session connection.
					""",
				DefaultExpectedBehaviour = ToolBehaviour.None,
				NameKey = Locale.GetKey("tool.name.db-list"),
				DescriptionKey = Locale.GetKey("tool.description.db-list"),
				CategoryKey = Locale.GetKey("tool.category.database")
			});

			AddTool(new ToolInitializationInfo
			{
				Executor = ConnectAsync,
				PreviewExecutor = ConnectPreview,
				Name = "db-switch",
				Description = """
					Activates a database connection for the current chat session: a named connection from the chat settings (pass its name, see db-list) or a custom database by a raw connection string with the specified connector type. Only switches the active connection string; the real connection is opened lazily on the first use (db-schema or db-execute).
					""",
				DefaultExpectedBehaviour = ToolBehaviour.DatabaseCustomConnect,
				NameKey = Locale.GetKey("tool.name.db-switch"),
				DescriptionKey = Locale.GetKey("tool.description.db-switch"),
				CategoryKey = Locale.GetKey("tool.category.database"),
				ModifyArgumentSchema = schema =>
				{
					var properties = schema["properties"]!;

					var connectorType = properties["connectorType"]!;
					connectorType["enum"] = new JsonArray(_connectionManager.SupportedConnectors
						.Select(v => JsonValue.Create(v.ToString().ToLower())).ToArray());
				}
			});

			AddTool(new ToolInitializationInfo
			{
				Executor = GetSchemaAsync,
				Name = "db-schema",
				Description = """
					Shows the schema of the active database: tables, views and their columns.
					Call db-connect first if no connection is active.
					""",
				DefaultExpectedBehaviour = ToolBehaviour.DatabaseRead,
				NameKey = Locale.GetKey("tool.name.db-schema"),
				DescriptionKey = Locale.GetKey("tool.description.db-schema"),
				CategoryKey = Locale.GetKey("tool.category.database")
			});

			AddTool(new ToolInitializationInfo
			{
				Executor = ExecuteAsync,
				PreviewExecutor = ExecutePreview,
				Name = "db-execute",
				Description = """
					Executes a SQL statement against the active database and returns the result.
					SELECT-like statements return a markdown table of rows; modifying statements (INSERT, UPDATE, DELETE, DDL) return the number of affected rows.
					""",
				DefaultExpectedBehaviour = ToolBehaviour.DatabaseRead | ToolBehaviour.DatabaseChange,
				NameKey = Locale.GetKey("tool.name.db-execute"),
				DescriptionKey = Locale.GetKey("tool.description.db-execute"),
				CategoryKey = Locale.GetKey("tool.category.database")
			});
		}

		private Task<ToolExecutionResult> ListAsync(IToolExecutionContext ctx, CancellationToken cancellationToken = default)
		{
			var settings = _chatSettings.Settings.Databases.GetEffectiveDatabaseConnection();
			var sb = new StringBuilder();

			sb.AppendLine("### Named connections");
			var items = settings.Items.Where(i => i.IsEnabled).ToList();
			if (items.Count == 0)
				sb.AppendLine("None configured.");
			else
			{
				foreach (var item in items)
					sb.AppendLine($"- **{item.Name}** ({item.ConnectorType}, {(item.IsActive ? "active" : "inactive")})");
			}

			sb.AppendLine();
			sb.AppendLine("### Custom connection");
			sb.AppendLine(settings.IsCustomActive && !string.IsNullOrEmpty(settings.CustomConnectionString)
				? $"- **{settings.CustomConnectionString}** ({settings.CustomConnectorType}, active)"
				: "- Not configured or inactive.");

			ctx.Call.SetStatusIcon(MaterialIconKind.Database);
			ctx.Call.UseMarkdown = true;
			ctx.Call.SetResult(sb.ToString());
			return Task.FromResult(ToolExecutionResult.Succeeded);
		}

		private DomainPreviewResult? ConnectPreview(IToolExecutionContext ctx, string nameOrConnectionString)
		{
			var settings = _chatSettings.Settings.Databases.GetEffectiveDatabaseConnection();
			bool isNamed = settings.Items.Any(i => i.IsEnabled &&
				string.Equals(i.Name, nameOrConnectionString, StringComparison.OrdinalIgnoreCase));

			ctx.Call.SetStatusTitle($"**{nameOrConnectionString}**");

			return new DomainPreviewResult
			{
				ExpectedBehaviour = isNamed ? ToolBehaviour.None : ToolBehaviour.DatabaseCustomConnect
			};
		}

		private Task<ToolExecutionResult> ConnectAsync(
			[Description("The name of the configured database connection (see db-list) or a raw connection string")] string nameOrConnectionString,
			IToolExecutionContext ctx,
			[Description("The connector type for a custom connection string. Ignored when connecting to a named connection.")] string? connectorType = null)
		{
			ctx.Call.SetStatus(MaterialIconKind.DatabaseArrowRight, $"**{nameOrConnectionString}**");

			try
			{
				var parsedConnectorType = connectorType != null
					? Enum.Parse<DatabaseConnectorType>(connectorType, true) : (DatabaseConnectorType?)null;
				var description = _connectionManager.Activate(nameOrConnectionString, parsedConnectorType);

				ctx.Call.SetStatus(MaterialIconKind.DatabaseCheck, $"**{nameOrConnectionString}**");
				ctx.Call.SetResult($"Database connection **{description}** is now active. The connection will be opened on the first use (db-schema or db-execute).");
				return Task.FromResult(ToolExecutionResult.Succeeded);
			}
			catch (Exception ex)
			{
				ctx.Call.SetStatusIcon(MaterialIconKind.DatabaseOff);
				ctx.Call.SetResult($"Failed to activate the database connection. Error: {ex.Message}");
				return Task.FromResult(ToolExecutionResult.Failed);
			}
		}

		private async Task<ToolExecutionResult> GetSchemaAsync(IToolExecutionContext ctx, CancellationToken cancellationToken = default)
		{
			IDatabaseConnection connection;
			try
			{
				connection = await _connectionManager.GetCurrentAsync(cancellationToken);
			}
			catch (Exception ex)
			{
				ctx.Call.SetStatusIcon(MaterialIconKind.DatabaseOff);
				ctx.Call.SetResult($"Failed to open the database connection. Error: {ex.Message}");
				return ToolExecutionResult.Failed;
			}

			ctx.Call.SetStatusIcon(MaterialIconKind.DatabaseSearch);

			try
			{
				ctx.Call.UseMarkdown = true;
				ctx.Call.SetResult(await connection.GetSchemaAsync(cancellationToken));
				return ToolExecutionResult.Succeeded;
			}
			catch (Exception ex)
			{
				ctx.Call.SetStatusIcon(MaterialIconKind.DatabaseOff);
				ctx.Call.SetResult($"Failed to load the database schema. Error: {ex.Message}");
				return ToolExecutionResult.Failed;
			}
		}

		private DomainPreviewResult? ExecutePreview(IToolExecutionContext ctx, string query)
		{
			if (!_connectionManager.IsActiveConfigured)
			{
				ctx.Call.SetResult("No active database connection. Call db-connect first.");
				return new DomainPreviewResult
				{
					InterruptingSuccess = false
				};
			}

			var keyword = ExtractKeyword(query);
			bool isRead = ReadKeywords.Contains(keyword);

			ctx.Call.SetStatusTitle($"**{keyword}**");

			return new DomainPreviewResult
			{
				ExpectedBehaviour = isRead ? ToolBehaviour.DatabaseRead : ToolBehaviour.DatabaseChange
			};
		}

		private async Task<ToolExecutionResult> ExecuteAsync(
			[Description("The SQL statement to execute")] string query,
			IToolExecutionContext ctx,
			CancellationToken cancellationToken = default)
		{
			IDatabaseConnection connection;
			try
			{
				connection = await _connectionManager.GetCurrentAsync(cancellationToken);
			}
			catch (Exception ex)
			{
				ctx.Call.SetStatusIcon(MaterialIconKind.DatabaseOff);
				ctx.Call.SetResult($"Failed to open the database connection. Error: {ex.Message}");
				return ToolExecutionResult.Failed;
			}

			ctx.Call.SetStatus(MaterialIconKind.DatabaseSearch, $"**{ExtractKeyword(query)}**");

			try
			{
				var queryResult = await connection.ExecuteQueryAsync(query, cancellationToken);

				if (queryResult.Columns.Length == 0)
				{
					ctx.Call.SetStatusIcon(MaterialIconKind.DatabaseCheck);
					ctx.Call.SetResult($"Query executed successfully. Rows affected: {queryResult.RowsAffected}");
					return ToolExecutionResult.Succeeded;
				}

				var sb = new StringBuilder();
				sb.AppendLine($"| {string.Join(" | ", queryResult.Columns)} |");
				sb.AppendLine($"|{string.Join("|", queryResult.Columns.Select(c => "---"))}|");
				foreach (var row in queryResult.Rows)
					sb.AppendLine($"| {string.Join(" | ", row.Select(EscapeCell))} |");

				if (queryResult.Truncated)
				{
					sb.AppendLine();
					sb.AppendLine($"> Result was truncated: only the first {queryResult.Rows.Length} rows are shown.");
				}

				ctx.Call.UseMarkdown = true;
				ctx.Call.SetResult(sb.ToString());
				return ToolExecutionResult.Succeeded;
			}
			catch (Exception ex)
			{
				ctx.Call.SetStatusIcon(MaterialIconKind.DatabaseOff);
				ctx.Call.SetResult($"Query execution failed. Error: {ex.Message}");
				return ToolExecutionResult.Failed;
			}
		}

		private static string ExtractKeyword(string query)
		{
			var trimmed = query.TrimStart();
			var end = trimmed.IndexOfAny([' ', '\t', '\r', '\n', '(', ';']);
			var keyword = end < 0 ? trimmed : trimmed[..end];
			return string.IsNullOrEmpty(keyword) ? "SQL" : keyword.ToUpperInvariant();
		}

		private static string EscapeCell(string value)
		{
			if (value.Length > 300)
				value = value[..300] + "…";
			return value.Replace("|", "\\|").Replace("\r", " ").Replace("\n", " ");
		}
	}
}
