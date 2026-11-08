using AsyncLua;
using LLMDesktopAssistant.Data.Connectors;
using LLMDesktopAssistant.Scripting.Lua;
using LLMDesktopAssistant.Scripting.Lua.Store;

namespace LLMDesktopAssistant.Tests.Database
{
	public class LuaDatabaseHandleTests
	{
		[Fact]
		public async Task Execute_TablePackWithTrailingNil_PassesBothParameters()
		{
			var (state, handle, connection) = CreateHandle();

			await state.ExecuteAsync("return h:execute('select @p0, @p1', table.pack('a', nil))");

			Assert.NotNull(connection.LastParameters);
			Assert.Equal(2, connection.LastParameters!.Count);
			Assert.Equal("a", connection.LastParameters[0]);
			Assert.Null(connection.LastParameters[1]);
		}

		[Fact]
		public async Task Execute_TablePackWithNilInTheMiddle_KeepsThePosition()
		{
			var (state, handle, connection) = CreateHandle();

			await state.ExecuteAsync("return h:execute('select @p0, @p1, @p2', table.pack('a', nil, 'c'))");

			Assert.NotNull(connection.LastParameters);
			Assert.Equal(3, connection.LastParameters!.Count);
			Assert.Equal("a", connection.LastParameters[0]);
			Assert.Null(connection.LastParameters[1]);
			Assert.Equal("c", connection.LastParameters[2]);
		}

		[Fact]
		public async Task Execute_TablePackWithSingleNil_PassesOneNullParameter()
		{
			var (state, handle, connection) = CreateHandle();

			await state.ExecuteAsync("return h:execute('select @p0', table.pack(nil))");

			Assert.NotNull(connection.LastParameters);
			Assert.Single(connection.LastParameters!);
			Assert.Null(connection.LastParameters[0]);
		}

		[Fact]
		public async Task Execute_PlainArray_DropsTheTrailingNil()
		{
			var (state, handle, connection) = CreateHandle();

			await state.ExecuteAsync("return h:execute('select @p0, @p1', { 'a', nil })");

			Assert.NotNull(connection.LastParameters);
			Assert.Single(connection.LastParameters!);
		}

		[Fact]
		public async Task Execute_NoOptions_AppliesTheDefaultRowLimit()
		{
			var (state, handle, connection) = CreateHandle();

			await state.ExecuteAsync("return h:execute('select 1')");

			Assert.Equal(10_000, connection.LastMaxRows);
		}

		[Fact]
		public async Task Execute_LimitZero_MeansNoLimit()
		{
			var (state, handle, connection) = CreateHandle();

			await state.ExecuteAsync("return h:execute('select 1', nil, { limit = 0 })");

			Assert.Null(connection.LastMaxRows);
		}

		[Fact]
		public async Task Execute_ExplicitLimit_IsPassedThrough()
		{
			var (state, handle, connection) = CreateHandle();

			await state.ExecuteAsync("return h:execute('select 1', nil, { limit = 7 })");

			Assert.Equal(7, connection.LastMaxRows);
		}

		[Fact]
		public async Task Result_ExposesColumnsRowsAndTruncated()
		{
			var (state, handle, _) = CreateHandle();

			var result = await state.ExecuteAsync("""
				local r = await h:execute('select 1')
				return r.columns[1], r.rows[1][1], tostring(r.truncated), tostring(r.rows_affected)
				""");

			Assert.Equal("c", result[0].ToString());
			Assert.Equal("r", result[1].ToString());
			Assert.Equal("false", result[2].ToString());
			Assert.Equal("nil", result[3].ToString());
		}

		[Fact]
		public async Task SessionStore_KeepsTheHandleByReference()
		{
			var (state, handle, _) = CreateHandle();
			var store = new LuaStore("test", backend: null);
			var userdata = LuaDatabaseHandle.Wrap(handle);

			store.Set("db", userdata, null);
			state.Globals.Set("h", store.Get("db"));

			Assert.Same(userdata, store.Get("db"));

			var result = await state.ExecuteAsync("""
				local before = tostring(h.is_closed)
				await h:close()
				return before, tostring(h.is_closed), tostring(h.type)
				""");

			Assert.Equal("false", result[0].ToString());
			Assert.Equal("true", result[1].ToString());
			Assert.Equal("sqlite", result[2].ToString());
			Assert.True(handle.IsClosed);
		}

		[Fact]
		public async Task Close_MarksTheHandleAndRefusesFurtherCalls()
		{
			var (state, handle, _) = CreateHandle();

			var result = await state.ExecuteAsync("""
				local closed = await h:close()
				local ok, err = pcall(function() return await h:execute('select 1') end)
				return tostring(closed), tostring(h.is_closed), tostring(ok), tostring(err)
				""");

			Assert.Equal("true", result[0].ToString());
			Assert.Equal("true", result[1].ToString());
			Assert.Equal("false", result[2].ToString());
			Assert.Contains("closed", result[3].ToString());
			Assert.True(handle.IsClosed);
		}

		private static (LuaState State, LuaDatabaseHandle Handle, FakeConnection Connection) CreateHandle()
		{
			var connection = new FakeConnection();
			var handle = new LuaDatabaseHandle(new FakeLease(connection), new LuaDatabaseHandleRegistry());
			var state = new LuaState().LoadDefaultLibraries();
			state.Globals.Set("h", LuaDatabaseHandle.Wrap(handle));

			return (state, handle, connection);
		}

		private sealed class FakeConnection : IDatabaseConnection
		{
			public DatabaseConnectorType Type => DatabaseConnectorType.SQLite;

			public IReadOnlyList<object?>? LastParameters { get; private set; }

			public int? LastMaxRows { get; private set; }

			public Task OpenAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

			public Task TestConnectionAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

			public Task<DatabaseQueryResult> ExecuteQueryAsync(
				string query,
				IReadOnlyList<object?>? parameters = null,
				int? maxRows = null,
				CancellationToken cancellationToken = default)
			{
				LastParameters = parameters;
				LastMaxRows = maxRows;

				return Task.FromResult(new DatabaseQueryResult
				{
					Columns = ["c"],
					Rows = [["r"]],
				});
			}

			public Task<string> GetSchemaAsync(CancellationToken cancellationToken = default) => Task.FromResult("schema");

			public ValueTask DisposeAsync() => default;
		}

		private sealed class FakeLease(IDatabaseConnection connection) : IDatabaseConnectionLease
		{
			public DatabaseConnectorType Type => connection.Type;

			public string ConnectionString => "fake";

			public Task<IDatabaseConnection> EnsureAsync(CancellationToken cancellationToken = default)
				=> Task.FromResult(connection);

			public Task TestAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;

			public Task<bool> CloseAsync(bool force, CancellationToken cancellationToken = default)
				=> Task.FromResult(true);

			public ValueTask DisposeAsync() => default;
		}
	}
}
