using System.Collections.Generic;
using System.Globalization;

namespace LLMDesktopAssistant.MVVM.Dynamic
{
	/// <summary>
	/// Base class for <see cref="DynamicViewModel"/> implementations backed by a hierarchical
	/// structured value (a dictionary/array tree). Members are resolved lazily: scalars are
	/// projected into raw CLR values, containers into nested view models, and functions into
	/// commands. Nested view models are cached per key so that repeated binding reads return the
	/// same instance.
	/// </summary>
	public abstract class StructuredDynamicViewModel : DynamicViewModel, IDisposable
	{
		private static readonly IReadOnlySet<string> NoHiddenKeys = new HashSet<string>(StringComparer.Ordinal);

		private readonly Dictionary<string, object?> _memberCache = new(StringComparer.Ordinal);
		private readonly Dictionary<int, object?> _arrayCache = [];
		private readonly Lock _lock = new();

		private DynamicArrayView? _items;
		private bool _disposed;

		/// <summary>Describes how a raw source value is exposed to bindings.</summary>
		protected enum ValueKind
		{
			/// <summary>An explicit null value.</summary>
			Null,

			/// <summary>A scalar value projected to a raw CLR value.</summary>
			Scalar,

			/// <summary>A nested dictionary/array exposed as a nested view model.</summary>
			Container,

			/// <summary>A callable value exposed as a command.</summary>
			Function,
		}

		/// <summary>
		/// Gets the set of source keys that must not be exposed as dynamic members (service keys
		/// such as change callbacks or helper functions injected by the view model).
		/// </summary>
		protected virtual IReadOnlySet<string> HiddenKeys => NoHiddenKeys;

		/// <summary>Enumerates the string data keys exposed as members.</summary>
		protected abstract IEnumerable<string> EnumerateDataKeys();

		/// <summary>Returns the total number of data entries in the underlying structure.</summary>
		protected abstract int CountEntries();

		/// <summary>Attempts to read the raw source value for a member key.</summary>
		protected abstract bool TryGetRawValue(string key, out object? raw);

		/// <summary>Writes a raw source value for a member key. A <see langword="null"/> raw value removes/nulls it.</summary>
		protected abstract void SetRawValue(string key, object? raw);

		/// <summary>Classifies a raw source value.</summary>
		protected abstract ValueKind Classify(object? raw);

		/// <summary>Projects a scalar raw value into a CLR value suitable for binding.</summary>
		protected abstract object? ScalarToClr(object? raw);

		/// <summary>Projects a CLR value into a raw source value suitable for writing.</summary>
		protected abstract object? ClrToRaw(object? value);

		/// <summary>Determines whether two raw values are considered equal (used to skip no-op writes).</summary>
		protected abstract bool RawEquals(object? left, object? right);

		/// <summary>Creates a nested view model for a container raw value.</summary>
		protected abstract StructuredDynamicViewModel CreateChildViewModel(object? raw);

		/// <summary>Creates a command for a function raw value, or <see langword="null"/> when functions are unsupported.</summary>
		protected virtual object? CreateCommandFor(object? raw) => null;

		/// <summary>Attempts to read the raw value of the array part at the given 0-based index.</summary>
		protected abstract bool TryGetArrayRawItem(int index, out object? raw);

		/// <summary>Gets the length of the contiguous array part.</summary>
		protected abstract int GetArrayLength();

		/// <summary>Called after a member is written, letting subclasses react to the change.</summary>
		protected virtual void OnMemberWritten(string key, object? oldRaw, object? newRaw)
		{
		}

		/// <summary>Called when a member is materialized, letting subclasses subscribe to its changes.</summary>
		protected virtual void OnMemberMaterialized(string key, object? raw, object? materialized)
		{
		}

		/// <summary>Called when a cached member is invalidated, letting subclasses unsubscribe.</summary>
		protected virtual void OnMemberInvalidated(string key, object? previous)
		{
		}

		/// <summary>
		/// Normalizes a binding name into a member key. Return <see langword="false"/> to treat the
		/// name as unknown.
		/// </summary>
		protected virtual bool TryNormalizeKey(string name, out string key)
		{
			key = name;
			return true;
		}

		/// <summary>Gets the total number of data entries in the underlying structure.</summary>
		public int Count => CountEntries();

		/// <summary>Gets the string data keys exposed as members.</summary>
		public IReadOnlyList<string> Keys => EnumerateDataKeys().ToArray();

		/// <summary>Gets a live view over the array part, suitable as <c>ItemsControl.ItemsSource</c>.</summary>
		public DynamicArrayView Items => _items ??= new DynamicArrayView(GetArrayLength, GetArrayItem);

