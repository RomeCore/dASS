using LLMDesktopAssistant.Services;
using LLMDesktopAssistant.SlashCommands;
using LLMDesktopAssistant.SlashCommands.Arguments;
using LLMDesktopAssistant.SlashCommands.Execution;
using LLMDesktopAssistant.SlashCommands.Scripting;

namespace LLMDesktopAssistant.Scripting.Lua.Addons
{
	/// <summary>
	/// Lua implementation of <see cref="IScriptableSlashCommandEngine"/>. Handles commands written in Lua with YAML
	/// frontmatter in <c>--[[ ... ]]</c> blocks.
	/// </summary>
	/// <remarks>
	/// The engine itself is stateless and app-scoped; the chat-scoped <see cref="LuaService"/> is resolved per
	/// execution from the command context, exactly like the Lua tool engine does.
	/// </remarks>
	[Service(typeof(IScriptableSlashCommandEngine))]
	public class LuaSlashCommandEngine : IScriptableSlashCommandEngine
	{
		/// <inheritdoc/>
		public IReadOnlyList<IScriptEngineDescriptor> Descriptors { get; } = [LuaScriptEngineDescriptor.Instance];

		/// <inheritdoc/>
		public ISlashCommandExecutor CreateExecutor(SlashCommandInfo command, SlashCommandArgumentSchema? argumentSchema)
			=> new LuaScriptCommandExecutor(command, argumentSchema);
	}
}
