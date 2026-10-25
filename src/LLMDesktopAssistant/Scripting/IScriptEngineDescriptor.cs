namespace LLMDesktopAssistant.Scripting
{
	/// <summary>
	/// What a scriptable language declares: the file extensions it owns, the frontmatter syntax of those files, and
	/// what it can do beyond running a body.
	/// </summary>
	/// <remarks>
	/// There is one descriptor per language, not per addon kind: one adapter can serve several languages, so the
	/// parser has to resolve the language from the <em>file</em>, not from the engine. This is why the kind engine
	/// interfaces expose a list of descriptors instead of a single one.
	/// </remarks>
	public interface IScriptEngineDescriptor
	{
		/// <summary>
		/// The scripting language this descriptor describes.
		/// </summary>
		ScriptLanguageType Language { get; }

		/// <summary>
		/// The file extensions this language owns, in priority order (with the leading dot).
		/// </summary>
		string[] Extensions { get; }

		/// <summary>
		/// The frontmatter start marker (for example '"""', '--[[' or '---').
		/// </summary>
		string FrontmatterStart { get; }

		/// <summary>
		/// The frontmatter end marker.
		/// </summary>
		string FrontmatterEnd { get; }

		/// <summary>
		/// The exact prefix of every frontmatter line, or <see langword="null"/> for block markers.
		/// Used by languages without block comments - '# ' for the shell languages, 'REM ' for Batch - so the
		/// whole block stays a comment and the file remains runnable as-is.
		/// </summary>
		string? FrontmatterLinePrefix { get; }

		/// <summary>
		/// What this language can do beyond running a body.
		/// </summary>
		ScriptEngineCapabilities Capabilities { get; }
	}
}
