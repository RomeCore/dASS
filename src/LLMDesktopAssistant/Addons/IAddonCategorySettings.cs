using System.ComponentModel;

namespace LLMDesktopAssistant.Addons
{
	/// <summary>
	/// A settings category that owns an addon set and can switch the whole category off.
	/// </summary>
	/// <remarks>
	/// Categories without a gate do not implement this interface; the collector treats their addons as always
	/// enabled. The property is a default interface member so that a gated category only declares a settable
	/// property when it actually has one.
	/// </remarks>
	public interface IAddonCategorySettings : INotifyPropertyChanged
	{
		/// <summary>
		/// Gets a value indicating whether this addon category contributes addons at all.
		/// </summary>
		bool AddonsCategoryEnabled => true;
	}
}
