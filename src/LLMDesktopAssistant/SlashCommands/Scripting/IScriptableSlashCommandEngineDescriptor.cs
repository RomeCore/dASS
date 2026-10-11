using LLMDesktopAssistant.Scripting;

namespace LLMDesktopAssistant.SlashCommands.Scripting
{
	/// <summary>
	/// The descriptor of a scriptable slash-command engine: the language it handles and the file-level syntax
	/// (frontmatter markers and extensions) it claims.
	/// </summary>
	public interface IScriptableSlashCommandEngineDescriptor
	{
		/// <summary>The language this engine handles.</summary>
		ScriptLanguageType Language { get; }

		/// <summary>The file extensions this engine claims, ordered by priority.</summary>
		string[] Extensions { get; }

		/// <summary>The marker that starts the frontmatter block.</summary>
		string FrontmatterStart { get; }

		/// <summary>The marker that ends the frontmatter block.</summary>
		string FrontmatterEnd { get; }
	}
}
