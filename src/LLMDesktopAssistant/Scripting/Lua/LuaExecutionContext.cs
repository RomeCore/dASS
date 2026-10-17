using LLMDesktopAssistant.SlashCommands.Execution;
using LLMDesktopAssistant.Tools.Execution.Domain;

namespace LLMDesktopAssistant.Scripting.Lua
{
	/// <summary>
	/// The ambient execution context of the Lua script currently running, carried through
	/// <see cref="AsyncLocal{T}"/> rather than as Lua globals.
	/// </summary>
	/// <remarks>
	/// The context used to be injected into <c>_G</c> as <c>_dass_tool_ctx</c>/<c>_dass_tool_result</c>. That made
	/// non-isolated execution (running against the shared, persistent Lua state) impossible without stomping the
	/// globals of a concurrent run. An <see cref="AsyncLocal{T}"/> is scoped to the current async flow, so the context
	/// is correct per execution regardless of whether globals are cloned.
	/// </remarks>
	public static class LuaExecutionContext
	{
		private static readonly AsyncLocal<Ambient?> _current = new();

		/// <summary>The tool execution the current script belongs to, or <see langword="null"/>.</summary>
		public static IToolExecutionContext? Tool => _current.Value?.Tool;

		/// <summary>The slash-command execution the current script belongs to, or <see langword="null"/>.</summary>
		public static SlashCommandExecutionContext? Command => _current.Value?.Command;

		/// <summary>The tool result of the current tool execution, or <see langword="null"/>.</summary>
		internal static LuaToolResult? Result => _current.Value?.Result;

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

		private static void SetAmbient(IToolExecutionContext? tool, SlashCommandExecutionContext? command,
			LuaToolResult? result)
			=> _current.Value = new Ambient(tool, command, result);

		private sealed record Ambient(IToolExecutionContext? Tool, SlashCommandExecutionContext? Command,
			LuaToolResult? Result);
	}
}