		/// <inheritdoc/>
		public override object? GetDynamicMember(string name)
		{
			ArgumentNullException.ThrowIfNull(name);

			if (HiddenKeys.Contains(name))
				return null;
			if (!TryNormalizeKey(name, out var key))
				return null;

			return GetOrCreateMember(key);
		}

		/// <inheritdoc/>
		public override void SetDynamicMember(string name, object? value)
		{
			ArgumentNullException.ThrowIfNull(name);

			if (HiddenKeys.Contains(name))
				return;
			if (!TryNormalizeKey(name, out var key))
				return;

			TryGetRawValue(key, out var oldRaw);
			var raw = ClrToRaw(value);
			if (RawEquals(oldRaw, raw))
				return;

			SetRawValue(key, raw);
			InvalidateMember(key);
			NotifyMemberChanged(key);
			OnMemberWritten(key, oldRaw, raw);
		}

		/// <summary>Raises every change notification required for a member update.</summary>
		protected void NotifyMemberChanged(string key)
		{
			RaisePropertyChangedFor(key);
			RaisePropertyChanged(nameof(Count));
			RaisePropertyChanged(nameof(Keys));
			_items?.RaiseReset();
		}

		/// <summary>Raises a change notification that refreshes every binding (used after external structural changes).</summary>
		protected void NotifyAllMembersChanged()
		{
			RaisePropertyChangedFor(string.Empty);
			RaisePropertyChanged(nameof(Count));
			RaisePropertyChanged(nameof(Keys));
			_items?.RaiseReset();
		}

		/// <summary>Invalidates a single cached member.</summary>
		protected void InvalidateMember(string key)
		{
			object? previous = null;
			var removed = false;
			lock (_lock)
			{
				removed = _memberCache.Remove(key, out previous);
			}

			if (!removed)
				return;

			OnMemberInvalidated(key, previous);
			if (previous is IDisposable disposable)
				disposable.Dispose();
		}

		/// <summary>Invalidates every cached member (e.g. after an external collection change).</summary>
		protected void InvalidateAllMembers()
		{
			List<KeyValuePair<string, object?>> members;
			List<object?> arrayItems;
			lock (_lock)
			{
				members = _memberCache.ToList();
				arrayItems = _arrayCache.Values.ToList();
				_memberCache.Clear();
				_arrayCache.Clear();
			}

			foreach (var kvp in members)
			{
				OnMemberInvalidated(kvp.Key, kvp.Value);
				if (kvp.Value is IDisposable disposable)
					disposable.Dispose();
			}

			// Array items share the same invalidation semantics as members; a reset notification
			// is raised by the caller, so the cached children are disposed here.
			foreach (var item in arrayItems)
			{
				if (item is IDisposable disposable)
					disposable.Dispose();
			}
		}

		private object? GetOrCreateMember(string key)
		{
			lock (_lock)
			{
				if (_memberCache.TryGetValue(key, out var cached))
					return cached;
			}

			if (!TryGetRawValue(key, out var raw))
				return null;

			var materialized = Materialize(key, raw);
			if (materialized is not null)
			{
				lock (_lock)
					_memberCache[key] = materialized;
				OnMemberMaterialized(key, raw, materialized);
			}

			return materialized;
		}

		private object? GetArrayItem(int index)
		{
			lock (_lock)
			{
				if (_arrayCache.TryGetValue(index, out var cached))
					return cached;
			}

			if (!TryGetArrayRawItem(index, out var raw))
				return null;

			var materialized = Materialize(index.ToString(CultureInfo.InvariantCulture), raw);
			if (materialized is not null)
				lock (_lock)
					_arrayCache[index] = materialized;

			return materialized;
		}

		private object? Materialize(string key, object? raw)
		{
			return Classify(raw) switch
			{
				ValueKind.Null => null,
				ValueKind.Container => CreateChildViewModel(raw),
				ValueKind.Function => CreateCommandFor(raw),
				_ => ScalarToClr(raw),
			};
		}

		/// <inheritdoc/>
		public void Dispose()
		{
			Dispose(true);
			GC.SuppressFinalize(this);
		}

		/// <summary>Releases subscriptions held by this view model and disposes its nested view models.</summary>
		protected virtual void Dispose(bool disposing)
		{
			if (_disposed)
				return;
			_disposed = true;

			if (!disposing)
				return;

			List<KeyValuePair<string, object?>> members;
			List<object?> arrayItems;
			lock (_lock)
			{
				members = _memberCache.ToList();
				arrayItems = _arrayCache.Values.ToList();
				_memberCache.Clear();
				_arrayCache.Clear();
			}

			foreach (var kvp in members)
			{
				OnMemberInvalidated(kvp.Key, kvp.Value);
				if (kvp.Value is IDisposable disposable)
					disposable.Dispose();
			}

			foreach (var item in arrayItems)
			{
				if (item is IDisposable disposable)
					disposable.Dispose();
			}
		}
	}
}
