using AsyncLua;
using AsyncLua.Values;
using LLMDesktopAssistant.LLM.Services;
using LLMDesktopAssistant.Services.Instances;
using Serilog;

namespace LLMDesktopAssistant.Scripting.Lua
{
	/// <summary>
	/// Runs the Lua functions registered with <c>dass.chat.on_close</c> when the chat closes.
	/// </summary>
	/// <remarks>
	/// Hooks run LIFO - the last one registered runs first - and a failure is logged without
	/// stopping the rest. The handler is called before the chat services dispose, so a hook can
	/// still use them.
	/// </remarks>
	[ChatService]
	[ChatService(typeof(IChatClosingHandler))]
	public class LuaChatClosingHandler : IChatClosingHandler
	{
		private readonly List<LuaCloseHook> _hooks = [];
		private readonly Lock _lock = new();
		private bool _closed;

		/// <summary>
		/// Registers a function to run when the chat closes.
		/// </summary>
		/// <param name="function">The function to run. It is called with no arguments.</param>
		/// <param name="context">The calling context the function was registered from.</param>
		/// <returns>The registration handle.</returns>
		public LuaCloseHook Register(LuaFunction function, LuaCallingContext context)
		{
			var hook = new LuaCloseHook(this, function, context);

			lock (_lock)
			{
				if (_closed)
				{
					Log.Warning("dass.chat.on_close(): the chat is already closing, the hook will never run.");
					return hook;
				}

				_hooks.Add(hook);
			}

			return hook;
		}

		/// <summary>
		/// Checks whether the hook still runs when the chat closes.
		/// </summary>
		internal bool Contains(LuaCloseHook hook)
		{
			lock (_lock)
				return _hooks.Contains(hook);
		}

		/// <summary>
		/// Removes the hook from the registry.
		/// </summary>
		/// <returns><see langword="true"/> when it was still registered.</returns>
		internal bool Unregister(LuaCloseHook hook)
		{
			lock (_lock)
				return _hooks.Remove(hook);
		}

		/// <inheritdoc/>
		public void OnChatClosing()
		{
			LuaCloseHook[] hooks;

			lock (_lock)
			{
				if (_closed)
					return;

				_closed = true;
				hooks = [.. _hooks];
				_hooks.Clear();
			}

			// LIFO: the most recent registration runs first.
			for (int index = hooks.Length - 1; index >= 0; index--)
			{
				var hook = hooks[index];
				try
				{
					// The registering call is long over: its token would abort the hook on the first
					// instruction, so the hook runs detached from it.
					hook.Function.Invoke(hook.Context.WithoutCancellation());
				}
				catch (Exception ex)
				{
					Log.Error(ex, "Error running a Lua chat close hook: {Error}", ex.Message);
				}
			}
		}
	}
}
