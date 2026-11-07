using System.Security.Cryptography;
using System.Text;
using AsyncLua;
using AsyncLua.Values;
using LLMDesktopAssistant.LLM.Services;
using LLMDesktopAssistant.Scripting.Lua.Store;
using LLMDesktopAssistant.Utils;

namespace LLMDesktopAssistant.Scripting.Lua.API
{
	/// <summary>
	/// Lua API for persistent keyed storage: <c>store.*</c>.
	/// </summary>
	/// <remarks>
	/// Four stores share one method set and differ only in lifetime and backend:
	/// <c>session</c> (chat memory), <c>appsession</c> (application memory), <c>chat</c> (chat database)
	/// and <c>file</c> (one JSON file per store). <c>workdir</c> and <c>user</c> are named-file shortcuts.
	/// </remarks>
	[LuaApi(chatScoped: true)]
	public class LuaApiStore(
		LuaSessionStoreService sessionStore,
		LuaChatStoreService chatStore,
		LuaStoreRegistry registry,
		IWorkingDirectoryAccessService workingDirectory) : LuaApiBase
	{
		private const string DefaultStoreName = "shared";
		private const string WorkdirStoreRoot = "store";
		private const string UserStoreRoot = "store";

		/// <inheritdoc />
		public override string? Namespace => "store";

		/// <inheritdoc />
		public override string? Manuals => """
			--- store - persistent keyed storage

			Four stores, one method set, different lifetime:
			  store.session      - in memory, per chat; survives Lua script reloads, dies with the chat
			  store.appsession   - in memory, shared by ALL open chats; dies when the application exits
			  store.chat         - persisted in the chat database, follows the chat
			  store.file(path)   - one JSON file; the SAME store instance is returned for the same path
			  store.workdir[(n)] - shortcut for <working directory>/.llmassist/store/<n>.json
			  store.user[(n)]    - shortcut for %LOCALAPPDATA%\.llmassist\store\<n>.json

			The default name for store.workdir and store.user is "shared", so store.workdir and
			store.workdir("shared") are the same store. Names are file names; anything outside
			[A-Za-z0-9._-] is percent-encoded.

			VALUES may be anything: numbers, strings, booleans, tables - store.set("hp", 100) is fine.

			READING SHARES, WRITING COPIES:
			  local t = store.get("k"); t.x = 1   -- visible to every later get()
			  store.set("k", t)                   -- an in-memory store keeps a DEEP COPY of t,
			                                      -- so later changes to t are lost

			METHODS (every store):
			  get(key)                        -> value or nil
			  get_or_add(key [, default={}])  -> the value, writing the default when the key is missing
			  set(key, value)                 -> nil; set(key, nil) removes the key
			  remove(key)                     -> boolean (was the key there)
			  contains(key)                   -> boolean
			  keys([prefix])                  -> array of strings, sorted alphabetically
			  size()                          -> number of keys
			  clear()                         -> nil
			  move(old, new)                  -> boolean; silent overwrite, keeps the value identity
			  copy(src, dst)                  -> boolean; silent overwrite, copies as a deep clone
			  save()                          -> rewrites the whole store; needed after mutating a value
			                                 obtained from get() in place. No-op for session stores.
			  reload()                        -> re-reads the backend, DISCARDING unsaved in-memory changes
			  path                            -> absolute file path for store.file, nil otherwise
			  import(path)                    -> true, count; merges a JSON object over the existing keys
			  export(path)                    -> true, count; writes every key as a JSON object

			Paths in store.file/import/export are relative to the working directory unless absolute.
			Missing directories are created. Every change is written immediately, except the in-place
			mutation case above.

			VALUE LIMITS (store.chat and store.file only):
			  only structured data survives - tables, strings, numbers, booleans.
			  functions, userdata, threads, metatables and non-string table keys are DROPPED with a
			  warning that names the path of the lost value.
			  cyclic tables and nesting deeper than 200 levels raise an error.

			ERRORS: a store never guesses. A malformed JSON file is renamed to <name>.<timestamp>.broken,
			the store starts empty and the reason is logged. import() raises an error instead and leaves
			the source file untouched.

			EXAMPLES:

			  -- A counter that survives script reloads, per chat, living in a running application
			  local hp = store.session.get_or_add("hp", 100)
			  hp = hp - 10
			  store.session.set("hp", hp)

			  -- Shared between chats, gone after restart
			  store.appsession.get_or_add("last_agent", "default")

			  -- Follows the chat
			  store.chat.set("draft", { title = "Untitled", tags = { "a", "b" } })

			  -- A file in the working directory
			  local s = store.file(".llmassist/notes.json")
			  s.set("intro", "hello")
			  for _, key in ipairs(s.keys()) do print(key) end

			  -- Named stores
			  store.workdir("dnd").set("character1", { name = "Tav", level = 3 })
			  store.user("global").set("theme", "dark")
			""";

		/// <inheritdoc />
		public override Action? Populate(LuaTable globals, LuaTable ns, LuaService luaService)
		{
			ns["session"] = CreateStoreTable(sessionStore.Session);
			ns["appsession"] = CreateStoreTable(registry.AppSession);
			ns["chat"] = CreateStoreTable(chatStore.GetStore());
			ns["file"] = new LuaCallbackFunction(File);
			ns["workdir"] = CreateNamedStoreTable("store.workdir()", ResolveWorkdirStore);
			ns["user"] = CreateNamedStoreTable("store.user()", ResolveUserStore);
			return null;
		}

		private LuaTuple File(LuaCallingContext ctx, LuaValue[] args)
		{
			var path = RequireString("store.file()", args, 0, "path");
			if (path.Length == 0)
				throw new LuaRuntimeException("store.file(): the path must not be empty.");

			var fullPath = workingDirectory.GetFullPath(path);
			if (Directory.Exists(fullPath))
				throw new LuaRuntimeException($"store.file(): '{path}' is a directory, not a file.");

			return new LuaTuple(CreateStoreTable(registry.GetFileStore(fullPath)));
		}

		private LuaStore ResolveWorkdirStore(string name)
		{
			var directory = Path.Combine(workingDirectory.GetFullPath(string.Empty), Directories.WorkingHome, WorkdirStoreRoot);
			return registry.GetFileStore(Path.Combine(directory, EncodeStoreName("store.workdir()", name) + ".json"));
		}

		private LuaStore ResolveUserStore(string name)
		{
			var directory = Path.Combine(Directories.LocalAppData, UserStoreRoot);
			return registry.GetFileStore(Path.Combine(directory, EncodeStoreName("store.user()", name) + ".json"));
		}

		private LuaTable CreateNamedStoreTable(string function, Func<string, LuaStore> factory)
		{
			var table = CreateStoreTable(factory(DefaultStoreName));

			var metatable = new LuaMetatable();
			metatable.Set(LuaMetatableEvent.Call, new LuaCallbackFunction((ctx, args) =>
			{
				var name = args.Length > 1 && args[1] is not LuaNil
					? RequireString(function, args, 1, "name")
					: DefaultStoreName;

				return new LuaTuple(CreateNamedStoreTable(function, factory, name));
			}));

			table.Metatable = metatable;
			return table;
		}

		private LuaTable CreateNamedStoreTable(string function, Func<string, LuaStore> factory, string name)
		{
			var table = CreateStoreTable(factory(name));

			var metatable = new LuaMetatable();
			metatable.Set(LuaMetatableEvent.Call, new LuaCallbackFunction((ctx, args) =>
			{
				var next = args.Length > 1 && args[1] is not LuaNil
					? RequireString(function, args, 1, "name")
					: DefaultStoreName;

				return new LuaTuple(CreateNamedStoreTable(function, factory, next));
			}));

			table.Metatable = metatable;
			return table;
		}

		private LuaTable CreateStoreTable(LuaStore store)
		{
			var table = new LuaTable
			{
				["get"] = new LuaCallbackFunction((ctx, args) =>
					new LuaTuple(store.Get(RequireString("store.get()", args, 0, "key")))),

				["get_or_add"] = new LuaCallbackFunction((ctx, args) =>
				{
					var key = RequireString("store.get_or_add()", args, 0, "key");
					var defaultValue = args.Length > 1 && args[1] is not LuaNil ? args[1] : new LuaTable();
					return new LuaTuple(store.GetOrAdd(key, defaultValue, ctx.Warn));
				}),

				["set"] = new LuaCallbackFunction((ctx, args) =>
				{
					if (args.Length < 2)
						throw new LuaRuntimeException("store.set(key, value): at least 2 arguments expected.");

					store.Set(RequireString("store.set()", args, 0, "key"), args[1], ctx.Warn);
					return new LuaTuple(LuaNil.Instance);
				}),

				["remove"] = new LuaCallbackFunction((ctx, args) =>
					new LuaTuple(LuaBoolean.FromBoolean(
						store.Remove(RequireString("store.remove()", args, 0, "key"), ctx.Warn)))),

				["save"] = new LuaCallbackFunction((ctx, args) =>
				{
					store.Save(ctx.Warn);
					return new LuaTuple(LuaNil.Instance);
				}),

				["contains"] = new LuaCallbackFunction((ctx, args) =>
					new LuaTuple(LuaBoolean.FromBoolean(
						store.Contains(RequireString("store.contains()", args, 0, "key"))))),

				["move"] = new LuaCallbackFunction((ctx, args) =>
					new LuaTuple(LuaBoolean.FromBoolean(
						store.Move(RequireKeyPair("store.move()", args), RequireString("store.move()", args, 1, "new key"), ctx.Warn)))),

				["copy"] = new LuaCallbackFunction((ctx, args) =>
					new LuaTuple(LuaBoolean.FromBoolean(
						store.Copy(RequireKeyPair("store.copy()", args), RequireString("store.copy()", args, 1, "destination key"), ctx.Warn)))),

				["keys"] = new LuaCallbackFunction((ctx, args) =>
				{
					var prefix = args.Length > 0 && args[0] is not LuaNil
						? RequireString("store.keys()", args, 0, "prefix")
						: string.Empty;

					return new LuaTuple(store.Keys(prefix));
				}),

				["size"] = new LuaCallbackFunction((ctx, args) => new LuaTuple(new LuaNumber(store.Size()))),

				["clear"] = new LuaCallbackFunction((ctx, args) =>
				{
					store.Clear(ctx.Warn);
					return new LuaTuple(LuaNil.Instance);
				}),

				["reload"] = new LuaCallbackFunction((ctx, args) =>
				{
					store.Reload();
					return new LuaTuple(LuaNil.Instance);
				}),

				["import"] = new LuaCallbackFunction((ctx, args) =>
				{
					var path = ResolveDataPath("store.import()", args);
					try
					{
						return new LuaTuple(LuaBoolean.True, new LuaNumber(store.Import(path, ctx.Warn)));
					}
					catch (Exception exception) when (exception is System.Text.Json.JsonException or FormatException)
					{
						throw new LuaRuntimeException($"store.import(): '{path}' is not a valid store file: {exception.Message}");
					}
				}),

				["export"] = new LuaCallbackFunction((ctx, args) =>
				{
					var path = ResolveDataPath("store.export()", args);
					return new LuaTuple(LuaBoolean.True, new LuaNumber(store.Export(path, ctx.Warn)));
				}),

				["path"] = store.FilePath is null ? LuaNil.Instance : new LuaString(store.FilePath),
			};

			return table;
		}

		private string ResolveDataPath(string function, LuaValue[] args)
		{
			var path = RequireString(function, args, 0, "path");
			if (path.Length == 0)
				throw new LuaRuntimeException($"{function}: the path must not be empty.");

			var fullPath = workingDirectory.GetFullPath(path);
			if (Directory.Exists(fullPath))
				throw new LuaRuntimeException($"{function}: '{path}' is a directory, not a file.");

			return fullPath;
		}

		private static string RequireKeyPair(string function, LuaValue[] args)
		{
			if (args.Length < 2)
				throw new LuaRuntimeException($"{function}: at least 2 arguments expected.");

			return RequireString(function, args, 0, "key");
		}

		private static string RequireString(string function, LuaValue[] args, int index, string name)
		{
			if (args.Length <= index || args[index] is not LuaString value)
				throw new LuaRuntimeException($"{function}: argument {index + 1} must be a string ({name}).");

			return value.Value;
		}

		private static string EncodeStoreName(string function, string name)
		{
			if (string.IsNullOrEmpty(name))
				throw new LuaRuntimeException($"{function}: the store name must not be empty.");

			if (name is "." or "..")
				throw new LuaRuntimeException($"{function}: '{name}' is not a valid store name.");

			var encoded = new StringBuilder(name.Length);

			foreach (var b in Encoding.UTF8.GetBytes(name))
			{
				var c = (char)b;
				if (b < 0x80 && (char.IsAsciiLetterOrDigit(c) || c is '.' or '_' or '-'))
					encoded.Append(c);
				else
					encoded.Append('%').Append(b.ToString("X2"));
			}

			if (encoded.Length <= 120)
				return encoded.ToString();

			var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(name)))[..8];
			return $"{encoded.ToString(0, 100)}~{hash}";
		}
	}
}
