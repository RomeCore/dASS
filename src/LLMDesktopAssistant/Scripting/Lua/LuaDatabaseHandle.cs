using AsyncLua;
using AsyncLua.Values;
using LLMDesktopAssistant.Data.Connectors;

namespace LLMDesktopAssistant.Scripting.Lua
{
	/// <summary>
	/// A database handle exposed to Lua as the <c>db_handle</c> userdata returned by <c>db.connect(...)</c>.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The handle is a key, not a connection: every operation re-resolves the cached connection through
	/// its lease, so a connection closed by another chat (or expired by the cache) costs a reconnect
	/// instead of a failed call.
	/// </para>
	/// <para>
	/// Members are named for Lua rather than for C# (<c>execute</c>, not <c>execute_async</c>), the same
	/// way the terminal handle names <c>wait</c> and <c>write</c>. Properties are computed because they
	/// describe this object, not a snapshot of it.
	/// </para>
	/// </remarks>
	public sealed class LuaDatabaseHandle
	{
		/// <summary>
		/// The row cap applied when the caller does not ask for one: a guard against materializing
		/// a whole table by accident, not a statement about what the query may return.
		/// </summary>
		private const int defaultRowLimit = 10_000;

		private readonly IDatabaseConnectionLease _lease;
		private readonly LuaDatabaseHandleRegistry _registry;
		private bool _closed;

		/// <summary>
		/// Initializes a handle over an acquired lease.
		/// </summary>
		/// <param name="lease">The lease this handle owns.</param>
		/// <param name="registry">The chat-scoped registry that closes leftovers when the chat dies.</param>
		public LuaDatabaseHandle(IDatabaseConnectionLease lease, LuaDatabaseHandleRegistry registry)
		{
			_lease = lease;
			_registry = registry;
		}

		/// <summary>
		/// Wraps the handle into a <c>db_handle</c> userdata.
		/// </summary>
		/// <param name="handle">The handle to wrap.</param>
		[LuaHidden]
		public static LuaUserData Wrap(LuaDatabaseHandle handle)
			=> new(handle, "db_handle")
			{
				Metatable = UserDataMetatableGenerator.GetOrCreate(typeof(LuaDatabaseHandle))
			};

		/// <summary>
		/// Gets the lowercase connector name, one of <c>db.connectors()</c>.
		/// </summary>
		public string Type => _lease.Type.ToString().ToLowerInvariant();

		/// <summary>
		/// Gets the connection string this handle was created with.
		/// </summary>
		public string ConnectionString => _lease.ConnectionString;

		/// <summary>
		/// Gets a value indicating whether the handle was closed.
		/// </summary>
		public bool IsClosed => _closed;

		/// <summary>
		/// Closes the handle, and the connection behind it when this handle is its last user.
		/// </summary>
		/// <param name="force">Whether to close the connection even when other chats still hold it.</param>
		/// <param name="cancellationToken">The cancellation token of the running script.</param>
		/// <returns><see langword="true"/> when the connection itself was closed.</returns>
		public async Task<bool> Close(bool force = false, CancellationToken cancellationToken = default)
		{
			if (_closed)
				return false;

			_closed = true;
			_registry.Unregister(this);

			return await _lease.CloseAsync(force, cancellationToken);
		}

		/// <summary>
		/// Executes one SQL statement and returns <c>columns</c>, <c>rows</c>, <c>truncated</c> and,
		/// for statements without a result set, <c>rows_affected</c>.
		/// </summary>
		/// <param name="sql">The SQL to execute.</param>
		/// <param name="parameters">The positional parameters, referenced from the SQL as <c>@p0</c>, <c>@p1</c> and so on.</param>
		/// <param name="options">Accepts <c>limit</c>: the maximum number of rows, <c>0</c> for no limit.</param>
		/// <param name="cancellationToken">The cancellation token of the running script.</param>
		public async Task<LuaTable> Execute(
			string sql,
			LuaTable? parameters = null,
			LuaTable? options = null,
			CancellationToken cancellationToken = default)
		{
			ThrowIfClosed("execute");

			var connection = await _lease.EnsureAsync(cancellationToken);

			var result = await connection.ExecuteQueryAsync(
				sql,
				ParseParameters(parameters),
				ParseRowLimit(options),
				cancellationToken);

			return ToLua(result);
		}

