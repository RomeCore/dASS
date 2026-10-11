using LLMDesktopAssistant.Scripting;
using LLMDesktopAssistant.SlashCommands.Arguments;
using LLMDesktopAssistant.SlashCommands.Execution;

namespace LLMDesktopAssistant.SlashCommands.Scripting
{
	/// <summary>
	/// Defines a scripting engine that can run slash-command addons written in a specific language (Lua, Python or
	/// C# script). Each engine knows the frontmatter syntax and how to build the command's executor.
	/// </summary>
	/// <remarks>
	/// Deliberately a separate family from the tool engines: a command and a tool differ in what they receive, what
	/// they may do and how their arguments are shaped, so a shared abstraction would be a leaky one. The obvious parts
	/// mirror <c>IScriptableToolEngine</c>; unlike it, there is no <c>Template</c>/<c>Examples</c>, and the argument
	/// schema is passed in (for a command it lives on the executor, not on the addon).
	/// </remarks>
	public interface IScriptableSlashCommandEngine
	{
		/// <summary>The scripting language this engine handles.</summary>
		ScriptLanguageType Language { get; }

		/// <summary>Additional information about serialization and extensions.</summary>
		IScriptableSlashCommandEngineDescriptor Descriptor { get; }

		/// <summary>
		/// Builds the executor that runs the command's body.
		/// </summary>
		/// <param name="command">The command to build the executor for.</param>
		/// <param name="argumentSchema">
		/// The argument schema parsed from the command's frontmatter, or <see langword="null"/> when it declares none.
		/// </param>
		ISlashCommandExecutor CreateExecutor(SlashCommandInfo command, SlashCommandArgumentSchema? argumentSchema);
	}
}
