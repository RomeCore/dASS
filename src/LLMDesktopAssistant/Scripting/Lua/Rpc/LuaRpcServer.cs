using AsyncLua;
using AsyncLua.Values;
using LLMDesktopAssistant.LLM.Services;
using LLMDesktopAssistant.Utils.Rpc;
using Serilog;

namespace LLMDesktopAssistant.Scripting.Lua.Rpc
{
	/// <summary>
	/// A listening endpoint exposed to Lua as the <c>rpc_server</c> userdata returned by <c>rpc.listen(...)</c>.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The listener owns the socket and hands every accepted connection to an ordinary
	/// <see cref="LuaRpcHandle"/>, so a server peer and a client peer are the same object once accepted:
	/// the script can answer it, invoke it and notify it either way.
	/// </para>
	/// <para>
	/// The routing table is shared with every accepted connection and read on each message, and handlers
	/// of one listener are serialized through a gate, because the chat has a single Lua state and two
	/// peers must not interleave in it.
	/// </para>
	/// <para>
	/// Only <see cref="Accept"/> is asynchronous. Binding and stopping a socket are not, so
	/// <see cref="Close"/> stays callable from a synchronous chat-close or script-cleanup block.
	/// </para>
	/// </remarks>
	public sealed class LuaRpcServer
	{
		private readonly IRpcListener _listener;
		private readonly LuaTable _routing;
		private readonly LuaRpcHandleRegistry _handles;
		private readonly LuaRpcServerRegistry _servers;
		private readonly RpcFraming _framing;
		private readonly TimeSpan? _invokeTimeout;
		private readonly LuaCallingContext _context;
		private readonly IWorkingDirectoryAccessService _workingDirectory;
		private readonly SemaphoreSlim _gate = new(1, 1);
		private readonly List<LuaRpcHandle> _connections = [];
		private readonly Lock _lock = new();

		private int _closed;

		/// <summary>
		/// Initializes a server over a bound listener.
		/// </summary>
		/// <param name="listener">The bound endpoint this server owns.</param>
		/// <param name="routing">The routing table shared by every accepted connection.</param>
		/// <param name="handles">The chat-scoped registry that closes accepted connections when the chat dies.</param>
		/// <param name="servers">The chat-scoped registry that closes the listener when the chat dies.</param>
		/// <param name="framing">The framing every accepted connection speaks.</param>
		/// <param name="invokeTimeout">The default timeout of a request sent through an accepted handle.</param>
		/// <param name="context">The listen call's context, used to call into Lua when a peer sends a message.</param>
		/// <param name="workingDirectory">Resolves a relative stderr path, which a socket peer never uses.</param>
		public LuaRpcServer(
			IRpcListener listener,
			LuaTable routing,
			LuaRpcHandleRegistry handles,
			LuaRpcServerRegistry servers,
			RpcFraming framing,
			TimeSpan? invokeTimeout,
			LuaCallingContext context,
			IWorkingDirectoryAccessService workingDirectory)
		{
			_listener = listener;
			_routing = routing;
			_handles = handles;
			_servers = servers;
			_framing = framing;
			_invokeTimeout = invokeTimeout;
			_context = context;
			_workingDirectory = workingDirectory;
		}

		/// <summary>
		/// Wraps the server into an <c>rpc_server</c> userdata.
		/// </summary>
		/// <param name="server">The server to wrap.</param>
		[LuaHidden]
		public static LuaUserData Wrap(LuaRpcServer server)
			=> new(server, "rpc_server")
			{
				Metatable = UserDataMetatableGenerator.GetOrCreate(typeof(LuaRpcServer))
			};

		/// <summary>
		/// Gets the address the listener is bound to.
		/// </summary>
		public string Address => _listener.Address;

		/// <summary>
		/// Gets the bound port, which is the real one when the script asked for port <c>0</c>.
		/// </summary>
		public int Port => _listener.Port;

		/// <summary>
		/// Gets a value indicating whether the listener still accepts connections.
		/// </summary>
		public bool IsAlive => _closed == 0 && _listener.IsListening;

		/// <summary>
		/// Gets the number of accepted connections that are still open.
		/// </summary>
		public int Connections
		{
			get
			{
				if (_closed != 0)
					return 0;

				lock (_lock)
					return _connections.Count(handle => handle.IsAlive);
			}
		}

