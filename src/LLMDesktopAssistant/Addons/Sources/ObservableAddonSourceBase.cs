using LLMDesktopAssistant.Utils;

namespace LLMDesktopAssistant.Addons.Sources
{
	/// <summary>
	/// A base for <see cref="IAdditionalAddonSource{TAddon}"/> implementations that change after construction.
	/// </summary>
	/// <typeparam name="TAddon">The type of the addons in the source.</typeparam>
	public abstract class ObservableAddonSourceBase<TAddon> : Disposable, IAdditionalAddonSource<TAddon>
		where TAddon : AddonBase<TAddon>
	{
		/// <inheritdoc/>
		public ReadOnlyObservableCollection<TAddon> Addons => field ??= new(Items);

		/// <summary>
		/// Gets the mutable collection the source fills and updates. Mutations raise collection-changed notifications
		/// the collector subscribes to.
		/// </summary>
		protected RangeObservableCollection<TAddon> Items { get; } = [];
	}
}
