using System.Collections.Concurrent;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Globalization;
using LLMDesktopAssistant.StructuredValues;
using LLMDesktopAssistant.StructuredValues.Converters;
using LLMDesktopAssistant.StructuredValues.Reactive;

namespace LLMDesktopAssistant.MVVM.Dynamic
{
	/// <summary>
	/// A dynamic view model over a reactive/immutable structured node value
	/// (<see cref="INodeValue"/>). Dictionary entries become members, array elements are exposed
	/// through the indexer and the <see cref="StructuredDynamicViewModel.Items"/> view, and scalar
	/// values are projected into raw CLR values.
	/// </summary>
	/// <remarks>
	/// The root is expected to be a dictionary or an array; scalar roots are not supported. Writes
	/// require a mutable (reactive) root, because <see cref="StructuredValues.Const.ConstNodeValue"/>
	/// instances are immutable.
	/// </remarks>
	public class NodeValueDynamicViewModel : StructuredDynamicViewModel
	{
		private readonly ConcurrentDictionary<ReactiveNodeValue, string> _scalarKeys = new();
		private readonly ConcurrentDictionary<string, ReactiveNodeValue> _scalarNodes = new();
		private bool _internalWrite;

		/// <summary>
		/// Initializes a new instance of the <see cref="NodeValueDynamicViewModel"/> class.
		/// </summary>
		/// <param name="root">The root node value (a dictionary or an array).</param>
		public NodeValueDynamicViewModel(INodeValue root)
		{
			Root = root ?? throw new ArgumentNullException(nameof(root));
			SubscribeRootCollection();
		}

		/// <summary>Gets the underlying root node value.</summary>
		public INodeValue Root { get; }

		/// <inheritdoc/>
		protected override IEnumerable<string> EnumerateDataKeys()
		{
			return Root is INodeDictionaryValue dictionary ? dictionary.Items.Keys : [];
		}

		/// <inheritdoc/>
		protected override int CountEntries()
		{
			return Root switch
			{
				INodeDictionaryValue dictionary => dictionary.Items.Count,
				INodeArrayValue array => array.Items.Count,
				_ => 0,
			};
		}

		/// <inheritdoc/>
		protected override bool TryGetRawValue(string key, out object? raw)
		{
			if (Root is INodeDictionaryValue dictionary && dictionary.Items.TryGetValue(key, out var value))
			{
				raw = value;
				return true;
			}

			if (Root is INodeArrayValue array && TryParseIndex(key, out var index) && index >= 0 && index < array.Items.Count)
			{
				raw = array.Items[index];
				return true;
			}

			raw = null;
			return false;
		}

		/// <inheritdoc/>
		protected override void SetRawValue(string key, object? raw)
		{
			_internalWrite = true;
			try
			{
				if (Root is ReactiveNodeDictionaryValue dictionary)
				{
					dictionary.Items[key] = (ReactiveNodeValue)raw!;
					return;
				}

				if (Root is ReactiveNodeArrayValue array && TryParseIndex(key, out var index) && index >= 0 && index < array.Items.Count)
				{
					array.Items[index] = (ReactiveNodeValue)raw!;
				}
			}
			finally
			{
				_internalWrite = false;
			}
		}

		/// <inheritdoc/>
		protected override ValueKind Classify(object? raw)
		{
			return raw switch
			{
				null or INodeNullValue => ValueKind.Null,
				INodeDictionaryValue or INodeArrayValue => ValueKind.Container,
				INodeValue => ValueKind.Scalar,
				_ => ValueKind.Null,
			};
		}

		/// <inheritdoc/>
		protected override object? ScalarToClr(object? raw)
		{
			return ((INodeValue)raw!).TakeValueSnapshot();
		}

		/// <inheritdoc/>
		protected override object? ClrToRaw(object? value)
		{
			return ClrToReactive(value);
		}

		/// <inheritdoc/>
		protected override bool RawEquals(object? left, object? right)
		{
			if (ReferenceEquals(left, right))
				return true;
			if (left is null || right is null)
				return false;
			if (left is INodeValue a && right is INodeValue b)
				return SnapshotEquals(a.TakeValueSnapshot(), b.TakeValueSnapshot());
			return Equals(left, right);
		}

		/// <inheritdoc/>
		protected override StructuredDynamicViewModel CreateChildViewModel(object? raw)
		{
			return new NodeValueDynamicViewModel((INodeValue)raw!);
		}

