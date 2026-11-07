namespace LLMDesktopAssistant.Scripting
{
	/// <summary>
	/// Whether a script language can run here, and what its interpreter reports about itself.
	/// </summary>
	/// <param name="Available">Whether the interpreter was found and answered.</param>
	/// <param name="Version">The interpreter's own version line, or <see langword="null"/>.</param>
	/// <param name="Problem">Why the language is unavailable, or <see langword="null"/>.</param>
	public readonly record struct ScriptEngineAvailability(bool Available, string? Version, string? Problem);
}
