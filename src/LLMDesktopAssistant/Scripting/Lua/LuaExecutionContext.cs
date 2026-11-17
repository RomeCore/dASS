using LLMDesktopAssistant.Agents;
using LLMDesktopAssistant.LLM.Domain;
using LLMDesktopAssistant.SlashCommands.Execution;
using LLMDesktopAssistant.Tools.Execution.Domain;

namespace LLMDesktopAssistant.Scripting.Lua
{
	/// <summary>
	/// The ambient execution context of the Lua script currently running, carried through
	/// <see cref="AsyncLocal{T}"/> rather than as Lua globals.
	/// </summary>
	public static class LuaExecutionContext
	{
		private static readonly AsyncLocal<IToolExecutionContext?> _tool = new();
		private static readonly AsyncLocal<LuaToolResult?> _toolResult = new();
		private static readonly AsyncLocal<SlashCommandExecutionContext?> _command = new();
		private static readonly AsyncLocal<BranchedMessage?> _message = new();
		private static readonly AsyncLocal<ChatAgentDescriptor?> _agent = new();

		/// <summary>
		/// The tool execution the current script belongs to, or <see langword="null"/>.
		/// </summary>
		public static IToolExecutionContext? Tool
		{
			get => _tool.Value;
			set => _tool.Value = value;
		}

		/// <summary>
		/// The tool result of the current tool execution, or <see langword="null"/>.
		/// </summary>
		public static LuaToolResult? ToolResult
		{
			get => _toolResult.Value;
			set => _toolResult.Value = value;
		}

		/// <summary>
		/// The slash-command execution the current script belongs to, or <see langword="null"/>.
		/// </summary>
		public static SlashCommandExecutionContext? Command
		{
			get => _command.Value;
			set => _command.Value = value;
		}

		/// <summary>
		/// The chat message the current flow belongs to - the one being generated or inserted - or
		/// <see langword="null"/> when the script runs outside such a window.
		/// </summary>
		public static BranchedMessage? Message
		{
			get => _message.Value;
			set => _message.Value = value;
		}

		/// <summary>
		/// The chat agent the current flow belongs to, or <see langword="null"/>.
		/// </summary>
		public static ChatAgentDescriptor? Agent
		{
			get => _agent.Value;
			set => _agent.Value = value;
		}
	}
}
