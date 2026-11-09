using AsyncLua;

namespace LLMDesktopAssistant.Desktop.Scripting.Lua
{
	/// <summary>
	/// Parses the numeric arguments shared by the process and terminal handles.
	/// </summary>
	internal static class LuaProcessArguments
	{
		/// <summary>
		/// Parses an optional <c>ms</c> argument: absent waits forever, zero does not wait at all.
		/// </summary>
		/// <param name="value">The boxed argument the Lua runtime produced, or <see langword="null"/> when it was omitted.</param>
		/// <param name="function">The function name used in an error message.</param>
		/// <returns>The timeout in milliseconds, or <see langword="null"/> for no limit.</returns>
		internal static int? ParseTimeout(object? value, string function)
		{
			if (value is null)
				return null;

			if (value is double number)
			{
				if (double.IsNaN(number) || number < 0)
					throw new LuaRuntimeException($"{function}([ms]): ms must be a non-negative number.");

				return number >= int.MaxValue ? int.MaxValue : (int)number;
			}

			throw new LuaRuntimeException($"{function}([ms]): ms must be a number.");
		}
	}
}
