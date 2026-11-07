using AsyncLua;
using AsyncLua.Values;
using LLMDesktopAssistant.StructuredValues.Converters;

namespace LLMDesktopAssistant.Scripting.Lua.Store
{
	/// <summary>
	/// A live key table that either lives only in memory (<c>store.session</c>, <c>store.appsession</c>)
	/// or is mirrored into a backend on every change (<c>store.chat</c>, <c>store.file</c>).
	/// </summary>
	/// <remarks>
	/// Reading shares and writing copies. <see cref="Get"/> hands out the live value, so mutating it is
	/// visible to every later read; <see cref="Set"/> on an in-memory store stores a deep copy, so the
	/// caller's table can never alias a stored one. Persisted stores keep the caller's table as-is
	/// (its identity is the store's identity) and write a lossy snapshot of it.
	/// </remarks>
	public sealed class LuaStore(string name, ILuaStoreBackend? backend)
	{
		private readonly Lock _lock = new();
		private LuaTable _data = backend?.Load() ?? new LuaTable();

		/// <summary>
		/// Gets the name used in diagnostics, e.g. <c>store.session</c> or <c>store.file(&lt;path&gt;)</c>.
		/// </summary>
		public string Name { get; } = name;

		/// <summary>
		/// Gets the absolute path of the backing file, or <see langword="null"/> when there is none.
		/// </summary>
		public string? FilePath => backend?.FilePath;

		/// <summary>
		/// Gets the live value stored under the key, or <c>nil</c> when the key is missing.
		/// </summary>
		/// <param name="key">The key to look up.</param>
		/// <returns>The live value, never <see langword="null"/>.</returns>
		public LuaValue Get(string key)
		{
			lock (_lock)
				return _data.Get(key);
		}

		/// <summary>
		/// Gets the live value stored under the key, writing the given default when the key is missing.
		/// </summary>
		/// <param name="key">The key to look up.</param>
		/// <param name="defaultValue">The value to write when the key is missing.</param>
		/// <param name="warn">Receives one message per value that could not be persisted.</param>
		/// <returns>The live value now stored under the key.</returns>
		public LuaValue GetOrAdd(string key, LuaValue defaultValue, Action<string>? warn)
		{
			lock (_lock)
			{
				if (_data.TryGetValue(new LuaString(key), out var existing))
					return existing;

				var stored = backend is null ? LuaStoreValueConverter.Clone(defaultValue) : defaultValue;
				_data.Set(key, stored);
				Persist(warn);
				return stored;
			}
		}

		/// <summary>
		/// Stores the value under the key, replacing whatever was there.
		/// </summary>
		/// <param name="key">The key to write.</param>
		/// <param name="value">The value to store; <c>nil</c> removes the key.</param>
		/// <param name="warn">Receives one message per value that could not be persisted.</param>
		public void Set(string key, LuaValue value, Action<string>? warn)
		{
			lock (_lock)
			{
				if (value is LuaNil)
				{
					if (_data.Remove(key))
						Persist(warn);
					return;
				}

				_data.Set(key, backend is null ? LuaStoreValueConverter.Clone(value) : value);
				Persist(warn);
			}
		}

		/// <summary>
		/// Removes the key.
		/// </summary>
		/// <param name="key">The key to remove.</param>
		/// <param name="warn">Receives one message per value that could not be persisted.</param>
		/// <returns><see langword="true"/> when the key was there.</returns>
		public bool Remove(string key, Action<string>? warn)
		{
			lock (_lock)
			{
				if (!_data.Remove(key))
					return false;

				Persist(warn);
				return true;
			}
		}

		/// <summary>
		/// Rewrites the whole store; the only way to persist an in-place mutation of a live value.
		/// </summary>
		/// <param name="warn">Receives one message per value that could not be persisted.</param>
		public void Save(Action<string>? warn)
		{
			lock (_lock)
				Persist(warn);
		}

		/// <summary>
		/// Determines whether the key is present.
		/// </summary>
		/// <param name="key">The key to check.</param>
		/// <returns><see langword="true"/> when the key is present.</returns>
		public bool Contains(string key)
		{
			lock (_lock)
				return _data.ContainsKey(new LuaString(key));
		}

