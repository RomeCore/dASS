using AsyncLua;
using AsyncLua.Values;
using LLMDesktopAssistant.SlashCommands.Execution;
using LLMDesktopAssistant.Tools;

namespace LLMDesktopAssistant.Scripting.Lua
{
	public static class LuaExtensions
	{
		/// <summary>
		/// Determines whether a Lua table should be serialized as a JSON array.
		/// A table is considered an array if it has a contiguous sequence of
		/// integer keys from 1 to <see cref="Table.Length"/> and no keys
		/// outside that range.
		/// </summary>
		public static bool IsArrayTable(this LuaTable table)
		{
			int len = table.Length;

			if (len == 0 && table.Keys.Count() == 0)
				return true;

			if (len == 0)
				return false;

			for (int i = 1; i <= len; i++)
			{
				if (!table.Keys.Contains(new LuaNumber(i)))
					return false;
			}

			foreach (var key in table.Keys)
			{
				if (key is not LuaNumber numberKey)
					return false;
				double num = numberKey.Value;
				if (num < 1 || num > len || num != Math.Truncate(num))
					return false;
			}

			return true;
		}

		public static T? TryGetUserData<T>(this LuaCallingContext ctx, string variableName)
			where T : class
		{
			var globals = ctx.Globals;
			return globals.Get<T>(variableName);
		}

		/// <summary>
		/// Gets the tool execution the current script belongs to, or <see langword="null"/> when the script is not
		/// running as a tool. Read from the ambient <see cref="LuaExecutionContext"/>, not from a global.
		/// </summary>
		public static ToolExecutionContext? TryGetToolExecutionContext(this LuaCallingContext ctx)
			=> LuaExecutionContext.Tool;

		/// <summary>
		/// Gets the slash-command execution the current script belongs to, or <see langword="null"/> when the script is
		/// not running as a slash command.
		/// </summary>
		public static SlashCommandExecutionContext? TryGetCommandExecutionContext(this LuaCallingContext ctx)
			=> LuaExecutionContext.Command;

		/// <summary>
		/// Gets the reactive result of the current tool execution, or <see langword="null"/>.
		/// </summary>
		public static ReactiveToolResult? TryGetReactiveToolResult(this LuaCallingContext ctx)
			=> LuaExecutionContext.ReactiveResult;
	}
}
