using LLMDesktopAssistant.Scripting;
using LLMDesktopAssistant.SlashCommands.Arguments;
using LLMDesktopAssistant.SlashCommands.Execution;

namespace LLMDesktopAssistant.SlashCommands.Scripting
{
	/// <summary>
	/// Defines a scripting engine that can run slash-command addons written in a specific language (Lua, Python,
	/// Bash, ...). Each engine knows how to build the command's executor.
	/// </summary>
	public interface IScriptableSlashCommandEngine : IScriptableAddonEngine
	{
		/// <summary>
		/// The languages this engine serves: extensions, frontmatter syntax and capabilities, one descriptor per
		/// language. A multi-language adapter answers with the union; a single-language engine answers with one item.
		/// </summary>
		IReadOnlyList<IScriptEngineDescriptor> Descriptors { get; }

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