		/// <summary>
		/// Moves the value to a different key, keeping its identity.
		/// </summary>
		/// <param name="oldKey">The key to move from.</param>
		/// <param name="newKey">The key to move to; an existing value there is overwritten.</param>
		/// <param name="warn">Receives one message per value that could not be persisted.</param>
		/// <returns><see langword="false"/> when <paramref name="oldKey"/> is missing.</returns>
		public bool Move(string oldKey, string newKey, Action<string>? warn)
		{
			lock (_lock)
			{
				if (!_data.TryGetValue(new LuaString(oldKey), out var value))
					return false;

				if (oldKey == newKey)
					return true;

				_data.Remove(oldKey);
				_data.Set(newKey, value);
				Persist(warn);
				return true;
			}
		}

		/// <summary>
		/// Copies the value to a different key as a deep clone.
		/// </summary>
		/// <param name="sourceKey">The key to copy from.</param>
		/// <param name="destinationKey">The key to copy to; an existing value there is overwritten.</param>
		/// <param name="warn">Receives one message per value that could not be persisted.</param>
		/// <returns><see langword="false"/> when <paramref name="sourceKey"/> is missing.</returns>
		public bool Copy(string sourceKey, string destinationKey, Action<string>? warn)
		{
			lock (_lock)
			{
				if (!_data.TryGetValue(new LuaString(sourceKey), out var value))
					return false;

				_data.Set(destinationKey, LuaStoreValueConverter.Clone(value));
				Persist(warn);
				return true;
			}
		}

		/// <summary>
		/// Lists the keys, filtered by a prefix.
		/// </summary>
		/// <param name="prefix">The prefix every returned key must start with; empty lists everything.</param>
		/// <returns>A Lua array of keys, sorted alphabetically.</returns>
		/// <remarks>
		/// Sorted, not insertion-ordered: a store that survives a reload is rebuilt from a serialized
		/// snapshot, and that snapshot has no order to preserve. A stable answer beats a lost one.
		/// </remarks>
		public LuaTable Keys(string prefix)
		{
			var matches = new List<string>();

			lock (_lock)
			{
				foreach (var kvp in _data.Entries)
				{
					if (kvp.Key is not LuaString key)
						continue;

					if (prefix.Length > 0 && !key.Value.StartsWith(prefix, StringComparison.Ordinal))
						continue;

					matches.Add(key.Value);
				}
			}

			matches.Sort(StringComparer.Ordinal);

			var result = new LuaTable();
			for (int i = 0; i < matches.Count; i++)
				result[i + 1] = new LuaString(matches[i]);

			return result;
		}

		/// <summary>
		/// Gets the number of keys in the store.
		/// </summary>
		/// <returns>The key count.</returns>
		public int Size()
		{
			lock (_lock)
				return _data.Count;
		}

		/// <summary>
		/// Removes every key.
		/// </summary>
		/// <param name="warn">Receives one message per value that could not be persisted.</param>
		public void Clear(Action<string>? warn)
		{
			lock (_lock)
			{
				_data.Clear();
				Persist(warn);
			}
		}

		/// <summary>
		/// Re-reads the store from its backend, discarding every unsaved in-memory change.
		/// </summary>
		public void Reload()
		{
			lock (_lock)
				_data = backend?.Load() ?? new LuaTable();
		}

		/// <summary>
		/// Merges the keys of a JSON object into the store, overwriting the keys it names.
		/// </summary>
		/// <param name="path">The absolute path of the JSON file.</param>
		/// <param name="warn">Receives one message per value that could not be persisted.</param>
		/// <returns>The number of imported keys.</returns>
		public int Import(string path, Action<string>? warn)
		{
			var source = LuaStoreJson.ReadFile(path, quarantine: false);

			lock (_lock)
			{
				foreach (var kvp in source.Items)
					_data.Set(kvp.Key, kvp.Value.ToLuaValue());

				Persist(warn);
				return source.Items.Count;
			}
		}

		/// <summary>
		/// Writes every key of the store into a JSON file.
		/// </summary>
		/// <param name="path">The absolute path of the JSON file.</param>
		/// <param name="warn">Receives one message per value that could not be persisted.</param>
		/// <returns>The number of exported keys.</returns>
		public int Export(string path, Action<string>? warn)
		{
			lock (_lock)
			{
				var dropped = new List<string>();
				var snapshot = LuaStoreValueConverter.ToSnapshot(_data, dropped);
				LuaFileStoreBackend.ReportDropped(Name, dropped, warn);
				LuaStoreJson.WriteFile(path, snapshot);
				return snapshot.Items.Count;
			}
		}

		private void Persist(Action<string>? warn)
		{
			backend?.Persist(_data, warn);
		}
	}
}
