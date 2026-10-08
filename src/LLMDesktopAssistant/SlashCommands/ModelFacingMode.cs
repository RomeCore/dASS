namespace LLMDesktopAssistant.SlashCommands
{
	/// <summary>
	/// How the message a slash command produced is presented to the model.
	/// </summary>
	/// <remarks>
	/// v1 uses <see cref="Raw"/> only: the agent sees the raw message content and a machine-readable fingerprint is
	/// written alongside it. <see cref="Neutral"/> and <see cref="Hidden"/> are reserved for later activation.
	/// </remarks>
	public enum ModelFacingMode
	{
		/// <summary>
		/// The model sees the raw message content.
		/// </summary>
		Raw = 0,

		/// <summary>
		/// The model sees a neutral rendering of the command instead of its raw text.
		/// </summary>
		Neutral = 1,

		/// <summary>
		/// The command and its message are hidden from the model.
		/// </summary>
		Hidden = 2
	}
}
