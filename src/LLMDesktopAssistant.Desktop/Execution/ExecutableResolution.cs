namespace LLMDesktopAssistant.Desktop.Execution
{
	/// <summary>
	/// The outcome of looking for an interpreter executable: the path to run, or the reason there is none.
	/// </summary>
	/// <remarks>
	/// A missing interpreter is a runtime answer, not a load-time diagnostic: an addon is parsed once for the whole
	/// application, while the executable path is a per-chat setting. Returning the reason as a value is what lets a
	/// tool call report it and a settings check display it, instead of an exception escaping a script run.
	/// </remarks>
	/// <param name="Path">The executable to run, or <see langword="null"/> when nothing usable was found.</param>
	/// <param name="Problem">Why nothing was found, or <see langword="null"/> on success.</param>
	public readonly record struct ExecutableResolution(string? Path, string? Problem)
	{
		/// <summary>
		/// Whether an executable was found.
		/// </summary>
		public bool Found => Path is not null;
	}
}
