using System.Collections;
using LLMDesktopAssistant.Utils;

namespace LLMDesktopAssistant.Addons
{
	/// <summary>
	/// An ordered, observable view over a resolved addon set.
	/// </summary>
	/// <remarks>
	/// The set owns the flat list and the identity lookup: every mutation keeps both in sync, so a lookup always
	/// reflects the list and no consumer ever rebuilds the lookup. Cache owners hand out the same instance for the
	/// same inputs, so a caller that holds one sees the collection update instead of receiving a new one. Items are
	/// ordered by <see cref="AddonBase{Self}.Order"/>, then <see cref="AddonBase{Self}.Name"/>, then
	/// <see cref="AddonBase{Self}.Key"/>, which keeps insertion positions deterministic.
	/// </remarks>
	/// <typeparam name="TAddon">The type of the addons in the set.</typeparam>
	public sealed class AddonSet<TAddon> : IReadOnlyList<TAddon>
		where TAddon : AddonBase<TAddon>
	{
		private readonly Lock _lock = new();
		private readonly RangeObservableCollection<TAddon> _items = [];
		private readonly ReadOnlyObservableCollection<TAddon> _view;
		private readonly Dictionary<string, TAddon> _byKey = new(StringComparer.Ordinal);
		private readonly Dictionary<string, List<TAddon>> _claims = new(StringComparer.OrdinalIgnoreCase);

		/// <summary>
		/// Initializes a new instance of the <see cref="AddonSet{TAddon}"/> class.
		/// </summary>
		public AddonSet()
		{
			_view = new(_items);
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="AddonSet{TAddon}"/> class with the specified addons.
		/// </summary>
		/// <param name="addons">The addons to seed the set with.</param>
		public AddonSet(IEnumerable<TAddon> addons) : this()
		{
			Reset(addons);
		}

		/// <summary>
		/// Gets the observable read-only view over the set.
		/// </summary>
		public ReadOnlyObservableCollection<TAddon> Addons => _view;

		/// <inheritdoc/>
		public int Count => _items.Count;

		/// <inheritdoc/>
		public TAddon this[int index] => _items[index];

		/// <summary>
		/// Gets the addon with the specified name or alias, or <see langword="null"/> when the set does not contain it.
		/// A name match wins over an alias match; ties are resolved by <see cref="AddonBase{Self}.OverrideOrder"/>
		/// descending, then <see cref="AddonBase{Self}.Order"/> ascending, then <see cref="AddonBase{Self}.Key"/>.
		/// </summary>
		/// <param name="name">The name or alias to look up.</param>
		public TAddon? GetByName(string name)
		{
			ArgumentNullException.ThrowIfNull(name);

			lock (_lock)
				return _claims.TryGetValue(name, out var claims) && claims.Count > 0 ? claims[0] : null;
		}

		/// <summary>
		/// Determines whether the set contains an addon with the specified name.
		/// </summary>
		/// <param name="name">The name to look up.</param>
		/// <returns><see langword="true"/> if the set contains an addon with the specified name; otherwise, <see langword="false"/>.</returns>
		public bool ContainsName(string name)
		{
			ArgumentNullException.ThrowIfNull(name);

			lock (_lock)
				return _claims.ContainsKey(name);
		}

		/// <summary>
		/// Gets the addon with the specified key, or <see langword="null"/> when the set does not contain it.
		/// </summary>
		/// <param name="key">The exact <see cref="AddonBase{Self}.Key"/> of the addon.</param>
		public TAddon? GetByKey(string key)
		{
			ArgumentNullException.ThrowIfNull(key);

			lock (_lock)
				return _byKey.GetValueOrDefault(key);
		}

		/// <summary>
		/// Determines whether the set contains an addon with the specified key.
		/// </summary>
		/// <param name="key">The key to look up.</param>
		/// <returns><see langword="true"/> if the set contains an addon with the specified key; otherwise, <see langword="false"/>.</returns>
		public bool ContainsKey(string key)
		{
			ArgumentNullException.ThrowIfNull(key);

			lock (_lock)
				return _byKey.ContainsKey(key);
		}

		/// <inheritdoc/>
		public IEnumerator<TAddon> GetEnumerator() => _items.GetEnumerator();

		/// <inheritdoc/>
		IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

		/// <summary>
		/// Inserts the addon or replaces the addon with the same key. An unchanged item is a no-op, so repeated
		/// reconciliations never raise events.
		/// </summary>
		internal void Upsert(TAddon addon)
		{
			ArgumentNullException.ThrowIfNull(addon);

			lock (_lock)
			{
				var hadOld = _byKey.TryGetValue(addon.Key, out var old);
				if (hadOld && ReferenceEquals(old, addon))
					return;

				var oldIndex = hadOld ? IndexOf(old!) : -1;
				if (hadOld)
					RemoveClaims(old!);

				_byKey[addon.Key] = addon;
				AddClaims(addon);

				if (oldIndex >= 0 && ComparePosition(old!, addon) == 0)
				{
					_items.Set(oldIndex, addon);
					return;
				}

				if (oldIndex >= 0)
					_items.RemoveAt(oldIndex);
				_items.Insert(FindInsertIndex(addon), addon);
			}
		}

		/// <summary>
		/// Removes the addon with the specified key. A missing key is a no-op.
		/// </summary>
		internal void Remove(string key)
		{
			ArgumentNullException.ThrowIfNull(key);

			lock (_lock)
			{
				if (!_byKey.Remove(key, out var addon))
					return;
				RemoveClaims(addon);
				var index = IndexOf(addon);
				if (index >= 0)
					_items.RemoveAt(index);
			}
		}

		/// <summary>
		/// Removes every addon from the set.
		/// </summary>
		internal void Clear()
		{
			lock (_lock)
			{
				_byKey.Clear();
				_claims.Clear();
				_items.Clear();
			}
		}

		/// <summary>
		/// Replaces the whole set with the specified addons, raising a single collection change. The underlying
		/// collection reports Add for an empty set and Replace otherwise, per its range-reset rule.
		/// </summary>
		internal void Reset(IEnumerable<TAddon> addons)
		{
			var list = addons.ToList();
			list.Sort(ComparePosition);

			lock (_lock)
			{
				_byKey.Clear();
				_claims.Clear();
				foreach (var addon in list)
				{
					_byKey[addon.Key] = addon;
					AddClaims(addon);
				}
				_items.Reset(list);
			}
		}

		private int IndexOf(TAddon addon)
		{
			for (var i = 0; i < _items.Count; i++)
				if (ReferenceEquals(_items[i], addon))
					return i;
			return -1;
		}

		private int FindInsertIndex(TAddon addon)
		{
			var low = 0;
			var high = _items.Count;
			while (low < high)
			{
				var middle = (low + high) / 2;
				if (ComparePosition(_items[middle], addon) < 0)
					low = middle + 1;
				else
					high = middle;
			}
			return low;
		}

		private void AddClaims(TAddon addon)
		{
			AddClaim(addon.Name, addon);
			foreach (var alias in addon.Aliases)
				AddClaim(alias, addon);
		}

		private void AddClaim(string token, TAddon addon)
		{
			if (string.IsNullOrEmpty(token))
				return;

			if (!_claims.TryGetValue(token, out var claims))
				_claims[token] = claims = [];
			if (claims.Contains(addon))
				return;

			claims.Add(addon);
			claims.Sort((x, y) => CompareClaims(x, y, token));
		}

		private void RemoveClaims(TAddon addon)
		{
			RemoveClaim(addon.Name, addon);
			foreach (var alias in addon.Aliases)
				RemoveClaim(alias, addon);
		}

		private void RemoveClaim(string token, TAddon addon)
		{
			if (string.IsNullOrEmpty(token) || !_claims.TryGetValue(token, out var claims))
				return;

			claims.RemoveAll(candidate => ReferenceEquals(candidate, addon));
			if (claims.Count == 0)
				_claims.Remove(token);
		}

		private static int CompareClaims(TAddon x, TAddon y, string token)
		{
			// A canonical name always wins over an alias of another addon.
			var xName = string.Equals(x.Name, token, StringComparison.OrdinalIgnoreCase) ? 0 : 1;
			var yName = string.Equals(y.Name, token, StringComparison.OrdinalIgnoreCase) ? 0 : 1;
			var result = xName.CompareTo(yName);
			if (result != 0)
				return result;

			result = y.OverrideOrder.CompareTo(x.OverrideOrder);
			if (result != 0)
				return result;

			result = x.Order.CompareTo(y.Order);
			if (result != 0)
				return result;

			return string.CompareOrdinal(x.Key, y.Key);
		}

		private static int ComparePosition(TAddon x, TAddon y)
		{
			var result = x.Order.CompareTo(y.Order);
			if (result != 0)
				return result;

			result = string.CompareOrdinal(x.Name, y.Name);
			if (result != 0)
				return result;

			return string.CompareOrdinal(x.Key, y.Key);
		}
	}
}
