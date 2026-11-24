using LiteDB;
using LLMDesktopAssistant.Controls.Icons;

namespace LLMDesktopAssistant.LLM.MVVM.Additional.Chat
{
	/// <summary>
	/// An additional chat data item that asks for a tab in the chat header, with its payload shown under it.
	/// </summary>
	/// <remarks>
	/// The strip is the chat's chrome, so the envelope owns the title, the icon and the close button while the
	/// payload draws only the body. Selection belongs to the user, not to the script: it is inherited when the
	/// payload is replaced under one replacement key, so an update never closes an open tab.
	/// </remarks>
	public class AdditionalChatDataTabItem : AdditionalChatDataItemBase
	{
		/// <summary>
		/// Gets or sets the title the tab shows.
		/// </summary>
		public string? Title
		{
			get;
			set
			{
				if (SetProperty(ref field, value))
					RaisePropertyChanged(nameof(HasTitle));
			}
		}

		/// <summary>
		/// Gets a value indicating whether the tab has a title worth drawing.
		/// </summary>
		[BsonIgnore]
		public bool HasTitle => !string.IsNullOrWhiteSpace(Title);

		/// <summary>
		/// Gets or sets the icon drawn before the title.
		/// </summary>
		public VisualIconKind Icon
		{
			get;
			set
			{
				if (SetProperty(ref field, value))
					RaisePropertyChanged(nameof(HasIcon));
			}
		} = VisualIconKind.None;

		/// <summary>
		/// Gets a value indicating whether the tab has an icon of its own.
		/// </summary>
		[BsonIgnore]
		public bool HasIcon => !Icon.IsNone;

		/// <summary>
		/// Gets or sets a value indicating whether the user may remove the tab, together with its payload.
		/// </summary>
		public bool CanClose
		{
			get;
			set => SetProperty(ref field, value);
		}

		/// <summary>
		/// Gets or sets a value indicating whether the tab is the one whose payload covers the conversation.
		/// </summary>
		/// <remarks>
		/// Only one tab is selected at a time, which the host enforces. Persisted, because opening a tab is a
		/// gesture like dragging a window, and the tab should still be open after a reload.
		/// </remarks>
		public bool IsSelected
		{
			get;
			set => SetProperty(ref field, value);
		}

		/// <summary>
		/// Takes over what a script's update must not reset: the title and the icon when the update brought none,
		/// and whether it is the open one.
		/// </summary>
		/// <param name="previous">The envelope this item replaces under one replacement key.</param>
		public override void InheritFrom(AdditionalChatDataItemBase previous)
		{
			base.InheritFrom(previous);

			if (previous is not AdditionalChatDataTabItem tab)
				return;

			if (string.IsNullOrWhiteSpace(Title))
				Title = tab.Title;

			if (Icon.IsNone)
				Icon = tab.Icon;

			IsSelected = tab.IsSelected;
		}
	}
}
