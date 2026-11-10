using LLMDesktopAssistant.LLM.Services;
using Serilog;

namespace LLMDesktopAssistant.Scripting.Lua.Rpc
{
	/// <summary>
	/// Owns the RPC listeners of one chat, so a script that forgets to close one does not leave a bound
	/// port behind after the chat is gone.
	/// </summary>
	/// <remarks>
	/// The registry lives outside the Lua state, so a listener survives script reloads; it is chat-scoped,
	/// so the port dies with the chat. Closing a server also closes the connections it accepted.
	/// </remarks>
	[ChatService]
	public sealed class LuaRpcServerRegistry : IDisposable
	{
		private readonly Lock _lock = new();
		private readonly List<LuaRpcServer> _servers = [];

		/// <summary>
		/// Adds a listener to the chat's set.
		/// </summary>
		/// <param name="server">The server to track.</param>
		public void Register(LuaRpcServer server)
		{
			lock (_lock)
				_servers.Add(server);
		}

		/// <summary>
		/// Removes a listener that was closed by its own script.
		/// </summary>
		/// <param name="server">The server to forget.</param>
		public void Unregister(LuaRpcServer server)
		{
			lock (_lock)
				_servers.Remove(server);
		}

		/// <inheritdoc/>
		/// <remarks>
		/// Closing a listener is synchronous, which is what lets the chat dispose it here without an
		/// asynchronous disposal path.
		/// </remarks>
		public void Dispose()
		{
			LuaRpcServer[] servers;

			lock (_lock)
			{
				servers = [.. _servers];
				_servers.Clear();
			}

			foreach (var server in servers)
			{
				try
				{
					server.Close();
				}
				catch (Exception ex)
				{
					Log.Warning(ex, "Failed to close a Lua RPC listener while disposing the chat.");
				}
			}
		}
	}
}
