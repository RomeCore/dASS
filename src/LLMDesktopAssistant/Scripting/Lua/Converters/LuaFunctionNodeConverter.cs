using System.Diagnostics.CodeAnalysis;
using System.Runtime.CompilerServices;
using AsyncLua.Interpreting;
using AsyncLua.Values;
using LLMDesktopAssistant.StructuredValues;
using LLMDesktopAssistant.StructuredValues.Const;
using LLMDesktopAssistant.StructuredValues.Reactive;
using Serilog;

namespace LLMDesktopAssistant.Scripting.Lua.Converters
{
	[LuaValueNodeConverter(order: 0)]
	public class LuaFunctionNodeConverter : LuaValueNodeConverter
	{
		public const string FunctionMarker = "\u0000dass:lua-fn:v1:";
		private static readonly ConditionalWeakTable<LuaNativeFunction, string> FunctionDumps = [];

		public override INodeValue? TryConvertFromLua(LuaValue? value, bool isConst)
		{
			if (value is not LuaNativeFunction function)
				return null;

			if (isConst)
				return new ConstNodeStringValue { Value = DumpFunction(function) };
			return new ReactiveNodeStringValue { Value = DumpFunction(function) };
		}

		public override LuaValue? TryConvertToLua(INodeValue? value)
		{
			if (value is INodeStringValue stringValue && !string.IsNullOrEmpty(stringValue.Value) &&
				TryReviveFunction(stringValue.Value, out var function))
				return function;
			return null;
		}

		/// <summary>
		/// Serializes a Lua function into the marker string that carries it through a node tree.
		/// </summary>
		/// <remarks>
		/// Only the prototype travels: upvalues and the per-closure environment stay behind, so the revived
		/// function reads globals from the calling context it is invoked with. Dumps are cached per function
		/// because a widget and a store each re-serialize their whole value on every change.
		/// </remarks>
		/// <param name="function">The function to serialize.</param>
		/// <returns>The marker string, ready to be kept as a <see cref="ConstNodeStringValue"/>.</returns>
		public static string DumpFunction(LuaNativeFunction function)
		{
			return FunctionDumps.GetValue(function, static f =>
				FunctionMarker + Convert.ToBase64String(PrototypeSerializer.Serialize(f.Prototype)));
		}

		/// <summary>
		/// Revives a function serialized by <see cref="DumpFunction"/>.
		/// </summary>
		/// <remarks>
		/// A string that carries the marker but does not deserialize (bytecode from another build, a truncated
		/// payload) stays a string: the caller keeps the original value, so the next save writes it back
		/// verbatim instead of erasing the command from history.
		/// </remarks>
		public static bool TryReviveFunction(string text, [NotNullWhen(true)] out LuaNativeFunction? function)
		{
			function = null;

			if (!text.StartsWith(FunctionMarker, StringComparison.Ordinal))
				return false;

			try
			{
				var bytes = Convert.FromBase64String(text[FunctionMarker.Length..]);
				var prototype = PrototypeSerializer.Deserialize(bytes);

				// A dumped prototype keeps its upvalue count but not their values: every captured variable
				// comes back nil, which is the price of a function outliving the frame that created it.
				var upvalues = prototype.UpvalueDescriptions.Length > 0
					? Array.ConvertAll(prototype.UpvalueDescriptions, _ => Upvalue.CreateClosed(LuaNil.Instance))
					: [];

				function = new LuaNativeFunction(prototype, upvalues);
				return true;
			}
			catch (Exception ex)
			{
				Log.Warning(ex, "Failed to revive a serialized Lua function; keeping it as data.");
				return false;
			}
		}
	}
}
