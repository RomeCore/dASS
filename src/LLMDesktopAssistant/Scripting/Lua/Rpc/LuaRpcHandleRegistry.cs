using LLMDesktopAssistant.LLM.Services;
using Serilog;

namespace LLMDesktopAssistant.Scripting.Lua.Rpc
{
	/// <summary>
	/// Owns the RPC handles of one chat, so a script that forgets to close them does not leave child
	/// processes running after the chat is gone.
	/// </summary>
	/// <remarks>
	/// The registry lives outside the Lua state, so a handle survives script reloads; it is chat-scoped,
	/// so the handles die with the chat. Unlike a shared database connection, a transport has exactly one
	/// owner, so closing here is always forcing.
	/// </remarks>
	[ChatService]
	public sealed class LuaRpcHandleRegistry : IDisposable
	{
		private readonly Lock _lock = new();
		private readonly List<LuaRpcHandle> _handles = [];

		/// <summary>
		/// Adds a handle to the chat's set.
		/// </summary>
		/// <param name="handle">The handle to track.</param>
		public void Register(LuaRpcHandle handle)
		{
			lock (_lock)
				_handles.Add(handle);
		}

		/// <summary>
		/// Removes a handle that was closed by its own script.
		/// </summary>
		/// <param name="handle">The handle to forget.</param>
		public void Unregister(LuaRpcHandle handle)
		{
			lock (_lock)
				_handles.Remove(handle);
		}

		/// <inheritdoc/>
		public void Dispose()
		{
			LuaRpcHandle[] handles;

			lock (_lock)
			{
				handles = [.. _handles];
				_handles.Clear();
			}

			foreach (var handle in handles)
				_ = CloseSafelyAsync(handle);
		}

		private static async Task CloseSafelyAsync(LuaRpcHandle handle)
		{
			try
			{
				await handle.Close();
			}
			catch (Exception ex)
			{
				Log.Warning(ex, "Failed to close a Lua RPC handle while disposing the chat.");
			}
		}
	}
}
