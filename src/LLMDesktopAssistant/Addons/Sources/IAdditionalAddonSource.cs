using LLMDesktopAssistant.Utils;

namespace LLMDesktopAssistant.Addons.Sources
{
	/// <summary>
	/// A source of additional addons for an addon set collector: addons that are not loaded from addon packs
	/// (native modules, MCP tools, prompt template parts) and therefore are not part of
	/// <see cref="IFileAddonAccessor{T}"/>.
	/// </summary>
	/// <remarks>
	/// The collection is observable: the collector subscribes to it and patches only the affected keys.
	/// </remarks>
	/// <typeparam name="TAddon">The type of the addons in the source.</typeparam>
	public interface IAdditionalAddonSource<TAddon>
		where TAddon : AddonBase<TAddon>
	{
		/// <summary>
		/// Gets the addons this source contributes.
		/// </summary>
		ReadOnlyObservableCollection<TAddon> Addons { get; }
	}
}
