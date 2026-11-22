using AsyncLua.Values;
using LLMDesktopAssistant.StructuredValues;

namespace LLMDesktopAssistant.Scripting.Lua.Converters
{
	public abstract class LuaValueNodeConverter
	{
		/// <summary>
		/// Converts a structured value to a Lua value.
		/// </summary>
		/// <param name="value">The structured value to convert.</param>
		/// <returns>The Lua value.</returns>
		public abstract LuaValue? TryConvertToLua(INodeValue? value);

		/// <summary>
		/// Converts a Lua value to a structured value.
		/// </summary>
		/// <param name="value">The Lua value to convert.</param>
		/// <param name="isConst">Whether to convert value to Const* instead of Reactive*.</param>
		/// <returns></returns>
		public abstract INodeValue? TryConvertFromLua(LuaValue? value, bool isConst);
	}
}
