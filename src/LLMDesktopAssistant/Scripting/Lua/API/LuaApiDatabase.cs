using System.Data.Common;
using AsyncLua;
using AsyncLua.Values;
using LLMDesktopAssistant.ApiKeys;
using LLMDesktopAssistant.Data.Connectors;
using LLMDesktopAssistant.LLM.Services;
using Microsoft.Data.Sqlite;

namespace LLMDesktopAssistant.Scripting.Lua.API
{
	/// <summary>
	/// Lua API for database access: the <c>db</c> namespace.
	/// </summary>
	/// <remarks>
	/// Mirrors the <c>db-*</c> tools on purpose, with one difference: the tools act on the chat's active
	/// connection, while a Lua handle names its own. The connection itself always comes from the
	/// application-wide cache, never from a private one.
	/// </remarks>
	[LuaApi(chatScoped: true)]
	public class LuaApiDatabase(
		IDatabaseConnectionCache cache,
		IDatabaseConnectionManager connectionManager,
		IChatSettingsService chatSettings,
		IApiKeyManagerService apiKeyManager,
		IWorkingDirectoryAccessService workingDirectory,
		LuaDatabaseHandleRegistry registry) : LuaApiBase
	{
		private static readonly HashSet<string> secretKeyFragments =
		[
			"password", "pwd", "secret", "token", "key", "credential"
		];

		/// <inheritdoc />
		public override string? Namespace => "db";

		/// <inheritdoc />
		public override string? Manuals => """
			--- db - database access

			Connections are shared by the whole application and cached by (connector type, connection
			string). A handle is a KEY, not a connection: every call re-resolves the cached connection
			and reopens it when somebody else closed it, so a handle survives other chats and script
			reloads. Store it in store.session to keep it across reloads.

			HANDLE (a userdata):

			  handle.type                string  - connector name, one of db.connectors()
			  handle.connection_string   string  - the connection string this handle was created with
			  handle.is_closed           boolean - set by close()
			  handle:execute(sql, [parameters], [options]) -> result
			  handle:schema()            string  - markdown description of tables, views and columns
			  handle:test()              boolean, error
			  handle:close([force])      boolean - true when the connection itself was closed

			FUNCTIONS:

			--- db.connectors() -> array of strings
			  The connector names this platform can run, e.g. {"sqlite", "postgresql"}.
			  A name that is not in this list cannot be used.

			--- db.list() -> array of tables
			  Every configured connection of the chat, the custom one, and every connection that is
			  open right now even if nothing configured it (source = "ad-hoc"):
				{ source = "named"|"custom"|"ad-hoc", name = string or nil, type = string,
				  connection_string = string, is_active = boolean, is_open = boolean }
			  Passwords and tokens are masked here.

			--- db.active() -> table or nil
			  The connection the chat's db-* tools use, or nil when none is configured.
			  Same fields as db.list() plus 'description'.

			--- async db.connect(name) -> handle, error
			  A named connection from the chat settings. The name is matched case-insensitively.

			--- async db.connect(type, connection_string) -> handle, error
			  A custom connection. 'type' is a name from db.connectors(); 'connection_string' is native
			  to that database. For SQLite a string without '=' is treated as a file path and resolved
			  against the chat working directory.

			  Both forms open the connection immediately, so a bad connection string fails here and not
			  later. A failure returns nil plus the reason.

			--- async handle:execute(sql, [parameters], [options]) -> result
			  Executes one statement (or a multi-statement batch, when the database supports it).

			  parameters: array table of positional values, referenced from the SQL as @p0, @p1, ...
			  in the same order: execute("select * from t where id = @p0", { 42 }). A nil element is
			  sent as NULL. Lua values that are not strings, numbers, booleans or nil are refused.

			  #parameters drops a TRAILING nil, so a NULL in the last position needs table.pack:
				{ "a", nil }               -- one parameter
				table.pack("a", nil)       -- two parameters, the second is NULL

			  options:
			    limit = number (default 10000) - the maximum number of rows materialized. 0 means
			    no limit. The cap exists to keep a forgotten LIMIT from eating the whole memory;
			    it does not change the SQL and does not round-trip the database.

			  Returns:
			    columns: array of column names
			    rows: array of arrays, one per row, in column order. A NULL cell is nil, which is
			      why rows are arrays and not name-keyed tables: a join can repeat a column name.
			    truncated: boolean - true when the limit cut the result
			    rows_affected: number - only for statements without a result set (INSERT, UPDATE, DDL)

			  Throws on a SQL error, an unknown parameter, or a closed handle.

			--- async handle:schema() -> string
			  The same markdown the db-schema tool returns.

			--- async handle:test() -> boolean, error
			  Opens the connection without disturbing the cached one. Returns false plus the reason
			  instead of throwing.

			--- async handle:close([force]) -> boolean
			  Closes the handle. The connection is closed only when this handle was its last user,
			  because other chats may be using the same (type, connection string). force = true closes
			  it anyway; the other chats reconnect on their next call. Either way this handle is dead,
			  and its methods throw afterwards.

			LIMITS OF THE MODEL:

			  The connection is shared, so there are NO transactions and no session state: BEGIN,
			  COMMIT, SET, PRAGMA and temporary tables would leak into every other chat using the same
			  connection. Executing them is not blocked, it is just not yours to rely on.

			  db.switch() does not exist on purpose: repointing the chat's active connection is a chat
			  setting, and a script doing it silently would surprise the next db-execute.

			EXAMPLES:

			  local h, err = await db.connect("sqlite", "notes.db")
			  if not h then print("connect failed:", err) return end

			  local r = await h:execute("select id, title from notes where title like @p0", { "%lua%" })
			  for _, row in ipairs(r.rows) do
			    print(row[1], row[2])
			  end
			  if r.truncated then print("more rows exist") end

			  local ins = await h:execute("insert into notes (title) values (@p0)", { "hello" })
			  print("inserted rows:", ins.rows_affected)

			  local everything = await h:execute("select * from notes", nil, { limit = 0 })
			  print("rows:", #everything.rows)

			  print(await h:schema())
			  h:close()

			  -- A named connection from the chat settings
			  local prod = await db.connect("prod")

			  -- Who is open right now
			  for _, c in ipairs(db.list()) do
			    print(c.source, c.name or "-", c.type, c.is_open and "open" or "closed")
			  end
			""";

		/// <inheritdoc />
		public override Action? Populate(LuaTable globals, LuaTable ns, LuaService luaService)
		{
			ns["connectors"] = new LuaCallbackFunction(Connectors);
			ns["list"] = new LuaCallbackFunction(List);
			ns["active"] = new LuaCallbackFunction(Active);
			ns["connect"] = new LuaCallbackFunction(ConnectAsync);
			return null;
		}

		private LuaTuple Connectors(LuaCallingContext ctx, LuaValue[] args)
		{
			var result = new LuaTable();
			int i = 1;

			foreach (var type in cache.SupportedConnectors)
				result[i++] = new LuaString(TypeName(type));

			return new LuaTuple(result);
		}

		private LuaTuple List(LuaCallingContext ctx, LuaValue[] args)
		{
			var settings = chatSettings.Settings.Databases.GetEffectiveDatabaseConnection();
			var open = cache.ActiveConnections.ToHashSet();
			var described = new HashSet<(DatabaseConnectorType, string)>();
			var result = new LuaTable();
			int i = 1;

			foreach (var item in settings.Items)
			{
				if (!item.IsEnabled)
					continue;

				var connectionString = item.GetConnectionString(apiKeyManager) ?? string.Empty;
				described.Add((item.ConnectorType, connectionString));

				result[i++] = Describe(
					source: "named",
					name: item.Name,
					type: item.ConnectorType,
					connectionString: connectionString,
					isActive: item.IsActive && !settings.IsCustomActive,
					isOpen: open.Contains((item.ConnectorType, connectionString)));
			}

			if (!string.IsNullOrWhiteSpace(settings.CustomConnectionString))
			{
				described.Add((settings.CustomConnectorType, settings.CustomConnectionString));

				result[i++] = Describe(
					source: "custom",
					name: null,
					type: settings.CustomConnectorType,
					connectionString: settings.CustomConnectionString,
					isActive: settings.IsCustomActive,
					isOpen: open.Contains((settings.CustomConnectorType, settings.CustomConnectionString)));
			}

			// Connections a script opened by itself are not configured anywhere, but they are open.
			foreach (var (type, connectionString) in open)
			{
				if (described.Contains((type, connectionString)))
					continue;

				result[i++] = Describe(
					source: "ad-hoc",
					name: null,
					type: type,
					connectionString: connectionString,
					isActive: false,
					isOpen: true);
			}

			return new LuaTuple(result);
		}

		private LuaTuple Active(LuaCallingContext ctx, LuaValue[] args)
		{
			if (connectionManager.ActiveConnection is not { } active)
				return new LuaTuple(LuaNil.Instance);

			var open = cache.ActiveConnections.ToHashSet();
			var table = Describe(
				source: "active",
				name: null,
				type: active.Type,
				connectionString: active.ConnectionString,
				isActive: true,
				isOpen: open.Contains((active.Type, active.ConnectionString)));

			table["description"] = new LuaString(active.Description);
			return new LuaTuple(table);
		}

		private async Task<LuaTuple> ConnectAsync(LuaCallingContext ctx, LuaValue[] args)
		{
			if (args.Length == 0)
				throw new LuaRuntimeException("db.connect(name) or db.connect(type, connection_string): at least 1 argument expected.");

			DatabaseConnectorType type;
			string connectionString;

			if (args.Length >= 2)
			{
				if (args[0] is not LuaString typeValue)
					throw new LuaRuntimeException("db.connect(type, connection_string): the first argument must be a string (connector type).");
				if (args[1] is not LuaString connectionStringValue)
					throw new LuaRuntimeException("db.connect(type, connection_string): the second argument must be a string (connection string).");

				type = ParseConnectorType(typeValue.Value);
				connectionString = ResolveConnectionString(type, connectionStringValue.Value);
			}
			else
			{
				if (args[0] is not LuaString nameValue)
					throw new LuaRuntimeException("db.connect(name): the argument must be a string (connection name).");

				(type, connectionString) = ResolveNamed(nameValue.Value);
			}

			try
			{
				var lease = await cache.AcquireAsync(type, connectionString, ctx.CancellationToken);

				try
				{
					// Opening here is the whole point of connect: a bad connection string fails now.
					await (await lease.EnsureAsync(ctx.CancellationToken)).OpenAsync(ctx.CancellationToken);
				}
				catch
				{
					// A connection that cannot be opened must not stay cached for everyone else.
					await lease.CloseAsync(force: true, CancellationToken.None);
					throw;
				}

				var handle = new LuaDatabaseHandle(lease, registry);
				registry.Register(handle);
				return new LuaTuple(LuaDatabaseHandle.Wrap(handle));
			}
			catch (OperationCanceledException)
			{
				throw;
			}
			catch (Exception ex)
			{
				return new LuaTuple(LuaNil.Instance, new LuaString(ex.Message));
			}
		}

		private (DatabaseConnectorType Type, string ConnectionString) ResolveNamed(string name)
		{
			var settings = chatSettings.Settings.Databases.GetEffectiveDatabaseConnection();
			var item = settings.Items.FirstOrDefault(i => i.IsEnabled &&
				string.Equals(i.Name, name, StringComparison.OrdinalIgnoreCase));

			if (item is null)
			{
				var available = settings.Items.Where(i => i.IsEnabled).Select(i => i.Name).ToArray();
				var list = available.Length == 0 ? "none configured" : string.Join(", ", available);
				throw new LuaRuntimeException($"db.connect('{name}'): no such configured connection. Available: {list}.");
			}

			var connectionString = item.GetConnectionString(apiKeyManager);
			if (string.IsNullOrWhiteSpace(connectionString))
				throw new LuaRuntimeException($"db.connect('{name}'): the configured connection has an empty connection string.");

			return (item.ConnectorType, connectionString);
		}

		private DatabaseConnectorType ParseConnectorType(string name)
		{
			var normalized = name.Trim().ToLowerInvariant();
			var supported = cache.SupportedConnectors.ToArray();

			DatabaseConnectorType? type = normalized switch
			{
				"sqlite" or "sqlite3" => DatabaseConnectorType.SQLite,
				"postgres" or "postgresql" or "pgsql" => DatabaseConnectorType.PostgreSQL,
				"sqlserver" or "mssql" => DatabaseConnectorType.SQLServer,
				_ => null,
			};

			if (type is { } resolved && supported.Contains(resolved))
				return resolved;

			var list = string.Join(", ", supported.Select(TypeName));
			throw new LuaRuntimeException($"db.connect(): unknown connector type '{name}'. Supported: {list}.");
		}

		/// <summary>
		/// A bare file path is a SQLite database: the process working directory is not something a
		/// script author can reason about, so it is resolved against the chat working directory.
		/// </summary>
		private string ResolveConnectionString(DatabaseConnectorType type, string connectionString)
		{
			if (type != DatabaseConnectorType.SQLite || connectionString.Contains('='))
				return connectionString;

			return new SqliteConnectionStringBuilder
			{
				DataSource = workingDirectory.GetFullPath(connectionString)
			}.ConnectionString;
		}

		private static LuaTable Describe(
			string source,
			string? name,
			DatabaseConnectorType type,
			string? connectionString,
			bool isActive,
			bool isOpen)
		{
			return new LuaTable
			{
				["source"] = new LuaString(source),
				["name"] = name is null ? LuaNil.Instance : new LuaString(name),
				["type"] = new LuaString(TypeName(type)),
				["connection_string"] = new LuaString(Mask(connectionString)),
				["is_active"] = LuaBoolean.FromBoolean(isActive),
				["is_open"] = LuaBoolean.FromBoolean(isOpen),
			};
		}

		/// <summary>
		/// Masks the values of password-like keys: these tables end up in tool results, prompts and
		/// chat history, which is not where a connection string's secrets belong.
		/// </summary>
		/// <remarks>
		/// A string without secrets is returned untouched, because it is also the cache key: a
		/// re-serialized one would not match, and reusing it in <c>db.connect</c> would open a second
		/// connection to the same database.
		/// </remarks>
		private static string Mask(string? connectionString)
		{
			if (string.IsNullOrEmpty(connectionString))
				return string.Empty;

			try
			{
				var builder = new DbConnectionStringBuilder { ConnectionString = connectionString };
				var secretFound = false;

				foreach (var key in builder.Keys.Cast<string>().ToArray())
				{
					if (!secretKeyFragments.Any(fragment => key.Contains(fragment, StringComparison.OrdinalIgnoreCase)))
						continue;

					builder[key] = "***";
					secretFound = true;
				}

				return secretFound ? builder.ConnectionString : connectionString;
			}
			catch (ArgumentException)
			{
				// Not a key-value string: a bare SQLite file path, which holds no secret.
				return connectionString;
			}
		}

		private static string TypeName(DatabaseConnectorType type) => type.ToString().ToLowerInvariant();
	}
}
