using AsyncLua;
using AsyncLua.Values;
using LLMDesktopAssistant.Scripting.Lua.Store;

namespace LLMDesktopAssistant.Tests.Scripting
{
	public class LuaStoreTests
	{
		private sealed class TempDirectory : IDisposable
		{
			public TempDirectory()
			{
				Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "dass-lua-store-tests", Guid.NewGuid().ToString("N"));
				Directory.CreateDirectory(Path);
			}

			public string Path { get; }

			public string File(string name) => System.IO.Path.Combine(Path, name);

			public void Dispose()
			{
				try
				{
					Directory.Delete(Path, recursive: true);
				}
				catch (IOException)
				{
				}
			}
		}

		private static LuaTable Table(params (string Key, LuaValue Value)[] items)
		{
			var table = new LuaTable();
			foreach (var (key, value) in items)
				table[key] = value;
			return table;
		}

		[Fact]
		public void Set_Nil_RemovesTheKey()
		{
			var store = new LuaStore("test", null);
			store.Set("a", new LuaNumber(1), null);

			store.Set("a", LuaNil.Instance, null);

			Assert.False(store.Contains("a"));
			Assert.Equal(0, store.Size());
		}

		[Fact]
		public void Set_OnMemoryStore_KeepsTheValueByReference()
		{
			var store = new LuaStore("test", null);
			var value = Table(("n", new LuaNumber(1)));

			store.Set("a", value, null);
			value["n"] = new LuaNumber(99);

			var stored = Assert.IsType<LuaTable>(store.Get("a"));
			Assert.Same(value, stored);
			Assert.Equal(99, Assert.IsType<LuaNumber>(stored.Get("n")).Value);
		}

		[Fact]
		public void Set_SameValueUnderTwoKeys_Aliases()
		{
			var store = new LuaStore("test", null);
			var value = Table(("n", new LuaNumber(1)));

			store.Set("a", value, null);
			store.Set("b", value, null);

			Assert.Same(store.Get("a"), store.Get("b"));
		}

		[Fact]
		public void GetOrAdd_KeepsTheDefaultByReference()
		{
			var store = new LuaStore("test", null);
			var value = Table(("n", new LuaNumber(1)));

			var stored = store.GetOrAdd("a", value, null);
			value["n"] = new LuaNumber(2);

			Assert.Same(value, stored);
			Assert.Equal(2, Assert.IsType<LuaNumber>(Assert.IsType<LuaTable>(store.Get("a")).Get("n")).Value);
		}

		[Fact]
		public void Get_ReturnsTheLiveValue()
		{
			var store = new LuaStore("test", null);
			store.Set("a", Table(("n", new LuaNumber(1))), null);

			var stored = Assert.IsType<LuaTable>(store.Get("a"));
			stored["n"] = new LuaNumber(5);

			Assert.Equal(5, Assert.IsType<LuaNumber>(Assert.IsType<LuaTable>(store.Get("a")).Get("n")).Value);
		}

		[Fact]
		public void GetOrAdd_WritesTheDefaultOnce()
		{
			var store = new LuaStore("test", null);

			var first = store.GetOrAdd("a", new LuaNumber(7), null);
			var second = store.GetOrAdd("a", new LuaNumber(9), null);

			Assert.Equal(7, Assert.IsType<LuaNumber>(first).Value);
			Assert.Same(first, second);
		}

		[Fact]
		public void Move_KeepsIdentity_AndCopyClones()
		{
			var store = new LuaStore("test", null);
			store.Set("a", Table(("n", new LuaNumber(1))), null);
			var stored = store.Get("a");

			Assert.True(store.Move("a", "b", null));
			Assert.False(store.Contains("a"));
			Assert.Same(stored, store.Get("b"));

			Assert.True(store.Copy("b", "c", null));
			Assert.NotSame(store.Get("b"), store.Get("c"));
		}

		[Fact]
		public void Keys_FiltersByPrefix()
		{
			var store = new LuaStore("test", null);
			store.Set("dnd.character1", new LuaNumber(1), null);
			store.Set("dnd.character2", new LuaNumber(2), null);
			store.Set("other", new LuaNumber(3), null);

			var keys = store.Keys("dnd.");

			Assert.Equal(2, keys.Length);
			Assert.Equal("dnd.character1", Assert.IsType<LuaString>(keys.Get(1)).Value);
			Assert.Equal("dnd.character2", Assert.IsType<LuaString>(keys.Get(2)).Value);
		}

		[Fact]
		public void CyclicTable_IsAcceptedInMemory_AndRaisesOnCopy()
		{
			var store = new LuaStore("test", null);
			var cyclic = new LuaTable();
			cyclic["self"] = cyclic;

			store.Set("a", cyclic, null);
			Assert.Same(cyclic, store.Get("a"));

			// The guard belongs to the walk, not to the write: only a copy or a snapshot walks the value.
			Assert.Throws<LuaRuntimeException>(() => store.Copy("a", "b", null));
		}

		[Fact]
		public void CyclicTable_RaisesWhenWrittenToAPersistedStore()
		{
			using var directory = new TempDirectory();
			var store = new LuaStore("test", new LuaFileStoreBackend(directory.File("data.json")));
			var cyclic = new LuaTable();
			cyclic["self"] = cyclic;

			Assert.Throws<LuaRuntimeException>(() => store.Set("a", cyclic, null));
		}

