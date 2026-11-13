namespace LLMDesktopAssistant.LLM.MVVM.Additional.Chat
{
	/// <summary>
	/// An additional chat data item that asks for the input rail: the thin strip above or below the input,
	/// on its left or its right side.
	/// </summary>
	/// <remarks>
	/// The rail is one row of the input panel, so the payload it presents has to be compact: there is no title,
	/// no close button and no frame of its own to hang them on. Descending from
	/// <see cref="AdditionalChatDataItemBase"/> is what keeps the item out of the side panel.
	/// </remarks>
	public class AdditionalChatDataInputRailItem : AdditionalChatDataItemBase
	{
		/// <summary>
		/// Gets or sets the side of the strip the payload is drawn on.
		/// </summary>
		public ChatInputRailHorizontalPlacement HorizontalPlacement
		{
			get;
			set => SetProperty(ref field, value);
		} = ChatInputRailHorizontalPlacement.Left;

		/// <summary>
		/// Gets or sets which of the two strips, above or below the input, draws the payload.
		/// </summary>
		public ChatInputRailVerticalPlacement VerticalPlacement
		{
			get;
			set => SetProperty(ref field, value);
		} = ChatInputRailVerticalPlacement.Bottom;
	}
}
