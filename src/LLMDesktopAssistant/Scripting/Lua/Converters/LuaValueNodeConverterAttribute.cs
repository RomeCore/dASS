namespace LLMDesktopAssistant.Scripting.Lua.Converters
{
	/// <summary>
	/// Registers a <see cref="LuaValueNodeConverter"/> inside <see cref="LuaValueNodeConverterRegistry"/>.
	/// </summary>
	/// <param name="order">The order of the converter. Converters with lower order are applied first.</param>
	public class LuaValueNodeConverterAttribute(int order = 0) : Attribute
	{
		public int Order { get; } = order;
	}
}
