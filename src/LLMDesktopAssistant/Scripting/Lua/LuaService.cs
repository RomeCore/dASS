using System.Text;
using AsyncLua;
using AsyncLua.Values;
using LLMDesktopAssistant.Addons;
using LLMDesktopAssistant.LLM.Services;
using LLMDesktopAssistant.StructuredValues.Converters;
using Serilog;

namespace LLMDesktopAssistant.Scripting.Lua
{
	[ChatService]
	public class LuaService : Disposable
	{
		private readonly LuaState _lua;
		private readonly List<string?> _namespaces;

		private readonly Lock _lock = new();
		private readonly IAddonSetCollector<LuaScriptInfo> _scriptSetCollector;
		private readonly AddonSet<LuaScriptInfo> _scripts;
		private readonly LuaTable _globalTableSnapshot;
		private readonly Dictionary<LuaType, LuaMetatable> _typeMetatablesSnapshot;
		private readonly List<string?> _namespacesSnapshot;
		private readonly Stack<Action> _cleanups;

		/// <summary>
		/// Gets the list of namespaces available in Lua.
		/// </summary>
		public IReadOnlyList<string?> Namespaces { get; }

		public LuaService(IAddonSetCollector<LuaScriptInfo> scriptSetCollector)
		{
			_scriptSetCollector = scriptSetCollector;
			_scripts = scriptSetCollector.GetAddonsForChat();
			_scripts.Addons.CollectionChanged += ScriptsCollectionChanged;

			_lua = new LuaState().LoadDefaultLibraries();
			_namespaces = [ null ];
			_cleanups = [];

			Namespaces = _namespaces.AsReadOnly();

			_lua.Globals.Set(LuaVariables.NamespaceApiMarker, LuaBoolean.True);
			_lua.Globals.Set(LuaVariables.NamespacePartPath, new LuaString(LuaVariables.GlobalTable));
			_lua.Globals.Set(LuaVariables.NamespaceFullPath, new LuaString(LuaVariables.GlobalTable));

			_globalTableSnapshot = _lua.Globals.DeepClone();
			_namespacesSnapshot = [.. _namespaces];
			_typeMetatablesSnapshot = _lua.TypeMetatables.ToDictionary();

			RefreshAllScripts();
		}

		private void ScriptsCollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
		{
			RefreshAllScripts();
		}

		protected override void Dispose(bool disposing)
		{
			base.Dispose(disposing);

			if (disposing)
			{
				Cleanup();
				_scripts.Addons.CollectionChanged -= ScriptsCollectionChanged;
			}
		}

		private void LoadScript(LuaScriptInfo script)
		{
			var globals = _lua.Globals;
			var ns = script.Namespace != null ? ResolveNamespace(script.Namespace) : null;
			var parameters = ScriptAddonParameters.Resolve(script.ParameterSchema, script.Change);

			// The load is the script's only input surface: `params` exists while the body runs and never leaks
			// into the shared state or into the next script.
			if (parameters is not null)
				globals["params"] = parameters.ToLuaValue();

			try
			{
				var cleanup = script.Loader(globals, ns, this);
				if (cleanup is not null)
					_cleanups.Push(cleanup);
			}
			finally
			{
				globals.Remove("params");
			}

			if (ns is not null)
			{
				var manuals = ns.Get(LuaVariables.NamespaceManuals);
				if (manuals is not LuaTable manualsTable)
				{
					manualsTable = new LuaTable();
					ns.Set(LuaVariables.NamespaceManuals, manualsTable);
				}
				var apiManuals = script.Manuals;
				if (apiManuals != null)
					manualsTable.Append(new LuaString(apiManuals));
			}
		}

		private void Cleanup()
		{
			while (_cleanups.TryPop(out var cleanup))
			{
				try
				{
					cleanup();
				}
				catch (Exception ex)
				{
					Log.Error(ex, "Error during script cleanup: {Error}", ex);
				}
			}
		}

