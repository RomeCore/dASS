using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json.Nodes;
using System.Threading.Channels;
using Serilog;

namespace LLMDesktopAssistant.Utils.Rpc
{
	/// <summary>
	/// A JSON-RPC 2.0 connection over a duplex stream: it sends requests and notifications, and answers
	/// the ones the peer sends.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Three paths, and the separation between them is what keeps the connection from deadlocking. The
	/// read loop only parses frames and resolves responses through a pending map, never entering the
	/// handler. Requests and notifications go to a queue that one sequential dispatcher drains. Because
	/// responses bypass the dispatcher, a handler that awaits a nested request still gets its answer
	/// while it is running.
	/// </para>
	/// <para>
	/// JSON-RPC is symmetric: either side may send a request at any moment, so a connection that reads
	/// only while it waits for its own answer hangs on the first message the peer sends unprompted.
	/// </para>
	/// </remarks>
	public sealed class JsonRpcConnection : IJsonRpcPeer, IAsyncDisposable
	{
		private readonly record struct InboundMessage(string Method, JsonNode? Parameters, JsonNode? Id);

		private readonly JsonRpcFrameReader _reader;
		private readonly JsonRpcFrameWriter _writer;
		private readonly IJsonRpcMessageHandler _handler;
		private readonly Channel<InboundMessage> _queue = Channel.CreateUnbounded<InboundMessage>(
			new UnboundedChannelOptions { SingleReader = true, SingleWriter = true });

		private readonly ConcurrentDictionary<string, TaskCompletionSource<JsonNode?>> _pending = new(StringComparer.Ordinal);
		private readonly CancellationTokenSource _lifetime = new();

		private Task? _readLoop;
		private Task? _dispatchLoop;
		private Exception? _failure;
		private long _nextId;
		private int _started;
		private int _disposed;
		private volatile bool _alive;

		/// <summary>
		/// Initializes a connection over the given streams.
		/// </summary>
		/// <param name="input">The stream the peer writes to.</param>
		/// <param name="output">The stream the peer reads from.</param>
		/// <param name="handler">The handler of the messages the peer initiates.</param>
		/// <param name="framing">The framing to use. <see cref="RpcFraming.Auto"/> writes
		/// <c>Content-Length</c> until the reader recognises the peer's dialect.</param>
		public JsonRpcConnection(
			Stream input,
			Stream output,
			IJsonRpcMessageHandler handler,
			RpcFraming framing = RpcFraming.ContentLength)
		{
			_reader = new JsonRpcFrameReader(input, framing);
			_writer = new JsonRpcFrameWriter(output, framing);
			_handler = handler;
		}

		/// <summary>
		/// Gets a value indicating whether the connection can still be used.
		/// </summary>
		public bool IsAlive => _alive;

		/// <summary>
		/// Gets the framing actually in use, after auto-detection has run.
		/// </summary>
		public RpcFraming Framing => _writer.Framing;

		/// <summary>
		/// Gets the number of requests waiting for a response.
		/// </summary>
		public int PendingCount => _pending.Count;

		/// <summary>
		/// Starts reading and dispatching. Must be called once, before anything is sent.
		/// </summary>
		public void Start()
		{
			if (Interlocked.Exchange(ref _started, 1) != 0)
				throw new InvalidOperationException("The connection is already started.");

			_alive = true;
			_readLoop = Task.Run(ReadLoopAsync);
			_dispatchLoop = Task.Run(DispatchLoopAsync);
		}

		/// <summary>
		/// Sends a request and waits for its response.
		/// </summary>
		/// <param name="method">The method name.</param>
		/// <param name="parameters">The parameters to send, or <see langword="null"/> for none. The node
		/// must not already belong to another JSON tree.</param>
		/// <param name="timeout">How long to wait, or <see langword="null"/> to wait for the connection.</param>
		/// <param name="cancellationToken">The token to cancel the call; the request is abandoned, not
		/// cancelled on the peer's side.</param>
		/// <returns>The decoded result, or <see langword="null"/> when the peer sent a JSON <c>null</c>.</returns>
		/// <exception cref="JsonRpcException">The peer answered with an error, or the connection is dead.</exception>
		/// <exception cref="TimeoutException">The peer did not answer within <paramref name="timeout"/>.</exception>
		public async Task<JsonNode?> InvokeAsync(
			string method,
			JsonNode? parameters = null,
			TimeSpan? timeout = null,
			CancellationToken cancellationToken = default)
		{
			ThrowIfNotAlive();

			var id = Interlocked.Increment(ref _nextId);
			var key = id.ToString(CultureInfo.InvariantCulture);
			var completion = new TaskCompletionSource<JsonNode?>(TaskCreationOptions.RunContinuationsAsynchronously);

			_pending[key] = completion;

			try
			{
				await WriteRequestAsync(id, method, parameters, cancellationToken).ConfigureAwait(false);

				using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _lifetime.Token);
				if (timeout is { } limit)
					linked.CancelAfter(limit);

				try
				{
					return await completion.Task.WaitAsync(linked.Token).ConfigureAwait(false);
				}
				catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
				{
					ThrowIfNotAlive();

					throw new TimeoutException(
						$"The peer did not answer '{method}' within {timeout?.ToString() ?? "the connection lifetime"}.");
				}
			}
			finally
			{
				_pending.TryRemove(key, out _);
			}
		}

