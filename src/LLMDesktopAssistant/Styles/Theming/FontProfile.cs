namespace LLMDesktopAssistant.Styles.Theming
{
	/// <summary>
	/// The density of the type scale: it changes the base size and the ratio between steps.
	/// </summary>
	public enum FontProfile
	{
		/// <summary>
		/// Small base and tight ratio, for phones and small screens.
		/// </summary>
		Compact,

		/// <summary>
		/// The default desktop scale.
		/// </summary>
		Normal,

		/// <summary>
		/// Large base for high resolution monitors and poor sight.
		/// </summary>
		Large,
	}
}
