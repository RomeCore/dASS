using System.Text;
using System.Text.Json.Nodes;
using AsyncLua;
using AsyncLua.Values;
using LLMDesktopAssistant.StructuredValues.Converters;
using LLMDesktopAssistant.Utils.Rpc;
using Serilog;

namespace LLMDesktopAssistant.Scripting.Lua.Rpc
{
	/// <summary>
	/// An RPC handle exposed to Lua as the <c>rpc_handle</c> userdata returned by <c>rpc.connect(...)</c>.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The handle owns its transport outright: nothing is shared between handles, so closing always ends
	/// the peer and there is nothing to force.
	/// </para>
	/// <para>
	/// Members are named for Lua rather than for C# (<c>invoke</c>, not <c>invoke_async</c>), and the ones
	/// that are plumbing rather than contract are hidden from the generated metatable.
	/// </para>
	/// </remarks>
	public sealed class LuaRpcHandle : IAsyncDisposable
	{
		private static int _nextId;

		private readonly IRpcTransport _transport;
		private readonly JsonRpcConnection _connection;
		private readonly LuaRpcHandleRegistry _registry;
		private readonly LuaRpcStandardError _standardError;
		private readonly LuaCallingContext _context;
		private readonly CancellationTokenSource _lifetime = new();
		private readonly TimeSpan? _invokeTimeout;
		private readonly Lazy<LuaUserData> _invoker;
		private readonly Lazy<LuaUserData> _notifier;

		private Task? _standardErrorPump;
		private int? _exitCode;
		private int _closed;

		/// <summary>
		/// Initializes a handle, starting the connection and draining the peer's standard error.
		/// </summary>
		/// <param name="transport">The connected transport this handle owns.</param>
		/// <param name="handler">The router for the messages the peer initiates.</param>
		/// <param name="registry">The chat-scoped registry that closes leftovers when the chat dies.</param>
		/// <param name="standardError">The sink for the peer's standard error.</param>
		/// <param name="framing">The framing to speak.</param>
		/// <param name="invokeTimeout">The default timeout of a request, or <see langword="null"/> for none.</param>
		/// <param name="context">The connect call's context, used to call into Lua from the drain task.</param>
		public LuaRpcHandle(
			IRpcTransport transport,
			IJsonRpcMessageHandler handler,
			LuaRpcHandleRegistry registry,
			LuaRpcStandardError standardError,
			RpcFraming framing,
			TimeSpan? invokeTimeout,
			LuaCallingContext context)
		{
			_transport = transport;
			_registry = registry;
			_standardError = standardError;
			_invokeTimeout = invokeTimeout;
			_context = context;
			_invoker = new Lazy<LuaUserData>(() => LuaRpcInvoker.Wrap(new LuaRpcInvoker(this)));
			_notifier = new Lazy<LuaUserData>(() => LuaRpcInvoker.Wrap(new LuaRpcInvoker(this, isNotification: true)));

			_connection = new JsonRpcConnection(transport.Input, transport.Output, handler, framing);
			_connection.Start();

			_standardErrorPump = Task.Run(PumpStandardErrorAsync);
		}

		/// <summary>
		/// Wraps the handle into an <c>rpc_handle</c> userdata.
		/// </summary>
		/// <param name="handle">The handle to wrap.</param>
		[LuaHidden]
		public static LuaUserData Wrap(LuaRpcHandle handle)
			=> new(handle, "rpc_handle")
			{
				Metatable = UserDataMetatableGenerator.GetOrCreate(typeof(LuaRpcHandle))
			};

		/// <summary>
		/// Gets the process-wide identity of this connection.
		/// </summary>
		/// <remarks>
		/// The counter belongs to the application, not to the chat, so an id is unique for the lifetime of
		/// the process. It exists because a handler is handed a fresh wrapper of the connection: compare
		/// handles by id, not by identity.
		/// </remarks>
		public int Id { get; } = Interlocked.Increment(ref _nextId);

		/// <summary>
		/// Gets a value indicating whether the connection can still be used.
		/// </summary>
		public bool IsAlive => _closed == 0 && _connection.IsAlive;

		/// <summary>
		/// Gets the peer's process exit code, or <see langword="null"/> while it runs.
		/// </summary>
		public int? ExitCode => _exitCode ?? _transport.ExitCode;

		/// <summary>
		/// Gets the peer's process id, or <see langword="null"/> for a transport without a process.
		/// </summary>
		public int? Pid => _transport.ProcessId;

		/// <summary>
		/// Gets the framing actually in use, <c>content_length</c> or <c>lines</c>.
		/// </summary>
		public string Framing => _connection.Framing == RpcFraming.ContentLength ? "content_length" : "lines";

