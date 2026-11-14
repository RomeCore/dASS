namespace LLMDesktopAssistant.LLM.MVVM.Additional.Chat
{
	/// <summary>
	/// The presentation state an overlay window is in.
	/// </summary>
	/// <remarks>
	/// The members carry explicit numbers because the value is stored as an integer: reordering them would
	/// silently turn a restored window into a different state. Collapsing and hiding are the user's, never the
	/// payload's - a payload's own <see cref="AdditionalChatData.IsVisible"/> stays a separate axis.
	/// </remarks>
	public enum AdditionalChatDataOverlayState
	{
		/// <summary>
		/// The window shows its header and its body.
		/// </summary>
		Normal = 0,

		/// <summary>
		/// The window shows its header only: the body stays alive and is not drawn.
		/// </summary>
		Collapsed = 1,

		/// <summary>
		/// The window is out of sight and is restored from the tray.
		/// </summary>
		Hidden = 2,
	}
}
