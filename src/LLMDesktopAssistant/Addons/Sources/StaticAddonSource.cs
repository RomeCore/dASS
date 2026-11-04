using LLMDesktopAssistant.Utils;

namespace LLMDesktopAssistant.Addons.Sources
{
	/// <summary>
	/// An <see cref="IAdditionalAddonSource{TAddon}"/> that never changes after construction.
	/// </summary>
	/// <typeparam name="TAddon">The type of the addons in the source.</typeparam>
	public class StaticAddonSource<TAddon>(IEnumerable<TAddon> addons) : IAdditionalAddonSource<TAddon>
		where TAddon : AddonBase<TAddon>
	{
		/// <inheritdoc/>
		public ReadOnlyObservableCollection<TAddon> Addons { get; } = new(addons.ToArray());
	}
}
