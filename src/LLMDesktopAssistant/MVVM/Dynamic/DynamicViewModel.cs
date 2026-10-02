using System.Collections.Specialized;
using System.ComponentModel;
using System.Reflection;
using System.Runtime.CompilerServices;

namespace LLMDesktopAssistant.MVVM.Dynamic
{
	/// <summary>
	/// A view model whose members are resolved dynamically by name, allowing bindings such as
	/// <c>{Binding key}</c> and <c>{Binding [key]}</c> to be satisfied without declaring real CLR
	/// properties or an indexer entry per member.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Dynamic member access is discovered by Avalonia through <see cref="IReflectableType"/>: the
	/// reflection binding resolves each path segment with
	/// <see cref="TypeInfo.GetProperty(string, BindingFlags)"/>, which <see cref="GetTypeInfo"/>
	/// answers with a <see cref="DynamicViewModelTypeInfo"/>. Because a <see cref="PropertyInfo"/>
	/// is produced for any requested name, bindings for members that do not exist yet become live
	/// and update once <see cref="INotifyPropertyChanged"/> reports them.
	/// </para>
	/// <para>
	/// Both binding syntaxes are supported: property syntax through <see cref="GetDynamicMember"/>
	/// / <see cref="SetDynamicMember"/>, and indexer syntax through the <see cref="this[string]"/>
	/// indexer. Raise changes with <see cref="RaisePropertyChangedFor"/> so both stay in sync.
	/// </para>
	/// </remarks>
	public abstract class DynamicViewModel : IReflectableType, INotifyPropertyChanged, INotifyCollectionChanged
	{
		/// <summary>
		/// The CLR name of the indexer property (see the <see cref="this[string]"/> indexer).
		/// Indexer bindings (<c>{Binding [key]}</c>) report their changes under this name.
		/// </summary>
		public const string IndexerPropertyName = "Item";

		// TypeInfo instances are stateless and instance-agnostic, so a single one per concrete
		// type can be shared by all instances (and cached weakly to avoid rooting dynamic types).
		private static readonly ConditionalWeakTable<Type, DynamicViewModelTypeInfo> typeInfos = [];

		private readonly TypeInfo _typeInfo;

		protected DynamicViewModel()
		{
			_typeInfo = typeInfos.GetValue(GetType(), static type => new DynamicViewModelTypeInfo(type));
		}

		/// <summary>
		/// Raised when a dynamic member value changes. The property name is the member key for
		/// property bindings, <see cref="IndexerPropertyName"/> for indexer bindings, or empty to
		/// refresh every binding.
		/// </summary>
		public event PropertyChangedEventHandler? PropertyChanged;

		/// <summary>
		/// Raised with a reset action when a dynamic member changes, so that indexer bindings
		/// (<c>{Binding [key]}</c>) refresh even when the indexer is inherited from this base type.
		/// </summary>
		public event NotifyCollectionChangedEventHandler? CollectionChanged;

		/// <summary>
		/// Gets or sets a dynamic member by key, enabling indexer binding syntax
		/// <c>{Binding [key]}</c> in addition to the property syntax <c>{Binding key}</c>.
		/// </summary>
		/// <param name="key">The dynamic member key.</param>
		public object? this[string key]
		{
			get => GetDynamicMember(key);
			set => SetDynamicMember(key, value);
		}

		/// <summary>
		/// Returns the <see cref="TypeInfo"/> through which Avalonia's reflection bindings resolve
		/// dynamic members.
		/// </summary>
		/// <returns>The type info describing the concrete view model type.</returns>
		/// <remarks>
		/// The same instance is reused for every instance of the concrete type.
		/// </remarks>
		public TypeInfo GetTypeInfo()
		{
			return _typeInfo;
		}

		/// <summary>
		/// Reads a dynamic member by name.
		/// </summary>
		/// <param name="name">The dynamic member key.</param>
		/// <returns>
		/// The current value of the member, or <see langword="null"/> when the member is not set.
		/// </returns>
		/// <remarks>
		/// The method is deliberately named unusually (rather than <c>GetProperty</c>): Avalonia's
		/// reflection method accessor takes priority over the dynamic property accessor whenever a
		/// member name collides with <em>any</em> CLR method name, so the API names are kept
		/// improbable as dynamic keys.
		/// </remarks>
		public abstract object? GetDynamicMember(string name);

		/// <summary>
		/// Writes a dynamic member by name. Implementations should store the value and then call
		/// <see cref="RaisePropertyChangedFor"/> so that bound UI updates.
		/// </summary>
		/// <param name="name">The dynamic member key.</param>
		/// <param name="value">The new value of the member.</param>
		public abstract void SetDynamicMember(string name, object? value);

		/// <summary>
		/// Raises a bare <see cref="INotifyPropertyChanged"/> notification for a single name.
		/// </summary>
		/// <param name="propertyName">
		/// The member name, or <see langword="null"/>/empty to refresh every binding.
		/// </param>
		protected void RaisePropertyChanged(string? propertyName)
		{
			PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
		}

		/// <summary>
		/// Raises every change notification required for a dynamic member update. Concrete
		/// <see cref="SetDynamicMember"/> implementations should call this after mutating the store.
		/// </summary>
		/// <param name="propertyName">The dynamic member key that changed.</param>
		/// <remarks>
		/// Three notifications are raised because the two binding syntaxes observe changes
		/// differently:
		/// <list type="bullet">
		/// <item><description>the member name itself, for <c>{Binding key}</c>;</description></item>
		/// <item><description>the indexer property name (<see cref="IndexerPropertyName"/>), for
		/// <c>{Binding [key]}</c> when the indexer is declared on the concrete view model type;</description></item>
		/// <item><description>a collection reset, because indexer bindings only react to a property
		/// <em>declared on the source type</em> and therefore do not see an indexer inherited from
		/// this base class - the collection-changed path matches regardless of where the indexer is
		/// declared.</description></item>
		/// </list>
		/// </remarks>
		protected void RaisePropertyChangedFor(string propertyName)
		{
			RaisePropertyChanged(propertyName);
			RaisePropertyChanged(IndexerPropertyName);
			RaiseCollectionChanged();
		}

		/// <summary>
		/// Raises <see cref="INotifyCollectionChanged.CollectionChanged"/> with a reset action.
		/// </summary>
		/// <remarks>
		/// Unlike <see cref="INotifyPropertyChanged"/>, Avalonia does not marshal
		/// collection-changed events to the UI thread, so raise this from the UI thread.
		/// </remarks>
		protected void RaiseCollectionChanged()
		{
			CollectionChanged?.Invoke(this, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
		}
	}
}