		[Fact]
		public void FileStore_RoundTripsValues()
		{
			using var directory = new TempDirectory();
			var path = directory.File("data.json");

			var store = new LuaStore("test", new LuaFileStoreBackend(path));
			store.Set("number", new LuaNumber(42), null);
			store.Set("text", new LuaString("hello"), null);
			store.Set("nested", Table(("a", new LuaNumber(1)), ("b", new LuaString("two"))), null);

			var reloaded = new LuaStore("test", new LuaFileStoreBackend(path));

			Assert.Equal(42, Assert.IsType<LuaNumber>(reloaded.Get("number")).Value);
			Assert.Equal("hello", Assert.IsType<LuaString>(reloaded.Get("text")).Value);
			Assert.Equal("two", Assert.IsType<LuaString>(Assert.IsType<LuaTable>(reloaded.Get("nested")).Get("b")).Value);
			Assert.Equal(3, reloaded.Size());
		}

		[Fact]
		public void FileStore_SurvivesNonFiniteNumbers()
		{
			using var directory = new TempDirectory();
			var path = directory.File("data.json");

			var store = new LuaStore("test", new LuaFileStoreBackend(path));
			store.Set("nan", new LuaNumber(double.NaN), null);
			store.Set("inf", new LuaNumber(double.PositiveInfinity), null);
			store.Set("neg", new LuaNumber(double.NegativeInfinity), null);

			var json = File.ReadAllText(path);
			Assert.Contains("\"NaN\"", json);
			Assert.Contains("\"Infinity\"", json);

			var reloaded = new LuaStore("test", new LuaFileStoreBackend(path));

			Assert.True(double.IsNaN(Assert.IsType<LuaNumber>(reloaded.Get("nan")).Value));
			Assert.True(double.IsPositiveInfinity(Assert.IsType<LuaNumber>(reloaded.Get("inf")).Value));
			Assert.True(double.IsNegativeInfinity(Assert.IsType<LuaNumber>(reloaded.Get("neg")).Value));
		}

		[Fact]
		public void FileStore_DropsFunctionsWithAWarning_AndKeepsThemLiveUntilReload()
		{
			using var directory = new TempDirectory();
			var path = directory.File("data.json");

			var store = new LuaStore("test", new LuaFileStoreBackend(path));
			var warnings = new List<string>();
			var handler = new LuaCallbackFunction((context, args) => new LuaTuple(LuaNil.Instance));

			store.Set("bad", Table(("handler", handler)), warnings.Add);

			Assert.Single(warnings);
			Assert.Contains("bad.handler", warnings[0]);
			Assert.IsType<LuaCallbackFunction>(Assert.IsType<LuaTable>(store.Get("bad")).Get("handler"));

			var reloaded = new LuaStore("test", new LuaFileStoreBackend(path));
			Assert.Equal(0, Assert.IsType<LuaTable>(reloaded.Get("bad")).Length);
		}

		[Fact]
		public void FileStore_QuarantinesABrokenFile()
		{
			using var directory = new TempDirectory();
			var path = directory.File("data.json");
			File.WriteAllText(path, "{ broken");

			var store = new LuaStore("test", new LuaFileStoreBackend(path));

			Assert.Equal(0, store.Size());
			Assert.Single(Directory.GetFiles(directory.Path, "*.broken"));
			Assert.False(File.Exists(path));
		}

		[Fact]
		public void FileStore_TreatsAnEmptyFileAsAnEmptyStore()
		{
			using var directory = new TempDirectory();
			var path = directory.File("data.json");
			File.WriteAllText(path, string.Empty);

			var store = new LuaStore("test", new LuaFileStoreBackend(path));

			Assert.Equal(0, store.Size());
			Assert.True(File.Exists(path));
		}

		[Fact]
		public void Import_MergesOverExistingKeys_AndExportRoundTrips()
		{
			using var directory = new TempDirectory();
			var sourcePath = directory.File("source.json");
			File.WriteAllText(sourcePath, """{"a": 1, "b": 2}""");

			var store = new LuaStore("test", null);
			store.Set("a", new LuaNumber(100), null);
			store.Set("c", new LuaNumber(3), null);

			Assert.Equal(2, store.Import(sourcePath, null));
			Assert.Equal(1, Assert.IsType<LuaNumber>(store.Get("a")).Value);
			Assert.Equal(3, Assert.IsType<LuaNumber>(store.Get("c")).Value);

			var targetPath = directory.File("target.json");
			Assert.Equal(3, store.Export(targetPath, null));

			var copy = new LuaStore("copy", null);
			Assert.Equal(3, copy.Import(targetPath, null));
			Assert.Equal(2, Assert.IsType<LuaNumber>(copy.Get("b")).Value);
		}

		[Fact]
		public void Import_DoesNotTouchABrokenSourceFile()
		{
			using var directory = new TempDirectory();
			var sourcePath = directory.File("source.json");
			File.WriteAllText(sourcePath, "{ broken");

			var store = new LuaStore("test", null);

			Assert.ThrowsAny<Exception>(() => store.Import(sourcePath, null));
			Assert.True(File.Exists(sourcePath));
			Assert.Empty(Directory.GetFiles(directory.Path, "*.broken"));
		}
	}
}