		private void ResetEnvironment()
		{
			Cleanup();

			_lua.Globals.Clear();
			foreach (var kvp in _globalTableSnapshot.Entries)
			{
				var key = kvp.Key;
				var value = kvp.Value;
				_lua.Globals.Set(key, value);
			}
			_lua.Globals.Set(LuaVariables.GlobalTable, _lua.Globals);
			_lua.TypeMetatables.Clear();
			foreach (var kvp in _typeMetatablesSnapshot)
			{
				_lua.TypeMetatables[kvp.Key] = kvp.Value.DeepClone();
			}
			_namespaces.Clear();
			_namespaces.AddRange(_namespacesSnapshot);
		}

		/// <summary>
		/// Drops the shared Lua state back to the snapshot taken before any script loaded and runs every script addon
		/// again, the native API addons included.
		/// </summary>
		/// <remarks>
		/// This is the way back from a script that overwrote an API member - <c>dass.chat.get_messages</c> being the
		/// classic casualty - because the state belongs to this service and no Lua-side code can rebuild it. A refresh
		/// already in progress makes the call a no-op, since the running refresh is about to do exactly this.
		/// </remarks>
		public void RefreshAllScripts()
		{
			if (!_lock.TryEnter())
				return;

			try
			{
				ResetEnvironment();

				foreach (var script in _scripts)
				{
					try
					{
						LoadScript(script);
					}
					catch (Exception ex)
					{
						Log.Error(ex, "Failed to load script '{Script}': {Error}", script.Name, ex);
					}
				}
			}
			catch (Exception ex)
			{
				Log.Error(ex, "Failed to refresh scripts: {Error}", ex);
			}
			finally
			{
				_lock.Exit();
			}
		}

		/// <summary>
		/// Resolves a Lua namespace to a table if it exists.
		/// </summary>
		/// <param name="namespaceName">The Lua namespace string to resolve.</param>
		/// <returns>The resolved Lua table, or null if the namespace does not exist.</returns>
		public LuaTable? TryResolveNamespace(string namespaceName)
		{
			var parts = namespaceName.Split(['.'], StringSplitOptions.RemoveEmptyEntries);
			var result = _lua.Globals;

			foreach (var part in parts)
			{
				var next = result.Get(part);
				if (next is not LuaTable nextTable)
					return null;
				result = nextTable;
			}

			return result;
		}

		/// <summary>
		/// Resolves a Lua namespace to a table.
		/// </summary>
		/// <param name="namespaceName">The Lua namespace string to resolve.</param>
		/// <returns>The resolved Lua table.</returns>
		public LuaTable ResolveNamespace(string namespaceName)
		{
			var parts = namespaceName.Split(['.'], StringSplitOptions.RemoveEmptyEntries);
			var result = _lua.Globals;

			var accumulatedPath = new StringBuilder();
			foreach (var part in parts)
			{
				var next = result.Get(part);

				if (accumulatedPath.Length > 0)
					accumulatedPath.Append('.');
				accumulatedPath.Append(part);

				if (next is not LuaTable table)
				{
					table = new LuaTable();
					_namespaces.Add(accumulatedPath.ToString());
					table.Set(LuaVariables.NamespaceApiMarker, LuaBoolean.True);
					table.Set(LuaVariables.NamespacePartPath, new LuaString(part));
					table.Set(LuaVariables.NamespaceFullPath, new LuaString(accumulatedPath.ToString()));
					result.Set(part, table);
				}
				else if (!table.Get(LuaVariables.NamespaceApiMarker).ToBoolean())
				{
					_namespaces.Add(accumulatedPath.ToString());
					table.Set(LuaVariables.NamespaceApiMarker, LuaBoolean.True);
					table.Set(LuaVariables.NamespacePartPath, new LuaString(part));
					table.Set(LuaVariables.NamespaceFullPath, new LuaString(accumulatedPath.ToString()));
				}

				result = table;
			}

			return result;
		}

		/// <summary>
		/// Gets the current Lua runtime state.
		/// </summary>
		public LuaState GetState() => _lua;

