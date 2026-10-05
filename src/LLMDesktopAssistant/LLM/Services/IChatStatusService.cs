using System.ComponentModel;
using Material.Icons;

namespace LLMDesktopAssistant.LLM.Services
{
	/// <summary>
	/// Tracks the free-form status bar state (icon and text) of a chat session.
	/// </summary>
	public interface IChatStatusService : INotifyPropertyChanged
	{
		/// <summary>
		/// Gets or sets the icon kind to display in the status bar of the chat window.
		/// </summary>
		VisualIconKind Icon { get; set; }

		/// <summary>
		/// Gets or sets the text to display in the status bar of the chat window.
		/// </summary>
		string? Text { get; set; }
	}
}
