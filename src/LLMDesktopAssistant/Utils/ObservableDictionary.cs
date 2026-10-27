using System.Collections;
using System.Collections.Specialized;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.Serialization;
using System.Text.Json.Serialization;
using LiteDB;

namespace LLMDesktopAssistant.Utils
{
	/// <summary>
	/// A thread-safe ObservableDictionary that notifies when entries are added, removed or replaced.
	/// </summary>
	/// <typeparam name="TKey">The type of keys in the dictionary.</typeparam>
	/// <typeparam name="TValue">The type of values in the dictionary.</typeparam>
	public class ObservableDictionary<TKey, TValue> : NotifyPropertyChanged,
		IDictionary<TKey, TValue>, IReadOnlyDictionary<TKey, TValue>, IDictionary, INotifyCollectionChanged
		where TKey : notnull
	{
		private readonly Dictionary<TKey, TValue> _items;
		private readonly object _lock;
		private volatile int _count;

		/// <summary>
		/// Gets the dictionary that backs this collection.
		/// </summary>
		protected Dictionary<TKey, TValue> Items => _items;

		/// <summary>
		/// Gets the <see cref="IEqualityComparer{T}"/> that is used to determine equality of keys for the dictionary.
		/// </summary>
		public IEqualityComparer<TKey> Comparer => _items.Comparer;

		/// <summary>
		/// Gets the number of key/value pairs contained in the <see cref="ObservableDictionary{TKey, TValue}"/>.
		/// </summary>
		[BsonIgnore]
		[JsonIgnore]
		[IgnoreDataMember]
		public int Count => _count;

		/// <summary>
		/// Gets a value indicating whether the dictionary contains any entries.
		/// </summary>
		[BsonIgnore]
		[JsonIgnore]
		[IgnoreDataMember]
		public bool Any => _count > 0;

		/// <summary>
		/// Gets a value indicating whether the <see cref="ObservableDictionary{TKey, TValue}"/> is read-only.
		/// </summary>
		[BsonIgnore]
		[JsonIgnore]
		[IgnoreDataMember]
		public bool IsReadOnly => false;

		/// <summary>
		/// Gets a value indicating whether the <see cref="ObservableDictionary{TKey, TValue}"/> has a fixed size.
		/// </summary>
		[BsonIgnore]
		[JsonIgnore]
		[IgnoreDataMember]
		public bool IsFixedSize => false;

		/// <summary>
		/// Gets a value indicating whether access to the <see cref="ObservableDictionary{TKey, TValue}"/> is synchronized (thread-safe).
		/// </summary>
		[BsonIgnore]
		[JsonIgnore]
		[IgnoreDataMember]
		public bool IsSynchronized => true;

		/// <summary>
		/// Gets an object that can be used to synchronize access to the <see cref="ObservableDictionary{TKey, TValue}"/>.
		/// </summary>
		[BsonIgnore]
		[JsonIgnore]
		[IgnoreDataMember]
		public object SyncRoot => _lock;

		/// <summary>
		/// Gets or sets the value associated with the specified key.
		/// If the key does not exist, a new entry is added; otherwise the existing entry is replaced.
		/// </summary>
		/// <param name="key">The key of the value to get or set.</param>
		/// <returns>The value associated with the specified key.</returns>
		/// <exception cref="ArgumentNullException"><paramref name="key"/> is <see langword="null"/>.</exception>
		/// <exception cref="KeyNotFoundException">The property is retrieved and <paramref name="key"/> does not exist.</exception>
		public virtual TValue this[TKey key]
		{
			get
			{
				lock (_lock)
					return _items[key];
			}
			set
			{
				bool exists;
				TValue oldValue;

				lock (_lock)
				{
					exists = _items.TryGetValue(key, out oldValue!);
					_items[key] = value;
					_count = _items.Count;
				}

				if (exists)
					RaiseChangedEvents(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Replace,
						new KeyValuePair<TKey, TValue>(key, value),
						new KeyValuePair<TKey, TValue>(key, oldValue)));
				else
					RaiseChangedEvents(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add,
						new KeyValuePair<TKey, TValue>(key, value)));
			}
		}
		object? IDictionary.this[object key]
		{
			get => this[(TKey)key];
			set => this[(TKey)key] = (TValue)value!;
		}

		/// <summary>
		/// Gets a snapshot collection containing the keys of the <see cref="ObservableDictionary{TKey, TValue}"/>.
		/// The collection is a copy of the current state and does not reflect subsequent changes.
		/// </summary>
		public ICollection<TKey> Keys
		{
			get
			{
				lock (_lock)
					return _items.Keys.ToList();
			}
		}

		/// <summary>
		/// Gets a snapshot collection containing the values of the <see cref="ObservableDictionary{TKey, TValue}"/>.
		/// The collection is a copy of the current state and does not reflect subsequent changes.
		/// </summary>
		public ICollection<TValue> Values
		{
			get
			{
				lock (_lock)
					return _items.Values.ToList();
			}
		}

		IEnumerable<TKey> IReadOnlyDictionary<TKey, TValue>.Keys => Keys;

		IEnumerable<TValue> IReadOnlyDictionary<TKey, TValue>.Values => Values;

		ICollection IDictionary.Keys
		{
			get
			{
				lock (_lock)
					return _items.Keys.ToList();
			}
		}

		ICollection IDictionary.Values
		{
			get
			{
				lock (_lock)
					return _items.Values.ToList();
			}
		}

		/// <summary>
		/// Gets or sets a value indicating whether to use snapshot enumeration for <see cref="GetEnumerator"/> method.
		/// </summary>
		public bool UseSnapshotEnumeration { get; set; } = true;

		/// <summary>
		/// Gets or sets a value indicating whether to prefer a <see cref="NotifyCollectionChangedAction.Reset"/> for range operations.
		/// If set to false, this collection will never raise <see cref="NotifyCollectionChangedAction.Reset"/> for any operations.
		/// </summary>
		public bool PreferResetForRangeOperations { get; set; } = false;

		/// <summary>
		/// Gets or sets a value indicating whether to raise events in the UI thread.
		/// </summary>
		public bool RaiseInUIThread { get; set; } = false;

		/// <summary>
		/// The event that is raised when the dictionary changes.
		/// </summary>
		public event NotifyCollectionChangedEventHandler? CollectionChanged;

		/// <summary>
		/// Initializes a new instance of the <see cref="ObservableDictionary{TKey, TValue}"/> class that is empty.
		/// </summary>
		public ObservableDictionary()
		{
			_items = [];
			_lock = new();
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="ObservableDictionary{TKey, TValue}"/> class that is empty
		/// and uses the specified <see cref="IEqualityComparer{T}"/> for keys.
		/// </summary>
		/// <param name="comparer">The <see cref="IEqualityComparer{T}"/> implementation to use when comparing keys.</param>
		public ObservableDictionary(IEqualityComparer<TKey> comparer)
		{
			_items = new(comparer);
			_lock = new();
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="ObservableDictionary{TKey, TValue}"/> class that contains
		/// elements copied from the specified <see cref="IDictionary{TKey, TValue}"/>.
		/// </summary>
		/// <param name="dictionary">The <see cref="IDictionary{TKey, TValue}"/> whose elements are copied to the new dictionary.</param>
		/// <exception cref="ArgumentNullException"><paramref name="dictionary"/> is <see langword="null"/>.</exception>
		/// <exception cref="ArgumentException"><paramref name="dictionary"/> contains one or more duplicate keys.</exception>
		public ObservableDictionary(IDictionary<TKey, TValue> dictionary)
		{
			if (dictionary == null)
				throw new ArgumentNullException(nameof(dictionary));

			_items = new(dictionary);
			_count = _items.Count;
			_lock = new();
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="ObservableDictionary{TKey, TValue}"/> class that contains
		/// elements copied from the specified sequence of key/value pairs.
		/// </summary>
		/// <param name="collection">The sequence of key/value pairs whose elements are copied to the new dictionary.</param>
		/// <exception cref="ArgumentNullException"><paramref name="collection"/> is <see langword="null"/>.</exception>
		/// <exception cref="ArgumentException"><paramref name="collection"/> contains one or more duplicate keys.</exception>
		public ObservableDictionary(IEnumerable<KeyValuePair<TKey, TValue>> collection)
		{
			if (collection == null)
				throw new ArgumentNullException(nameof(collection));

			_items = new(collection);
			_count = _items.Count;
			_lock = new();
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="ObservableDictionary{TKey, TValue}"/> class that contains
		/// elements copied from the specified <see cref="IDictionary{TKey, TValue}"/> and uses the specified
		/// <see cref="IEqualityComparer{T}"/> for keys.
		/// </summary>
		/// <param name="dictionary">The <see cref="IDictionary{TKey, TValue}"/> whose elements are copied to the new dictionary.</param>
		/// <param name="comparer">The <see cref="IEqualityComparer{T}"/> implementation to use when comparing keys.</param>
		/// <exception cref="ArgumentNullException"><paramref name="dictionary"/> is <see langword="null"/>.</exception>
		/// <exception cref="ArgumentException"><paramref name="dictionary"/> contains one or more duplicate keys.</exception>
		public ObservableDictionary(IDictionary<TKey, TValue> dictionary, IEqualityComparer<TKey> comparer)
		{
			if (dictionary == null)
				throw new ArgumentNullException(nameof(dictionary));

			_items = new(dictionary, comparer);
			_count = _items.Count;
			_lock = new();
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="ObservableDictionary{TKey, TValue}"/> class that contains
		/// elements copied from the specified sequence of key/value pairs and uses the specified
		/// <see cref="IEqualityComparer{T}"/> for keys.
		/// </summary>
		/// <param name="collection">The sequence of key/value pairs whose elements are copied to the new dictionary.</param>
		/// <param name="comparer">The <see cref="IEqualityComparer{T}"/> implementation to use when comparing keys.</param>
		/// <exception cref="ArgumentNullException"><paramref name="collection"/> is <see langword="null"/>.</exception>
		/// <exception cref="ArgumentException"><paramref name="collection"/> contains one or more duplicate keys.</exception>
		public ObservableDictionary(IEnumerable<KeyValuePair<TKey, TValue>> collection, IEqualityComparer<TKey> comparer)
		{
			if (collection == null)
				throw new ArgumentNullException(nameof(collection));

			_items = new(collection, comparer);
			_count = _items.Count;
			_lock = new();
		}

		/// <summary>
		/// Raises the <see cref="CollectionChanged"/> event with the provided arguments.
		/// </summary>
		/// <param name="e">The event arguments.</param>
		protected virtual void OnCollectionChanged(NotifyCollectionChangedEventArgs e)
		{
			CollectionChanged?.Invoke(this, e);
		}

		/// <summary>
		/// Raises the <see cref="PropertyChanged"/> and <see cref="CollectionChanged"/> events with the provided arguments.
		/// </summary>
		/// <param name="e">The event arguments.</param>
		protected virtual void RaiseChangedEvents(NotifyCollectionChangedEventArgs e)
		{
			if (RaiseInUIThread)
			{
				UIDispatcher.Invoke(() =>
				{
					OnCollectionChanged(e);
					RaisePropertyChanged(nameof(Count));
					RaisePropertyChanged(nameof(Any));
					RaisePropertyChanged(nameof(Keys));
					RaisePropertyChanged(nameof(Values));
					RaisePropertyChanged("Item[]");
				});
			}
			else
			{
				OnCollectionChanged(e);
				RaisePropertyChanged(nameof(Count));
				RaisePropertyChanged(nameof(Any));
				RaisePropertyChanged(nameof(Keys));
				RaisePropertyChanged(nameof(Values));
				RaisePropertyChanged("Item[]");
			}
		}

		/// <summary>
		/// Updates the count of entries in the dictionary.
		/// </summary>
		protected void UpdateCount()
		{
			_count = _items.Count;
		}

		/// <summary>
		/// Adds the specified key and value to the <see cref="ObservableDictionary{TKey, TValue}"/>.
		/// </summary>
		/// <param name="key">The key of the element to add.</param>
		/// <param name="value">The value of the element to add.</param>
		/// <exception cref="ArgumentNullException"><paramref name="key"/> is <see langword="null"/>.</exception>
		/// <exception cref="ArgumentException">An element with the same key already exists.</exception>
		public virtual void Add(TKey key, TValue value)
		{
			lock (_lock)
			{
				_items.Add(key, value);
				_count = _items.Count;
			}

			RaiseChangedEvents(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add,
				new KeyValuePair<TKey, TValue>(key, value)));
		}

		/// <summary>
		/// Adds the specified key/value pair to the <see cref="ObservableDictionary{TKey, TValue}"/>.
		/// </summary>
		/// <param name="item">The key/value pair to add.</param>
		/// <exception cref="ArgumentException">An element with the same key already exists.</exception>
		public virtual void Add(KeyValuePair<TKey, TValue> item)
		{
			Add(item.Key, item.Value);
		}
		void IDictionary.Add(object key, object? value)
		{
			Add((TKey)key, (TValue)value!);
		}

		/// <summary>
		/// Attempts to add the specified key and value to the <see cref="ObservableDictionary{TKey, TValue}"/>.
		/// </summary>
		/// <param name="key">The key of the element to add.</param>
		/// <param name="value">The value of the element to add.</param>
		/// <returns><see langword="true"/> if the key/value pair was added successfully; otherwise, <see langword="false"/>.</returns>
		public virtual bool TryAdd(TKey key, TValue value)
		{
			lock (_lock)
			{
				if (!_items.TryAdd(key, value))
					return false;
				_count = _items.Count;
			}

			RaiseChangedEvents(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add,
				new KeyValuePair<TKey, TValue>(key, value)));
			return true;
		}

		/// <summary>
		/// Adds a range of key/value pairs to the <see cref="ObservableDictionary{TKey, TValue}"/>.
		/// The operation is atomic: if any key already exists (or is duplicated within <paramref name="items"/>),
		/// no entries are added and an exception is thrown.
		/// </summary>
		/// <param name="items">The collection of key/value pairs to add.</param>
		/// <exception cref="ArgumentNullException"><paramref name="items"/> is <see langword="null"/>.</exception>
		/// <exception cref="ArgumentException">An item with the same key already exists in the dictionary or within <paramref name="items"/>.</exception>
		public virtual void AddRange(IEnumerable<KeyValuePair<TKey, TValue>> items)
		{
			if (items == null)
				throw new ArgumentNullException(nameof(items));

			var itemsList = items as List<KeyValuePair<TKey, TValue>> ?? items.ToList();
			if (itemsList.Count == 0)
				return;

			lock (_lock)
			{
				var seen = new HashSet<TKey>(_items.Comparer);
				foreach (var pair in itemsList)
				{
					if (!seen.Add(pair.Key) || _items.ContainsKey(pair.Key))
						throw new ArgumentException($"An item with the same key has already been added. Key: {pair.Key}", nameof(items));
				}

				foreach (var pair in itemsList)
					_items.Add(pair.Key, pair.Value);
				_count = _items.Count;
			}

			if (PreferResetForRangeOperations && itemsList.Count > 1)
				RaiseChangedEvents(EventArgsCache.ResetCollectionChanged);
			else
				RaiseChangedEvents(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, itemsList));
		}

		/// <summary>
		/// Removes the value with the specified key from the <see cref="ObservableDictionary{TKey, TValue}"/>.
		/// </summary>
		/// <param name="key">The key of the element to remove.</param>
		/// <returns><see langword="true"/> if the element was successfully removed; otherwise, <see langword="false"/>.</returns>
		public virtual bool Remove(TKey key)
		{
			TValue value;

			lock (_lock)
			{
				if (!_items.Remove(key, out value!))
					return false;
				_count = _items.Count;
			}

			RaiseChangedEvents(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove,
				new KeyValuePair<TKey, TValue>(key, value)));
			return true;
		}
		void IDictionary.Remove(object key)
		{
			if (key is TKey k)
				Remove(k);
		}

		/// <summary>
		/// Removes the value with the specified key from the <see cref="ObservableDictionary{TKey, TValue}"/> and
		/// copies the removed value to the <paramref name="value"/> parameter.
		/// </summary>
		/// <param name="key">The key of the element to remove.</param>
		/// <param name="value">The removed value.</param>
		/// <returns><see langword="true"/> if the element was successfully removed; otherwise, <see langword="false"/>.</returns>
		public virtual bool Remove(TKey key, out TValue value)
		{
			lock (_lock)
			{
				if (!_items.Remove(key, out value!))
					return false;
				_count = _items.Count;
			}

			RaiseChangedEvents(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove,
				new KeyValuePair<TKey, TValue>(key, value)));
			return true;
		}

		/// <summary>
		/// Removes the specified key/value pair from the <see cref="ObservableDictionary{TKey, TValue}"/>.
		/// The pair is removed only if both the key and the value match an existing entry.
		/// </summary>
		/// <param name="item">The key/value pair to remove.</param>
		/// <returns><see langword="true"/> if the pair was successfully removed; otherwise, <see langword="false"/>.</returns>
		public virtual bool Remove(KeyValuePair<TKey, TValue> item)
		{
			lock (_lock)
			{
				if (!_items.TryGetValue(item.Key, out var value) || !EqualityComparer<TValue>.Default.Equals(value, item.Value))
					return false;
				_items.Remove(item.Key);
				_count = _items.Count;
			}

			RaiseChangedEvents(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, item));
			return true;
		}

		/// <summary>
		/// Removes the values with the specified keys from the <see cref="ObservableDictionary{TKey, TValue}"/>.
		/// </summary>
		/// <param name="keys">The collection of keys to remove.</param>
		/// <exception cref="ArgumentNullException"><paramref name="keys"/> is <see langword="null"/>.</exception>
		public virtual void RemoveRange(IEnumerable<TKey> keys)
		{
			if (keys == null)
				throw new ArgumentNullException(nameof(keys));

			var keysList = keys as List<TKey> ?? keys.ToList();
			if (keysList.Count == 0)
				return;

			List<KeyValuePair<TKey, TValue>> removedItems;

			lock (_lock)
			{
				removedItems = [];
				foreach (var key in keysList)
				{
					if (_items.Remove(key, out var value))
						removedItems.Add(new KeyValuePair<TKey, TValue>(key, value));
				}
				_count = _items.Count;
			}

			if (removedItems.Count == 0)
				return;

			if (PreferResetForRangeOperations && removedItems.Count > 1)
				RaiseChangedEvents(EventArgsCache.ResetCollectionChanged);
			else
				RaiseChangedEvents(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, removedItems));
		}

		/// <summary>
		/// Gets the value associated with the specified key if it exists; otherwise, adds the specified value
		/// to the <see cref="ObservableDictionary{TKey, TValue}"/> and returns it.
		/// </summary>
		/// <param name="key">The key of the value to get or add.</param>
		/// <param name="value">The value to add if the key does not exist.</param>
		/// <returns>The existing value associated with <paramref name="key"/>, or <paramref name="value"/> if the key was not found.</returns>
		public virtual TValue GetOrAdd(TKey key, TValue value)
		{
			lock (_lock)
			{
				if (_items.TryGetValue(key, out var existing))
					return existing;

				_items.Add(key, value);
				_count = _items.Count;
			}

			RaiseChangedEvents(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add,
				new KeyValuePair<TKey, TValue>(key, value)));
			return value;
		}

		/// <summary>
		/// Gets the value associated with the specified key if it exists; otherwise, invokes the factory to create
		/// a new value, adds it to the <see cref="ObservableDictionary{TKey, TValue}"/> and returns it.
		/// </summary>
		/// <param name="key">The key of the value to get or add.</param>
		/// <param name="valueFactory">The function that produces the value to add if the key does not exist.</param>
		/// <returns>The existing value associated with <paramref name="key"/>, or the produced value if the key was not found.</returns>
		/// <exception cref="ArgumentNullException"><paramref name="valueFactory"/> is <see langword="null"/>.</exception>
		public virtual TValue GetOrAdd(TKey key, Func<TKey, TValue> valueFactory)
		{
			if (valueFactory == null)
				throw new ArgumentNullException(nameof(valueFactory));

			TValue value;

			lock (_lock)
			{
				if (_items.TryGetValue(key, out var existing))
					return existing;

				value = valueFactory(key);
				_items.Add(key, value);
				_count = _items.Count;
			}

			RaiseChangedEvents(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add,
				new KeyValuePair<TKey, TValue>(key, value)));
			return value;
		}

		/// <summary>
		/// Adds the specified key and value to the <see cref="ObservableDictionary{TKey, TValue}"/> if the key does
		/// not exist; otherwise, updates the existing value using the specified update function.
		/// </summary>
		/// <param name="key">The key of the element to add or update.</param>
		/// <param name="addValue">The value to add if the key does not exist.</param>
		/// <param name="updateFactory">The function that produces the new value from the existing value if the key exists.</param>
		/// <returns>The new value for the specified key.</returns>
		/// <exception cref="ArgumentNullException"><paramref name="updateFactory"/> is <see langword="null"/>.</exception>
		public virtual TValue AddOrUpdate(TKey key, TValue addValue, Func<TKey, TValue, TValue> updateFactory)
		{
			if (updateFactory == null)
				throw new ArgumentNullException(nameof(updateFactory));

			bool exists;
			TValue value, oldValue;

			lock (_lock)
			{
				if (_items.TryGetValue(key, out oldValue!))
				{
					exists = true;
					value = updateFactory(key, oldValue);
					_items[key] = value;
				}
				else
				{
					exists = false;
					value = addValue;
					_items.Add(key, value);
					_count = _items.Count;
				}
			}

			if (exists)
				RaiseChangedEvents(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Replace,
					new KeyValuePair<TKey, TValue>(key, value),
					new KeyValuePair<TKey, TValue>(key, oldValue)));
			else
				RaiseChangedEvents(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add,
					new KeyValuePair<TKey, TValue>(key, value)));
			return value;
		}

		/// <summary>
		/// Adds the specified key and the value produced by the add factory to the
		/// <see cref="ObservableDictionary{TKey, TValue}"/> if the key does not exist; otherwise, updates the
		/// existing value using the specified update function.
		/// </summary>
		/// <param name="key">The key of the element to add or update.</param>
		/// <param name="addFactory">The function that produces the value to add if the key does not exist.</param>
		/// <param name="updateFactory">The function that produces the new value from the existing value if the key exists.</param>
		/// <returns>The new value for the specified key.</returns>
		/// <exception cref="ArgumentNullException"><paramref name="addFactory"/> or <paramref name="updateFactory"/> is <see langword="null"/>.</exception>
		public virtual TValue AddOrUpdate(TKey key, Func<TKey, TValue> addFactory, Func<TKey, TValue, TValue> updateFactory)
		{
			if (addFactory == null)
				throw new ArgumentNullException(nameof(addFactory));
			if (updateFactory == null)
				throw new ArgumentNullException(nameof(updateFactory));

			bool exists;
			TValue value, oldValue;

			lock (_lock)
			{
				if (_items.TryGetValue(key, out oldValue!))
				{
					exists = true;
					value = updateFactory(key, oldValue);
					_items[key] = value;
				}
				else
				{
					exists = false;
					value = addFactory(key);
					_items.Add(key, value);
					_count = _items.Count;
				}
			}

			if (exists)
				RaiseChangedEvents(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Replace,
					new KeyValuePair<TKey, TValue>(key, value),
					new KeyValuePair<TKey, TValue>(key, oldValue)));
			else
				RaiseChangedEvents(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add,
					new KeyValuePair<TKey, TValue>(key, value)));
			return value;
		}

		/// <summary>
		/// Determines whether the <see cref="ObservableDictionary{TKey, TValue}"/> contains the specified key.
		/// </summary>
		/// <param name="key">The key to locate.</param>
		/// <returns><see langword="true"/> if the dictionary contains an element with the specified key; otherwise, <see langword="false"/>.</returns>
		public virtual bool ContainsKey(TKey key)
		{
			lock (_lock)
				return _items.ContainsKey(key);
		}
		bool IDictionary.Contains(object key)
		{
			return key is TKey k && ContainsKey(k);
		}

		/// <summary>
		/// Determines whether the <see cref="ObservableDictionary{TKey, TValue}"/> contains a specific key/value pair.
		/// </summary>
		/// <param name="item">The key/value pair to locate.</param>
		/// <returns><see langword="true"/> if the pair was found; otherwise, <see langword="false"/>.</returns>
		public virtual bool Contains(KeyValuePair<TKey, TValue> item)
		{
			lock (_lock)
			{
				return _items.TryGetValue(item.Key, out var value) && EqualityComparer<TValue>.Default.Equals(value, item.Value);
			}
		}

		/// <summary>
		/// Gets the value associated with the specified key.
		/// </summary>
		/// <param name="key">The key of the value to get.</param>
		/// <param name="value">When this method returns, contains the value associated with the specified key, if found; otherwise, the default value.</param>
		/// <returns><see langword="true"/> if the dictionary contains an element with the specified key; otherwise, <see langword="false"/>.</returns>
		public virtual bool TryGetValue(TKey key, [NotNullWhen(true)] out TValue value)
		{
			lock (_lock)
				return _items.TryGetValue(key, out value!);
		}

		/// <summary>
		/// Removes all keys and values from the <see cref="ObservableDictionary{TKey, TValue}"/>.
		/// </summary>
		public virtual void Clear()
		{
			if (_count == 0)
				return;

			List<KeyValuePair<TKey, TValue>> oldItems;

			lock (_lock)
			{
				oldItems = _items.ToList();
				_items.Clear();
				_count = 0;
			}

			if (PreferResetForRangeOperations)
				RaiseChangedEvents(EventArgsCache.ResetCollectionChanged);
			else
				RaiseChangedEvents(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Remove, oldItems));
		}

		/// <summary>
		/// Removes all keys and values from the <see cref="ObservableDictionary{TKey, TValue}"/> and resets it
		/// with the entries from the specified <see cref="IDictionary{TKey, TValue}"/>.
		/// </summary>
		/// <param name="dictionary">The dictionary whose entries will replace the current contents.</param>
		/// <exception cref="ArgumentNullException"><paramref name="dictionary"/> is <see langword="null"/>.</exception>
		public virtual void Reset(IDictionary<TKey, TValue> dictionary)
		{
			if (dictionary == null)
				throw new ArgumentNullException(nameof(dictionary));

			Reset((IEnumerable<KeyValuePair<TKey, TValue>>)dictionary);
		}

		/// <summary>
		/// Removes all keys and values from the <see cref="ObservableDictionary{TKey, TValue}"/> and resets it
		/// with the specified sequence of key/value pairs.
		/// </summary>
		/// <param name="items">The sequence of key/value pairs to reset the dictionary with.</param>
		/// <exception cref="ArgumentNullException"><paramref name="items"/> is <see langword="null"/>.</exception>
		/// <exception cref="ArgumentException"><paramref name="items"/> contains one or more duplicate keys.</exception>
		public virtual void Reset(IEnumerable<KeyValuePair<TKey, TValue>> items)
		{
			if (items == null)
				throw new ArgumentNullException(nameof(items));

			var itemsList = items as List<KeyValuePair<TKey, TValue>> ?? items.ToList();
			if (itemsList.Count == 0)
			{
				Clear();
				return;
			}

			List<KeyValuePair<TKey, TValue>> oldItems;

			lock (_lock)
			{
				oldItems = _items.ToList();
				_items.Clear();
				foreach (var pair in itemsList)
					_items.Add(pair.Key, pair.Value);
				_count = _items.Count;
			}

			if (PreferResetForRangeOperations)
				RaiseChangedEvents(EventArgsCache.ResetCollectionChanged);
			else if (oldItems.Count > 0)
				RaiseChangedEvents(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Replace, itemsList, oldItems));
			else
				RaiseChangedEvents(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, itemsList));
		}

		/// <summary>
		/// Copies the elements of the <see cref="ObservableDictionary{TKey, TValue}"/> to an existing
		/// one-dimensional array, starting at the specified array index.
		/// </summary>
		/// <param name="array">The one-dimensional array that is the destination of the elements copied from the dictionary.</param>
		/// <param name="arrayIndex">The zero-based index in <paramref name="array"/> at which copying begins.</param>
		public virtual void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
		{
			lock (_lock)
				((ICollection<KeyValuePair<TKey, TValue>>)_items).CopyTo(array, arrayIndex);
		}
		void ICollection.CopyTo(Array array, int index)
		{
			lock (_lock)
			{
				var pairs = _items.ToArray();
				Array.Copy(pairs, 0, array, index, pairs.Length);
			}
		}

		/// <summary>
		/// Returns a snapshot enumerator for the dictionary.
		/// This method is thread-safe and returns a snapshot of the current state of the dictionary,
		/// which can be enumerated without blocking other threads from modifying the dictionary.
		/// </summary>
		/// <returns>A snapshot enumerator for the dictionary.</returns>
		public IEnumerator<KeyValuePair<TKey, TValue>> GetSnapshotEnumerator()
		{
			lock (_lock)
				return ((IEnumerable<KeyValuePair<TKey, TValue>>)_items.ToArray()).GetEnumerator();
		}

		/// <summary>
		/// Returns an enumerator that iterates through the dictionary.
		/// Based on the value of <see cref="UseSnapshotEnumeration"/>, it either returns a snapshot enumerator
		/// or the current enumerator.
		/// </summary>
		/// <returns>An enumerator that iterates through the dictionary.</returns>
		public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
		{
			if (UseSnapshotEnumeration)
				return GetSnapshotEnumerator();

			return _items.GetEnumerator();
		}
		IEnumerator IEnumerable.GetEnumerator()
		{
			return GetEnumerator();
		}
		IDictionaryEnumerator IDictionary.GetEnumerator()
		{
			return new DictionaryEnumerator(GetSnapshotEnumerator());
		}

		private sealed class DictionaryEnumerator : IDictionaryEnumerator
		{
			private readonly IEnumerator<KeyValuePair<TKey, TValue>> _enumerator;

			public DictionaryEnumerator(IEnumerator<KeyValuePair<TKey, TValue>> enumerator)
			{
				_enumerator = enumerator;
			}

			public DictionaryEntry Entry => new(_enumerator.Current.Key, _enumerator.Current.Value);

			public object Key => _enumerator.Current.Key;

			public object? Value => _enumerator.Current.Value;

			public object Current => Entry;

			public bool MoveNext() => _enumerator.MoveNext();

			public void Reset() => _enumerator.Reset();
		}
	}
}
