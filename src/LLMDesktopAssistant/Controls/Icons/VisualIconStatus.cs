namespace LLMDesktopAssistant.Controls.Icons
{
	/// <summary>
	/// The semantic verdict produced by <see cref="VisualIconDataHandler"/>.
	/// </summary>
	public enum VisualIconStatus
	{
		/// <summary>Nothing to render ("no icon").</summary>
		None,

		/// <summary>The icon resolves to valid path data.</summary>
		Ok,

		/// <summary>The pack name is not recognised.</summary>
		InvalidPack,

		/// <summary>The pack is known but its data cannot be resolved.</summary>
		InvalidData,
	}
}
