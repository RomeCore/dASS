namespace LLMDesktopAssistant.Tools
{
	/// <summary>
	/// Declares that an executor parameter is a filesystem path - the one thing JSON Schema cannot say,
	/// because a path is a plain string there. The flags narrow what the path may point to: a specifier
	/// compiles every path the same way, but a tool card and the slash-command autocomplete want to know
	/// whether to offer files, directories, or either.
	/// </summary>
	[AttributeUsage(AttributeTargets.Parameter)]
	public sealed class PathArgumentAttribute : Attribute
	{
		/// <summary>
		/// Whether the path may point to a file.
		/// </summary>
		public bool File { get; set; } = true;

		/// <summary>
		/// Whether the path may point to a directory.
		/// </summary>
		public bool Directory { get; set; } = true;
	}
}