		/// <summary>
		/// Gets the userdata that sends a request by method name: <c>h.invoke:method(x)</c>.
		/// </summary>
		public LuaUserData Invoke => _invoker.Value;

		/// <summary>
		/// Gets the userdata that sends a notification by method name: <c>h.notify:method(x)</c>.
		/// </summary>
		public LuaUserData Notify => _notifier.Value;

		/// <summary>
		/// Sends a request and waits for its response.
		/// </summary>
		/// <param name="method">The method name.</param>
		/// <param name="parameters">The parameters, or <see langword="null"/> for none.</param>
		/// <param name="cancellationToken">The token of the running script.</param>
		/// <returns>The decoded result, or <see langword="null"/> for a JSON <c>null</c>.</returns>
		[LuaHidden]
		public async Task<JsonNode?> InvokeAsync(string method, JsonNode? parameters, CancellationToken cancellationToken = default)
		{
			ThrowIfClosed("invoke");

			return await _connection
				.InvokeAsync(method, parameters, _invokeTimeout, cancellationToken)
				.ConfigureAwait(false);
		}

		/// <summary>
		/// Sends a notification: no identifier, no response.
		/// </summary>
		/// <param name="method">The method name.</param>
		/// <param name="parameters">The parameters, or <see langword="null"/> for none.</param>
		/// <param name="cancellationToken">The token of the running script.</param>
		[LuaHidden]
		public async Task NotifyAsync(
			string method,
			JsonNode? parameters,
			CancellationToken cancellationToken = default)
		{
			ThrowIfClosed("notify");

			await _connection.NotifyAsync(method, parameters, cancellationToken).ConfigureAwait(false);
		}

		/// <summary>
		/// Reads the peer's buffered standard error.
		/// </summary>
		/// <returns>The buffered text and the number of dropped lines, or <see langword="nil"/> plus the
		/// reason when the lines were redirected to a file, a table or a callback.</returns>
		public LuaTuple Stderr() => _standardError.Read();

		/// <summary>
		/// Closes the handle and ends the peer.
		/// </summary>
		/// <param name="cancellationToken">The token of the running script.</param>
		/// <returns><see langword="true"/> when this call closed the handle, <see langword="false"/> when it
		/// was already closed.</returns>
		public async Task<bool> Close(CancellationToken cancellationToken = default)
		{
			if (Interlocked.Exchange(ref _closed, 1) != 0)
				return false;

			_registry.Unregister(this);
			_lifetime.Cancel();

			try
			{
				await _connection.DisposeAsync().ConfigureAwait(false);
			}
			catch (Exception ex)
			{
				Log.Debug(ex, "The RPC connection did not close cleanly.");
			}

			_transport.Kill();

			try
			{
				_exitCode = await _transport.WaitForExitAsync(CancellationToken.None)
					.WaitAsync(TimeSpan.FromSeconds(5), CancellationToken.None)
					.ConfigureAwait(false);
			}
			catch (Exception ex)
			{
				Log.Debug(ex, "The RPC peer did not report its exit code.");
			}

			try
			{
				await _transport.DisposeAsync().ConfigureAwait(false);
			}
			catch (Exception ex)
			{
				Log.Debug(ex, "The RPC transport did not dispose cleanly.");
			}

			_standardError.Dispose();
			return true;
		}

		/// <inheritdoc/>
		public async ValueTask DisposeAsync()
		{
			try
			{
				await Close().ConfigureAwait(false);
			}
			catch (Exception ex)
			{
				Log.Debug(ex, "Closing an RPC handle failed while disposing it.");
			}
		}

		/// <summary>
		/// Drains the peer's standard error into the configured sink.
		/// </summary>
		/// <remarks>
		/// This runs on its own task on purpose: a peer that fills its diagnostic pipe stops answering, so
		/// the draining must never wait for the protocol and the protocol must never wait for the draining.
		/// </remarks>
		private async Task PumpStandardErrorAsync()
		{
			var stream = _transport.StandardError;
			if (stream is null)
				return;

			var context = _context.WithCancellation(_lifetime.Token);

			try
			{
				using var reader = new StreamReader(stream, Encoding.UTF8);

				while (await reader.ReadLineAsync(_lifetime.Token).ConfigureAwait(false) is { } line)
					await _standardError.ReportAsync(line, context, _lifetime.Token).ConfigureAwait(false);
			}
			catch (OperationCanceledException)
			{
			}
			catch (Exception ex)
			{
				Log.Debug(ex, "The RPC peer's standard error ended.");
			}
		}

		private void ThrowIfClosed(string method)
		{
			if (_closed != 0)
				throw new LuaRuntimeException($"rpc handle:{method}(): the handle is closed.");
		}
	}
}
