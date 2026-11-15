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
		private static readonly AsyncLocal<Ambient?> _current = new();

		/// <summary>The tool execution the current script belongs to, or <see langword="null"/>.</summary>
		public static IToolExecutionContext? Tool => _current.Value?.Tool;

		/// <summary>The slash-command execution the current script belongs to, or <see langword="null"/>.</summary>
		public static SlashCommandExecutionContext? Command => _current.Value?.Command;

		/// <summary>The tool result of the current tool execution, or <see langword="null"/>.</summary>
		internal static LuaToolResult? ToolResult => _current.Value?.Result;

		/// <summary>
		/// The chat message the current flow belongs to - the one being generated or inserted - or
		/// <see langword="null"/> when the script runs outside such a window.
		/// </summary>
		public static BranchedMessage? Message => _current.Value?.Message;

		/// <summary>
		/// Sets the ambient execution context for the current async flow and everything it awaits.
		/// </summary>
		/// <param name="tool">The tool execution the script belongs to, or <see langword="null"/>.</param>
		/// <param name="command">The slash-command execution the script belongs to, or <see langword="null"/>.</param>
		/// <remarks>
		/// Deliberately no restore-on-exit: an <see cref="AsyncLocal{T}"/> value flows down into awaited calls and does
		/// not leak back out of an async method, so whoever sets it owns it for the rest of its own flow. Writing the
		/// previous value back would be dead code.
		/// </remarks>
		public static void Set(IToolExecutionContext? tool = null, SlashCommandExecutionContext? command = null)
			=> SetAmbient(tool, command, tool is null ? null : new LuaToolResult(tool.Call));

		/// <summary>
		/// Sets the ambient tool execution together with the result the script writes through.
		/// </summary>
		/// <param name="tool">The tool execution the script belongs to.</param>
		/// <param name="result">The result of that execution.</param>
		internal static void SetTool(IToolExecutionContext tool, LuaToolResult result)
			=> SetAmbient(tool, null, result);

		/// <summary>
		/// Opens or closes the ambient message window for the current async flow.
		/// </summary>
		/// <param name="message">The message the flow belongs to, or <see langword="null"/> to close the window.</param>
		/// <remarks>
		/// Unlike <see cref="Set"/> this replaces the slot instead of merging it: closing a window means setting it to
		/// <see langword="null"/>, and a merge would keep the previous value alive.
		/// </remarks>
		public static void SetMessage(BranchedMessage? message)
			=> _current.Value = new Ambient(Tool, Command, ToolResult, message);

		private static void SetAmbient(IToolExecutionContext? tool, SlashCommandExecutionContext? command,
			LuaToolResult? result)
			=> _current.Value = new Ambient(tool ?? Tool, command ?? Command, result ?? ToolResult, Message);

		private sealed record Ambient(IToolExecutionContext? Tool, SlashCommandExecutionContext? Command,
			LuaToolResult? Result, BranchedMessage? Message);
	}
}
