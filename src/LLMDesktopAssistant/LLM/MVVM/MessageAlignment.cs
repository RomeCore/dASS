using Avalonia.Controls;
using Avalonia.Layout;

namespace LLMDesktopAssistant.LLM.MVVM
{
	/// <summary>
	/// The horizontal alignment of a message's content, derived from the sender's role.
	/// Content that is aligned as a block uses <see cref="Horizontal"/>, wrapped content uses <see cref="Wrap"/>.
	/// </summary>
	public readonly record struct MessageAlignment(HorizontalAlignment Horizontal, WrapPanelItemsAlignment Wrap)
	{
		/// <summary>
		/// Content sits on the left edge: agent messages.
		/// </summary>
		public static MessageAlignment Left { get; } = new(HorizontalAlignment.Left, WrapPanelItemsAlignment.Start);

		/// <summary>
		/// Content sits on the right edge: user messages and agents that identify as a user.
		/// </summary>
		public static MessageAlignment Right { get; } = new(HorizontalAlignment.Right, WrapPanelItemsAlignment.End);

		/// <summary>
		/// Gets the placement of a tool call flyout: it opens below the target, anchored to the same horizontal
		/// edge the content is aligned to.
		/// </summary>
		public PlacementMode FlyoutPlacement => Horizontal == HorizontalAlignment.Right
			? PlacementMode.BottomEdgeAlignedRight
			: PlacementMode.BottomEdgeAlignedLeft;
	}
}