		/// <summary>
		/// Returns the markdown description of tables, views and columns.
		/// </summary>
		/// <param name="cancellationToken">The cancellation token of the running script.</param>
		public async Task<string> Schema(CancellationToken cancellationToken = default)
		{
			ThrowIfClosed("schema");

			var connection = await _lease.EnsureAsync(cancellationToken);
			return await connection.GetSchemaAsync(cancellationToken);
		}

		/// <summary>
		/// Proves that the connection opens, without disturbing the cached one.
		/// </summary>
		/// <param name="cancellationToken">The cancellation token of the running script.</param>
		/// <returns><c>true</c>, or <c>false</c> plus the reason.</returns>
		public async Task<LuaTuple> Test(CancellationToken cancellationToken = default)
		{
			ThrowIfClosed("test");

			try
			{
				await _lease.TestAsync(cancellationToken);
				return new LuaTuple(LuaBoolean.True);
			}
			catch (OperationCanceledException)
			{
				throw;
			}
			catch (Exception ex)
			{
				return new LuaTuple(LuaBoolean.False, new LuaString(ex.Message));
			}
		}

		private void ThrowIfClosed(string method)
		{
			if (_closed)
				throw new LuaRuntimeException($"db handle:{method}(): the handle is closed.");
		}

		private static List<object?>? ParseParameters(LuaTable? table)
		{
			if (table is null)
				return null;

			// table.pack("a", nil) keeps the trailing nil in 'n': #t drops it, and that is the only
			// difference between a packed and a literal array.
			var count = table.Get("n") is LuaNumber explicitCount ? (int)explicitCount.Value : table.Length;

			var result = new List<object?>(count);
			for (int i = 1; i <= count; i++)
				result.Add(ToClrValue(table.Get(i), i));

			return result;
		}

		private static object? ToClrValue(LuaValue value, int position)
		{
			return value switch
			{
				LuaNil => null,
				LuaString text => text.Value,
				LuaBoolean boolean => boolean.Value,
				LuaNumber number => number.Value == Math.Truncate(number.Value) && Math.Abs(number.Value) <= long.MaxValue
					? (object)(long)number.Value
					: number.Value,
				_ => throw new LuaRuntimeException(
					$"db handle:execute(sql, parameters): parameter {position} must be a string, number, boolean or nil."),
			};
		}

		/// <summary>
		/// Reads <c>options.limit</c>: absent means <c>defaultRowLimit</c>, <c>0</c> means no limit.
		/// </summary>
		private static int? ParseRowLimit(LuaTable? options)
		{
			if (options?.Get("limit") is not LuaNumber limit)
				return defaultRowLimit;

			if (limit.Value <= 0)
				return null;

			return (int)Math.Min(limit.Value, int.MaxValue);
		}

		private static LuaTable ToLua(DatabaseQueryResult result)
		{
			var columns = new LuaTable();
			for (int i = 0; i < result.Columns.Length; i++)
				columns[i + 1] = new LuaString(result.Columns[i]);

			var rows = new LuaTable();
			for (int r = 0; r < result.Rows.Length; r++)
			{
				var source = result.Rows[r];
				var row = new LuaTable();

				for (int c = 0; c < source.Length; c++)
					row[c + 1] = source[c] is null ? LuaNil.Instance : new LuaString(source[c]!);

				rows[r + 1] = row;
			}

			var table = new LuaTable
			{
				["columns"] = columns,
				["rows"] = rows,
				["truncated"] = LuaBoolean.FromBoolean(result.Truncated),
			};

			// Rows-affected only means something for statements that return no result set.
			if (result.Columns.Length == 0)
				table["rows_affected"] = new LuaNumber(result.RowsAffected);

			return table;
		}
	}
}