		/// <summary>
		/// Sends a notification: no identifier, no response, no exception beyond a write failure.
		/// </summary>
		/// <param name="method">The method name.</param>
		/// <param name="parameters">The parameters to send, or <see langword="null"/> for none.</param>
		/// <param name="cancellationToken">The token to cancel the write.</param>
		public async Task NotifyAsync(string method, JsonNode? parameters = null, CancellationToken cancellationToken = default)
		{
			ThrowIfNotAlive();

			var message = new JsonObject
			{
				["jsonrpc"] = "2.0",
				["method"] = method
			};

			if (parameters is not null)
				message["params"] = parameters;

			await WriteAsync(message, cancellationToken).ConfigureAwait(false);
		}

		/// <inheritdoc/>
		public async ValueTask DisposeAsync()
		{
			if (Interlocked.Exchange(ref _disposed, 1) != 0)
				return;

			_alive = false;
			_lifetime.Cancel();
			_queue.Writer.TryComplete();
			FailPending("The connection was closed.");

			Task[] loops = [.. new[] { _readLoop, _dispatchLoop }.OfType<Task>()];
			if (loops.Length > 0)
			{
				try
				{
					await Task.WhenAll(loops).WaitAsync(TimeSpan.FromSeconds(5)).ConfigureAwait(false);
				}
				catch (Exception ex)
				{
					Log.Debug(ex, "A JSON-RPC loop did not stop cleanly.");
				}
			}

			_lifetime.Dispose();
		}

		private async Task ReadLoopAsync()
		{
			try
			{
				while (!_lifetime.IsCancellationRequested)
				{
					var frame = await _reader.ReadFrameAsync(_lifetime.Token).ConfigureAwait(false);
					if (frame is null)
						break;

					// The reader owns the detection; the writer follows it from the first frame on.
					if (_reader.Framing != RpcFraming.Auto && _writer.Framing != _reader.Framing)
						_writer.Framing = _reader.Framing;

					Accept(frame);
				}
			}
			catch (OperationCanceledException)
			{
			}
			catch (Exception ex)
			{
				_failure = ex;
				Log.Debug(ex, "The JSON-RPC read loop ended.");
			}
			finally
			{
				_alive = false;
				FailPending(_failure is null
					? "The peer closed the connection."
					: $"The connection was lost: {_failure.Message}");

				_queue.Writer.TryComplete();
			}
		}

		private async Task DispatchLoopAsync()
		{
			try
			{
				await foreach (var message in _queue.Reader.ReadAllAsync(_lifetime.Token).ConfigureAwait(false))
				{
					if (message.Id is { } id)
						await HandleRequestAsync(message, id).ConfigureAwait(false);
					else
						await HandleNotificationAsync(message).ConfigureAwait(false);
				}
			}
			catch (OperationCanceledException)
			{
			}
			catch (Exception ex)
			{
				Log.Error(ex, "The JSON-RPC dispatch loop ended.");
			}
		}

		/// <summary>
		/// Sorts one parsed frame into a response, a request or a notification.
		/// </summary>
		/// <param name="frame">The frame payload.</param>
		private void Accept(string frame)
		{
			JsonNode? node;

			try
			{
				node = JsonNode.Parse(frame);
			}
			catch (Exception ex)
			{
				// Nothing identifies the sender of an unparsable frame, so there is nobody to answer.
				Log.Warning(ex, "The JSON-RPC peer sent unparsable JSON; the frame is dropped.");
				return;
			}

			if (node is not JsonObject message)
			{
				Log.Warning("The JSON-RPC peer sent a frame that is not a JSON object; it is dropped.");
				return;
			}

			if (message["method"] is JsonValue methodValue && methodValue.TryGetValue<string>(out var method))
			{
				_queue.Writer.TryWrite(new InboundMessage(method, message["params"], message["id"]));
				return;
			}

			if (message["id"] is { } responseId)
			{
				CompletePending(responseId, message);
				return;
			}

			Log.Warning("The JSON-RPC peer sent a message with neither a method nor an id; it is dropped.");
		}

