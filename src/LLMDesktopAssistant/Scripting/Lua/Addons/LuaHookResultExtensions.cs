using AsyncLua.Values;
using LLMDesktopAssistant.Hooks;
using LLMDesktopAssistant.StructuredValues.Converters;
using Serilog;

namespace LLMDesktopAssistant.Scripting.Lua.Addons
{
	public static class LuaHookResultExtensions
	{
		/// <summary>
		/// Reads a Lua hook's return values as the hook result: <c>value, blocking_message</c>.
		/// </summary>
		/// <remarks>
		/// This is the Lua projection of <see cref="HookExecutionResult"/>: the first value stays the chain value, a
		/// non-empty string in the second position ends the chain. Any other value there is a script mistake, reported
		/// as no blocking at all rather than as a veto.
		/// </remarks>
		/// <param name="result">What the hook returned.</param>
		/// <param name="hookType">The event, for the log line only.</param>
		/// <param name="hookTarget">The target, for the log line only.</param>
		public static HookExecutionResult ToHookResult(this LuaTuple result, string hookType, string? hookTarget)
		{
			var value = result.Count > 0 ? result[0].ToConstNodeValue(tolerant: true) : null;

			if (result.Count < 2 || result[1] is LuaNil)
				return new HookExecutionResult(value, null);

			if (result[1] is LuaString message)
				return new HookExecutionResult(value, string.IsNullOrEmpty(message.Value) ? null : message.Value);

			Log.Warning("A Lua hook of type {HookType} on target {HookTarget} returned a second value that is not a " +
				"string; it is ignored and the hook counts as having no opinion.", hookType, hookTarget);
			return new HookExecutionResult(value, null);
		}
	}
}
