using AsyncLua;
using AsyncLua.Values;

namespace LLMDesktopAssistant.Scripting.Lua
{
	/// <summary>
	/// One Lua function registered with <c>dass.chat.on_close</c>, exposed to Lua as a
	/// <c>close_hook</c> handle.
	/// </summary>
	public class LuaCloseHook
	{
		private readonly LuaChatClosingHandler _owner;

		/// <summary>
		/// Initialises a hook that runs the given function when its chat closes.
		/// </summary>
		/// <param name="owner">The registry the hook belongs to.</param>
		/// <param name="function">The function to run.</param>
		/// <param name="context">The calling context the function was registered from.</param>
		public LuaCloseHook(LuaChatClosingHandler owner, LuaFunction function, LuaCallingContext context)
		{
			_owner = owner;
			Function = function;
			Context = context;
		}

		/// <summary>
		/// The function the hook runs.
		/// </summary>
		[LuaHidden]
		public LuaFunction Function { get; }

		/// <summary>
		/// The calling context the function was registered from.
		/// </summary>
		[LuaHidden]
		public LuaCallingContext Context { get; }

		/// <summary>
		/// Whether the hook still runs when the chat closes.
		/// </summary>
		public bool Registered => _owner.Contains(this);

		/// <summary>
		/// Removes the hook from the registry.
		/// </summary>
		/// <returns><see langword="true"/> when it was still registered.</returns>
		public bool Cancel() => _owner.Unregister(this);

		/// <summary>
		/// Wraps the hook into a <c>close_hook</c> userdata.
		/// </summary>
		[LuaHidden]
		public static LuaUserData Wrap(LuaCloseHook hook)
			=> new(hook, "chat_close_hook")
			{
				Metatable = UserDataMetatableGenerator.GetOrCreate(typeof(LuaCloseHook))
			};
	}
}