		/// <summary>
		/// Resolves the request the given response answers.
		/// </summary>
		/// <param name="id">The response identifier.</param>
		/// <param name="message">The whole response object.</param>
		private void CompletePending(JsonNode id, JsonObject message)
		{
			var key = IdKey(id);
			if (!_pending.TryRemove(key, out var completion))
			{
				Log.Debug("A JSON-RPC response for id {Id} has no waiting caller; it is dropped.", key);
				return;
			}

			if (message["error"] is JsonObject error)
			{
				var code = error["code"] is JsonValue codeValue && codeValue.TryGetValue<int>(out var parsed)
					? parsed
					: RpcErrorCodes.InternalError;

				var text = error["message"] is JsonValue messageValue && messageValue.TryGetValue<string>(out var messageText)
					? messageText
					: "The peer sent an error without a message.";

				completion.TrySetException(new JsonRpcException(code, text, error["data"]));
				return;
			}

			completion.TrySetResult(message["result"]);
		}

		private async Task HandleRequestAsync(InboundMessage message, JsonNode id)
		{
			try
			{
				var result = await _handler
					.HandleRequestAsync(this, message.Method, message.Parameters, _lifetime.Token)
					.ConfigureAwait(false);

				await WriteResponseAsync(id, result).ConfigureAwait(false);
			}
			catch (JsonRpcException ex)
			{
				await WriteErrorAsync(id, ex.Code, ex.Message, ex.Data).ConfigureAwait(false);
			}
			catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
			{
			}
			catch (OperationCanceledException)
			{
				await WriteErrorAsync(id, RpcErrorCodes.RequestCancelled, "The request was cancelled.", null)
					.ConfigureAwait(false);
			}
			catch (Exception ex)
			{
				Log.Warning(ex, "A JSON-RPC handler failed for method {Method}.", message.Method);
				await WriteErrorAsync(id, RpcErrorCodes.InternalError, ex.Message, null).ConfigureAwait(false);
			}
		}

		private async Task HandleNotificationAsync(InboundMessage message)
		{
			try
			{
				await _handler
					.HandleNotificationAsync(this, message.Method, message.Parameters, _lifetime.Token)
					.ConfigureAwait(false);
			}
			catch (OperationCanceledException) when (_lifetime.IsCancellationRequested)
			{
			}
			catch (JsonRpcException ex)
			{
				// A notification has no response channel; the rejection can only be logged.
				Log.Warning("A JSON-RPC notification '{Method}' was rejected: {Code} {Message}",
					message.Method, ex.Code, ex.Message);
			}
			catch (Exception ex)
			{
				Log.Warning(ex, "A JSON-RPC notification handler failed for method {Method}.", message.Method);
			}
		}

		private Task WriteRequestAsync(long id, string method, JsonNode? parameters, CancellationToken cancellationToken)
		{
			var message = new JsonObject
			{
				["jsonrpc"] = "2.0",
				["id"] = id,
				["method"] = method
			};

			if (parameters is not null)
				message["params"] = parameters;

			return WriteAsync(message, cancellationToken);
		}

		private Task WriteResponseAsync(JsonNode id, JsonNode? result)
		{
			var message = new JsonObject
			{
				["jsonrpc"] = "2.0",
				// The identifier belongs to the request's tree; a node cannot have two parents.
				["id"] = id.DeepClone(),
				["result"] = result
			};

			return WriteAsync(message, _lifetime.Token);
		}

		private async Task WriteErrorAsync(JsonNode id, int code, string message, JsonNode? data)
		{
			try
			{
				var error = new JsonObject
				{
					["code"] = code,
					["message"] = message
				};

				if (data is not null)
					error["data"] = data.DeepClone();

				var response = new JsonObject
				{
					["jsonrpc"] = "2.0",
					["id"] = id.DeepClone(),
					["error"] = error
				};

				await WriteAsync(response, _lifetime.Token).ConfigureAwait(false);
			}
			catch (Exception ex)
			{
				// The handler already failed; failing to report it must not take the dispatcher down.
				Log.Debug(ex, "An error response could not be written.");
			}
		}

		private async Task WriteAsync(JsonObject message, CancellationToken cancellationToken)
		{
			await _writer.WriteAsync(message.ToJsonString(), cancellationToken).ConfigureAwait(false);
		}

		private void FailPending(string reason)
		{
			foreach (var key in _pending.Keys.ToArray())
			{
				if (_pending.TryRemove(key, out var completion))
					completion.TrySetException(new JsonRpcException(RpcErrorCodes.ConnectionClosed, reason));
			}
		}

		private void ThrowIfNotAlive()
		{
			if (_alive)
				return;

			throw new JsonRpcException(
				RpcErrorCodes.ConnectionClosed,
				_failure is null
					? "The connection is closed."
					: $"The connection is closed: {_failure.Message}");
		}

		/// <summary>
		/// Reduces an identifier node to the dictionary key used to match a response.
		/// </summary>
		/// <param name="id">The identifier node.</param>
		/// <returns>The key, stable across number and string identifiers alike.</returns>
		private static string IdKey(JsonNode id) => id.ToJsonString();
	}
}
