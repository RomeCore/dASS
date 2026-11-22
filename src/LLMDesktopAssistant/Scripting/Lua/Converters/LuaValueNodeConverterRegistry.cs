using LLMDesktopAssistant.Utils;

namespace LLMDesktopAssistant.Scripting.Lua.Converters
{
	public static class LuaValueNodeConverterRegistry
	{
		public static ImmutableArray<LuaValueNodeConverter> Converters { get; }

		static LuaValueNodeConverterRegistry()
		{
			Converters =
			[
				..  ReflectionUtility.GetTypesWithAttribute
					<LuaValueNodeConverter, LuaValueNodeConverterAttribute>()
					.OrderBy(a => a.Attribute.Order)
					.Select(a => a.Type)
					.Instantiate<LuaValueNodeConverter>(),
			];
		}
	}
}