		/// <summary>
		/// Creates a snapshot of the current Lua runtime.
		/// </summary>
		/// <returns>A new Lua runtime with a copy of the current global table.</returns>
		public LuaState CreateSnapshotRuntime()
		{
			return _lua.CreateSnapshot();
		}

		/// <summary>
		/// Executes the provided Lua code.
		/// </summary>
		/// <param name="lua">The Lua code to execute.</param>
		/// <param name="modifyGlobals">Action used to modify cloned _G table. If not null, the globals will be cloned and passed to this action, then passed to Lua.</param>
		/// <returns>The result of the Lua execution.</returns>
		public LuaTuple Execute(string lua, Action<LuaTable>? modifyGlobals = null,
			CancellationToken cancellationToken = default)
		{
			var globals = _lua.Globals;
			if (modifyGlobals != null)
			{
				globals = globals.ShallowClone();
				modifyGlobals(globals);
				globals.Set(LuaVariables.GlobalTable, globals);
			}
			return _lua.Execute(lua, editContext: ctx =>
			{
				ctx.Globals = globals;
			}, cancellationToken: cancellationToken);
		}

		/// <summary>
		/// Executes the provided Lua code and captures any output generated by print statements.
		/// </summary>
		/// <param name="lua">The Lua code to execute.</param>
		/// <param name="printOutput">The list to capture output into.</param>
		/// <param name="modifyGlobals">Action used to modify cloned _G table. If not null, the globals will be cloned and passed to this action, then passed to Lua.</param>
		/// <returns>The result of the Lua execution.</returns>
		public LuaTuple Execute(string lua, Action<string> printOutput, Action<LuaTable>? modifyGlobals = null,
			CancellationToken cancellationToken = default)
		{
			var globals = _lua.Globals;
			if (modifyGlobals != null)
			{
				globals = globals.ShallowClone();
				modifyGlobals(globals);
				globals.Set(LuaVariables.GlobalTable, globals);
			}
			return _lua.Execute(lua, editContext: ctx =>
			{
				ctx.Globals = globals;
				ctx.Print = printOutput;
			}, cancellationToken: cancellationToken);
		}

		/// <summary>
		/// Executes the provided Lua code.
		/// </summary>
		/// <param name="lua">The Lua code to execute.</param>
		/// <param name="modifyGlobals">Action used to modify cloned _G table. If not null, the globals will be cloned and passed to this action, then passed to Lua.</param>
		/// <returns>The result of the Lua execution.</returns>
		public Task<LuaTuple> ExecuteAsync(string lua, Action<LuaTable>? modifyGlobals = null,
			CancellationToken cancellationToken = default)
		{
			var globals = _lua.Globals;
			if (modifyGlobals != null)
			{
				globals = globals.ShallowClone();
				modifyGlobals(globals);
				globals.Set(LuaVariables.GlobalTable, globals);
			}
			return _lua.ExecuteAsync(lua, editContext: ctx =>
			{
				ctx.Globals = globals;
			}, cancellationToken: cancellationToken);
		}

		/// <summary>
		/// Executes the provided Lua code and captures any output generated by print statements.
		/// </summary>
		/// <param name="lua">The Lua code to execute.</param>
		/// <param name="printOutput">The list to capture output into.</param>
		/// <param name="modifyGlobals">Action used to modify cloned _G table. If not null, the globals will be cloned and passed to this action, then passed to Lua.</param>
		/// <returns>The result of the Lua execution.</returns>
		public Task<LuaTuple> ExecuteAsync(string lua, Action<string> printOutput, Action<LuaTable>? modifyGlobals = null,
			CancellationToken cancellationToken = default)
		{
			var globals = _lua.Globals;
			if (modifyGlobals != null)
			{
				globals = globals.ShallowClone();
				modifyGlobals(globals);
				globals.Set(LuaVariables.GlobalTable, globals);
			}
			return _lua.ExecuteAsync(lua, editContext: ctx =>
			{
				ctx.Globals = globals;
				ctx.Print = printOutput;
			}, cancellationToken: cancellationToken);
		}
	}
}