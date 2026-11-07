namespace LLMDesktopAssistant.Utils.Patterns
{
	/// <summary>
	/// The case rule of the file system the process runs on.
	/// </summary>
	public static class GlobCase
	{
		/// <summary>
		/// Ignore case on Windows, ordinal elsewhere. Everything that scans uses it, so one pattern
		/// means the same thing in a tool and in a script.
		/// </summary>
		/// <remarks>
		/// macOS is deliberately excluded: its file systems may be formatted either way.
		/// </remarks>
		public static GlobOptions PlatformDefault => OperatingSystem.IsWindows()
			? GlobOptions.IgnoreCase
			: GlobOptions.None;
	}
}
