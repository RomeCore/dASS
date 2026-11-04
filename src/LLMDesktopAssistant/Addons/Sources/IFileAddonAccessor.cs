using LLMDesktopAssistant.Utils;

namespace LLMDesktopAssistant.Addons.Sources
{
	/// <summary>
	/// The addon accessor used for accessing the list of addons of a specific type.
	/// Contains the reactive list of addons.
	/// </summary>
	public interface IFileAddonAccessor<T>
	{
		/// <summary>
		/// Gets the list of all addons of the specified type.
		/// </summary>
		ReadOnlyObservableCollection<T> Addons { get; }
	}
}