		/// <summary>
		/// Waits for the next inbound connection and returns it as an ordinary <c>rpc_handle</c>.
		/// </summary>
		/// <param name="timeoutMs">How long to wait, in milliseconds, or <c>0</c> to wait for the listener.</param>
		/// <param name="cancellationToken">The token of the running script.</param>
		/// <returns>The accepted handle, or <see langword="nil"/> plus the reason when the wait ended
		/// without a peer.</returns>
		/// <remarks>
		/// This is the only member that waits, because a peer is the only thing the server cannot produce
		/// synchronously.
		/// </remarks>
		public async Task<LuaTuple> Accept(int timeoutMs = 0, CancellationToken cancellationToken = default)
		{
			ThrowIfClosed("accept");

			if (timeoutMs < 0)
				throw new LuaRuntimeException("rpc_server:accept(timeout_ms): the timeout cannot be negative.");

			await PruneClosedConnectionsAsync().ConfigureAwait(false);

			using var timeout = timeoutMs > 0
				? CancellationTokenSource.CreateLinkedTokenSource(cancellationToken)
				: null;

			timeout?.CancelAfter(timeoutMs);

			IRpcTransport transport;

			try
			{
				transport = await _listener
					.AcceptAsync(timeout?.Token ?? cancellationToken)
					.ConfigureAwait(false);
			}
			catch (OperationCanceledException) when (timeout is not null && !cancellationToken.IsCancellationRequested)
			{
				return new LuaTuple(LuaNil.Instance, new LuaString($"accept timed out after {timeoutMs} ms"));
			}
			catch (Exception ex) when (!IsAlive)
			{
				return new LuaTuple(LuaNil.Instance, new LuaString($"the listener is closed ({ex.Message})"));
			}

			var router = new LuaRpcRouter(_routing, _context, _gate);
			var peer = new LuaRpcHandle(
				transport,
				router,
				_handles,
				LuaRpcStandardError.Create(null, LuaRpcStandardError.DefaultLimit, _workingDirectory),
				_framing,
				_invokeTimeout,
				_context);
			router.Connection = peer;

			_handles.Register(peer);

			lock (_lock)
				_connections.Add(peer);

			return new LuaTuple(LuaRpcHandle.Wrap(peer));
		}

		/// <summary>
		/// Stops listening and releases the port, then closes every connection this listener accepted.
		/// </summary>
		/// <returns><see langword="true"/> when this call closed the listener, <see langword="false"/> when
		/// it was already closed.</returns>
		/// <remarks>
		/// The port is free before this method returns, so a script can rebind at once. The accepted peers
		/// are closed without waiting: a handle's close drains its write buffer and waits for the peer to
		/// end, and this call has to stay usable from a synchronous cleanup block.
		/// </remarks>
		public bool Close()
		{
			if (Interlocked.Exchange(ref _closed, 1) != 0)
				return false;

			_servers.Unregister(this);

			// Stopping first is what wakes a pending accept and what frees the port.
			_listener.Stop();

			LuaRpcHandle[] connections;

			lock (_lock)
			{
				connections = [.. _connections];
				_connections.Clear();
			}

			foreach (var connection in connections)
				_ = CloseConnectionAsync(connection);

			return true;
		}

		/// <summary>
		/// Closes the connections whose peer disconnected, so a long-lived listener does not accumulate
		/// dead sockets and registry entries.
		/// </summary>
		private async Task PruneClosedConnectionsAsync()
		{
			LuaRpcHandle[] dead;

			lock (_lock)
			{
				dead = [.. _connections.Where(handle => !handle.IsAlive)];
				_connections.RemoveAll(handle => !handle.IsAlive);
			}

			foreach (var handle in dead)
			{
				try
				{
					await handle.Close().ConfigureAwait(false);
				}
				catch (Exception ex)
				{
					Log.Debug(ex, "Closing a disconnected RPC peer failed.");
				}
			}
		}

		private static async Task CloseConnectionAsync(LuaRpcHandle handle)
		{
			try
			{
				await handle.Close().ConfigureAwait(false);
			}
			catch (Exception ex)
			{
				Log.Debug(ex, "Closing an RPC connection of a closed listener failed.");
			}
		}

		private void ThrowIfClosed(string method)
		{
			if (_closed != 0)
				throw new LuaRuntimeException($"rpc_server:{method}(): the listener is closed.");
		}
	}
}
