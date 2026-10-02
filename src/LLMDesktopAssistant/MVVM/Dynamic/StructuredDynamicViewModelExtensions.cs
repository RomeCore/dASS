using AsyncLua;
using AsyncLua.Values;
using LLMDesktopAssistant.StructuredValues;

namespace LLMDesktopAssistant.MVVM.Dynamic
{
	/// <summary>
	/// Convenience factory methods for creating dynamic view models from structured values.
	/// </summary>
	public static class StructuredDynamicViewModelExtensions
	{
		/// <summary>Creates a dynamic view model over a structured node value.</summary>
		/// <param name="value">The root dictionary or array node value.</param>
		public static NodeValueDynamicViewModel ToDynamicViewModel(this INodeValue value)
		{
			return new NodeValueDynamicViewModel(value);
		}

		/// <summary>
		/// Creates a dynamic view model over a Lua table, using the supplied calling context to
		/// invoke callbacks and commands.
		/// </summary>
		/// <param name="table">The root Lua table.</param>
		/// <param name="context">The calling context captured when the table was created.</param>
		public static LuaValueDynamicViewModel ToDynamicViewModel(this LuaTable table, LuaCallingContext context)
		{
			return new LuaValueDynamicViewModel(table, context);
		}
	}
}
