using AsyncLua;
using AsyncLua.Values;

namespace LLMDesktopAssistant.Scripting.Lua
{
	/// <summary>
	/// The parsing contract the append calls share: one argument table, keys checked against the call's allow-list,
	/// values read into typed members by the derived options.
	/// </summary>
	/// <remarks>
	/// Parsing is strict on purpose: an unknown key or a wrong value type is a script mistake, and a silent
	/// fallback would create something the author cannot find. Only the mechanics live here - the key sets differ
	/// per call, because a part is described by what a chip shows while a payload by where it is shown.
	/// </remarks>
	public abstract class LuaAttachmentOptions
	{
		/// <summary>
		/// Checks that the argument is a table and that every key is one the call accepts.
		/// </summary>
		/// <param name="value">The argument the script passed.</param>
		/// <param name="call">The Lua call being executed, for the error message.</param>
		/// <param name="allowedKeys">The keys this call accepts, in the order the error lists them.</param>
		/// <param name="expectation">What the call expects instead of a table, for the error message.</param>
		/// <returns>The table, ready to be read.</returns>
		/// <exception cref="LuaRuntimeException">The argument is not a table or carries a key outside the allow-list.</exception>
		protected static LuaTable OpenTable(object? value, string call, string[] allowedKeys, string expectation)
		{
			if (value is not LuaTable table)
				throw new LuaRuntimeException($"{call}: {expectation}");

			foreach (var (key, _) in table)
			{
				if (key is not LuaString name)
					throw new LuaRuntimeException(
						$"{call}: keys must be strings. Expected one of: {string.Join(", ", allowedKeys)}.");

				if (!allowedKeys.Contains(name.Value))
					throw new LuaRuntimeException(
						$"{call}: '{name.Value}' is unknown. Expected one of: {string.Join(", ", allowedKeys)}.");
			}

			return table;
		}

		/// <summary>
		/// Reads a string member, or <see langword="null"/> when the script did not set it.
		/// </summary>
		/// <exception cref="LuaRuntimeException">The member holds a value of another type.</exception>
		protected static string? ReadString(LuaTable table, string key, string call)
			=> table.Get(key) switch
			{
				LuaNil => null,
				LuaString value => value.Value,
				_ => throw new LuaRuntimeException($"{call}: '{key}' must be a string.")
			};

		/// <summary>
		/// Reads a boolean member, or <see langword="null"/> when the script did not set it.
		/// </summary>
		/// <exception cref="LuaRuntimeException">The member holds a value of another type.</exception>
		protected static bool? ReadBool(LuaTable table, string key, string call)
			=> table.Get(key) switch
			{
				LuaNil => null,
				LuaBoolean value => value.Value,
				_ => throw new LuaRuntimeException($"{call}: '{key}' must be a boolean.")
			};
	}
}
