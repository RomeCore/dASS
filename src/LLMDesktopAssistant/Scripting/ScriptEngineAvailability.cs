namespace LLMDesktopAssistant.Scripting
{
	/// <summary>
	/// Whether a script language can run here, and what its interpreter reports about itself.
	/// </summary>
	/// <param name="Available">Whether the interpreter was found and answered.</param>
	/// <param name="Version">The interpreter's own version line, or <see langword="null"/>.</param>
	/// <param name="Problem">Why the language is unavailable, or <see langword="null"/>.</param>
	/// <param name="ExecutablePath">
	/// The executable that answered, or <see langword="null"/> when the language has nothing to locate (the shell finds
	/// python inside the venv on its own).
	/// </param>
	public readonly record struct ScriptEngineAvailability(bool Available, string? Version, string? Problem,
		string? ExecutablePath = null);
}
