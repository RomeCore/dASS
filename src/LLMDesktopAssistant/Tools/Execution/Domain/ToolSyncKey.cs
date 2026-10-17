namespace LLMDesktopAssistant.Tools.Execution.Domain
{
	/// <summary>
	/// The kind of a synchronization key. The kind decides how two keys are compared.
	/// </summary>
	public enum ToolSyncKeyKind
	{
		/// <summary>
		/// A single file. Conflicts with the same file and with any directory that contains it.
		/// </summary>
		File,

		/// <summary>
		/// A directory. Conflicts with every file beneath it and with the same directory.
		/// </summary>
		Directory,

		/// <summary>
		/// An opaque tool-wide key — the old synchronization group. Conflicts only with an equal key.
		/// </summary>
		Custom,

		/// <summary>
		/// Every file of the working set. Conflicts with any <see cref="File"/>, <see cref="Directory"/> or
		/// <see cref="AllFiles"/> key; the value is ignored and normalized to an empty string.
		/// </summary>
		AllFiles
	}

	/// <summary>
	/// A synchronization key an invocation claims. Keys are compared hierarchically: a held
	/// <see cref="ToolSyncKeyKind.Directory"/> conflicts with any <see cref="ToolSyncKeyKind.File"/>
	/// beneath it, not only with an exactly equal key.
	/// </summary>
	/// <param name="Kind">The kind that selects the comparison rule.</param>
	/// <param name="Value">The key itself. Paths are already absolute — the analyzer absolutizes them.</param>
	public readonly record struct ToolSyncKey(ToolSyncKeyKind Kind, string Value);
}
