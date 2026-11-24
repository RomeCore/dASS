using AsyncLua.Values;
using LLMDesktopAssistant.Utils;

namespace LLMDesktopAssistant.Scripting.Lua.Logs
{
	/// <summary>
	/// Renders a buffered log event into the table the Lua side documents.
	/// </summary>
	public static class LuaLogEntry
	{
		/// <summary>
		/// Builds the <c>{ seq, timestamp, level, message, template, properties, exception }</c> table.
		/// </summary>
		public static LuaTable ToLua(this RenderedLogEvent entry)
		{
			var properties = new LuaTable();
			foreach (var (key, value) in entry.Properties)
				properties[key] = new LuaString(value);

			return new LuaTable
			{
				["seq"] = new LuaNumber(entry.Sequence),
				["timestamp"] = new LuaNumber(entry.Timestamp.ToUnixTimeMilliseconds() / 1000.0),
				["level"] = new LuaString(LuaLogLevels.Name(entry.Level)),
				["message"] = new LuaString(entry.Rendered),
				["template"] = new LuaString(entry.Template),
				["properties"] = properties,
				["exception"] = entry.Exception is { } exception ? new LuaString(exception.ToString()) : LuaNil.Instance,
			};
		}
	}
}
