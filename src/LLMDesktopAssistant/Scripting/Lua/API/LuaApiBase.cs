using AsyncLua.Values;

namespace LLMDesktopAssistant.Scripting.Lua.API
{
	/// <summary>
	/// Provides a base class for Lua API parts (namespaces).
	/// These should be registered using <see cref="LuaApiAttribute"/>.
	/// </summary>
	public abstract class LuaApiBase : Disposable
	{
		/// <summary>
		/// Gets the name of the Lua namespace for this API. Examples: "dass.agents", "fs", "image", etc.
		/// </summary>
		public virtual string? Namespace => null;

		/// <summary>
		/// Gets the manuals for this API. If null, no manuals are provided.
		/// These are used for LLM to understand what functions/properties are available in this API.
		/// </summary>
		public virtual string? Manuals => null;

		/// <summary>
		/// Populates the given Lua table with this API's functions and properties.
		/// </summary>
		/// <param name="globals">The global Lua table to populate.</param>
		/// <param name="ns">The namespace Lua table to populate. This is located at <see cref="Namespace"/> in the global table.</param>
		/// <param name="luaService">The Lua service instance that have called this method.</param>
		/// <returns>An optional action that will be executed upon cleanup.</returns>
		public abstract Action? Populate(LuaTable globals, LuaTable ns, LuaService luaService);
	}
}
