using System.Text.Json.Nodes;
using LLMDesktopAssistant.SlashCommands.Execution;

namespace LLMDesktopAssistant.Scripting.CSX
{
	/// <summary>
	/// The globals object passed to C# scripts executed as slash commands by
	/// <see cref="CSharpScriptSlashCommandEngine"/>.
	/// </summary>
	public sealed class CSharpScriptCommandGlobals
	{
		/// <summary>
		/// The command's bound arguments as a JSON tree
		/// (<c>{ "positionals": [...], "keyed": {...}, "rest": "...", "raw": "..." }</c>).
		/// </summary>
		public required JsonNode? CommandArgs { get; init; }

		/// <summary>The context of the running command.</summary>
		public required SlashCommandExecutionContext Context { get; init; }

		/// <summary>The working directory of the chat the command runs in.</summary>
		public required string Workdir { get; init; }
	}
}
