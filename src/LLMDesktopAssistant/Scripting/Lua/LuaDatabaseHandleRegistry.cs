using LLMDesktopAssistant.LLM.Services;
using Serilog;

namespace LLMDesktopAssistant.Scripting.Lua
{
	/// <summary>
	/// Owns the database handles of one chat, so a script that forgets to close them does not leave
	/// connections open after the chat is gone.
	/// </summary>
	/// <remarks>
	/// The registry lives outside the Lua state, so handles survive script reloads; it is chat-scoped,
	/// so the handles die with the chat. Closing here is non-forcing: a connection another chat still
	/// uses stays open, and only the last user closes it.
	/// </remarks>
	[ChatService]
	public sealed class LuaDatabaseHandleRegistry : IDisposable
	{
		private readonly Lock _lock = new();
		private readonly List<LuaDatabaseHandle> _handles = [];

		/// <summary>
		/// Adds a handle to the chat's set.
		/// </summary>
		/// <param name="handle">The handle to track.</param>
		public void Register(LuaDatabaseHandle handle)
		{
			lock (_lock)
				_handles.Add(handle);
		}

		/// <summary>
		/// Removes a handle that was closed by its own script.
		/// </summary>
		/// <param name="handle">The handle to forget.</param>
		public void Unregister(LuaDatabaseHandle handle)
		{
			lock (_lock)
				_handles.Remove(handle);
		}

		/// <inheritdoc/>
		public void Dispose()
		{
			LuaDatabaseHandle[] handles;

			lock (_lock)
			{
				handles = [.. _handles];
				_handles.Clear();
			}

			foreach (var handle in handles)
				_ = CloseSafelyAsync(handle);
		}

		private static async Task CloseSafelyAsync(LuaDatabaseHandle handle)
		{
			try
			{
				await handle.Close();
			}
			catch (Exception ex)
			{
				Log.Warning(ex, "Failed to close a Lua database handle while disposing the chat.");
			}
		}
	}
}