		/// <inheritdoc/>
		protected override bool TryGetArrayRawItem(int index, out object? raw)
		{
			if (Root is INodeArrayValue array && index >= 0 && index < array.Items.Count)
			{
				raw = array.Items[index];
				return true;
			}

			raw = null;
			return false;
		}

		/// <inheritdoc/>
		protected override int GetArrayLength()
		{
			return Root is INodeArrayValue array ? array.Items.Count : 0;
		}

		/// <inheritdoc/>
		protected override void OnMemberMaterialized(string key, object? raw, object? materialized)
		{
			if (raw is ReactiveNodeValue reactive && Classify(reactive) == ValueKind.Scalar)
			{
				_scalarKeys[reactive] = key;
				_scalarNodes[key] = reactive;
				reactive.PropertyChanged += OnScalarChanged;
			}
		}

		/// <inheritdoc/>
		protected override void OnMemberInvalidated(string key, object? previous)
		{
			if (_scalarNodes.TryRemove(key, out var reactive))
			{
				reactive.PropertyChanged -= OnScalarChanged;
				_scalarKeys.TryRemove(reactive, out _);
			}
		}

		/// <inheritdoc/>
		protected override void Dispose(bool disposing)
		{
			if (disposing)
				UnsubscribeRootCollection();

			base.Dispose(disposing);
		}

		private void SubscribeRootCollection()
		{
			switch (Root)
			{
				case ReactiveNodeDictionaryValue dictionary:
					dictionary.Items.CollectionChanged += OnRootCollectionChanged;
					break;
				case ReactiveNodeArrayValue array:
					array.Items.CollectionChanged += OnRootCollectionChanged;
					break;
			}
		}

		private void UnsubscribeRootCollection()
		{
			switch (Root)
			{
				case ReactiveNodeDictionaryValue dictionary:
					dictionary.Items.CollectionChanged -= OnRootCollectionChanged;
					break;
				case ReactiveNodeArrayValue array:
					array.Items.CollectionChanged -= OnRootCollectionChanged;
					break;
			}
		}

		private void OnRootCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
		{
			if (_internalWrite)
				return;

			InvalidateAllMembers();
			NotifyAllMembersChanged();
		}

		private void OnScalarChanged(object? sender, PropertyChangedEventArgs e)
		{
			if (sender is not ReactiveNodeValue reactive || !_scalarKeys.TryGetValue(reactive, out var key))
				return;

			InvalidateMember(key);
			NotifyMemberChanged(key);
		}

		private static ReactiveNodeValue ClrToReactive(object? value)
		{
			return value switch
			{
				null => new ReactiveNodeNullValue(),
				ReactiveNodeValue reactive => reactive,
				INodeValue node => node.ToReactiveNodeValue()!,
				bool boolean => new ReactiveNodeBooleanValue { Value = boolean },
				string text => new ReactiveNodeStringValue { Value = text },
				sbyte or byte or short or ushort or int or uint or long or ulong or float or double or decimal
					=> new ReactiveNodeNumberValue { Value = Convert.ToDouble(value, CultureInfo.InvariantCulture) },
				_ => new ReactiveNodeStringValue { Value = value.ToString() },
			};
		}

		private static bool SnapshotEquals(object? left, object? right)
		{
			if (Equals(left, right))
				return true;

			if (left is object?[] leftArray && right is object?[] rightArray)
			{
				if (leftArray.Length != rightArray.Length)
					return false;
				for (var i = 0; i < leftArray.Length; i++)
				{
					if (!SnapshotEquals(leftArray[i], rightArray[i]))
						return false;
				}
				return true;
			}

			if (left is IEnumerable<KeyValuePair<string, object?>> leftMap && right is IEnumerable<KeyValuePair<string, object?>> rightMap)
			{
				var leftDict = leftMap.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
				var rightDict = rightMap.ToDictionary(kvp => kvp.Key, kvp => kvp.Value);
				if (leftDict.Count != rightDict.Count)
					return false;
				foreach (var kvp in leftDict)
				{
					if (!rightDict.TryGetValue(kvp.Key, out var other) || !SnapshotEquals(kvp.Value, other))
						return false;
				}
				return true;
			}

			return false;
		}

		private static bool TryParseIndex(string key, out int index)
		{
			return int.TryParse(key, NumberStyles.None, CultureInfo.InvariantCulture, out index);
		}
	}
}
