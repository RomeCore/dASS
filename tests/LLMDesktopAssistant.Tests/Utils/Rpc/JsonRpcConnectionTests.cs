using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Channels;
using LLMDesktopAssistant.Utils.Rpc;

namespace LLMDesktopAssistant.Tests.Utils
{
	public class JsonRpcConnectionTests
	{
		[Fact]
		public async Task ResolvesAResponseByItsId()
		{
			using var feed = new FeedStream();
			var output = new CapturingStream();
			await using var connection = new JsonRpcConnection(feed, output, new Handler());
			connection.Start();

			var call = connection.InvokeAsync("ping");
			var response = "{\"jsonrpc\":\"2.0\",\"id\":1,\"result\":1}";
			feed.Write($"Content-Length: {Encoding.UTF8.GetByteCount(response)}\r\n\r\n{response}");

			var result = await call.WaitAsync(TimeSpan.FromSeconds(5));
			Assert.NotNull(result);
			Assert.Equal("1", result!.ToJsonString());
		}

		[Fact]
		public async Task ResolvesAnErrorAsAnException()
		{
			using var feed = new FeedStream();
			await using var connection = new JsonRpcConnection(feed, new CapturingStream(), new Handler());
			connection.Start();

			var call = connection.InvokeAsync("boom");
			var error = "{\"jsonrpc\":\"2.0\",\"id\":1,\"error\":{\"code\":-32601,\"message\":\"nope\"}}";
			feed.Write($"Content-Length: {Encoding.UTF8.GetByteCount(error)}\r\n\r\n{error}");

			var exception = await Assert.ThrowsAsync<JsonRpcException>(async () => await call.WaitAsync(TimeSpan.FromSeconds(5)));
			Assert.Equal(RpcErrorCodes.MethodNotFound, exception.Code);
			Assert.Equal("nope", exception.Message);
		}

		[Fact]
		public async Task AnswersAnInboundRequest()
		{
			using var feed = new FeedStream();
			var output = new CapturingStream();

			var handler = new Handler(onRequest: (method, parameters) =>
				Task.FromResult<JsonNode?>(new JsonObject { ["echo"] = method }));

			await using var connection = new JsonRpcConnection(feed, output, handler);
			connection.Start();

			var request = "{\"jsonrpc\":\"2.0\",\"id\":\"srv-1\",\"method\":\"workspace/configuration\"}";
			feed.Write($"Content-Length: {Encoding.UTF8.GetByteCount(request)}\r\n\r\n{request}");

			var frame = await output.WaitForFrameAsync();
			var body = JsonNode.Parse(frame)!;
			Assert.Equal("srv-1", body["id"]!.ToJsonString().Trim('"'));
			Assert.Equal("workspace/configuration", body["result"]!["echo"]!.ToJsonString().Trim('"'));
		}

		[Fact]
		public async Task AnswersMethodNotFoundWhenNothingIsRouted()
		{
			using var feed = new FeedStream();
			var output = new CapturingStream();

			await using var connection = new JsonRpcConnection(feed, output, new Handler(), RpcFraming.Auto);
			connection.Start();

			feed.Write("{\"jsonrpc\":\"2.0\",\"id\":7,\"method\":\"nobody/home\"}\n");

			var frame = await output.WaitForFrameAsync();
			var body = JsonNode.Parse(frame)!;
			Assert.Equal(RpcErrorCodes.MethodNotFound, body["error"]!["code"]!.GetValue<int>());
		}

		[Fact]
		public async Task DispatchesNotifications()
		{
			using var feed = new FeedStream();
			var handler = new Handler();

			await using var connection = new JsonRpcConnection(feed, new CapturingStream(), handler, RpcFraming.Auto);
			connection.Start();

			feed.Write("{\"jsonrpc\":\"2.0\",\"method\":\"textDocument/publishDiagnostics\",\"params\":{}}\n");

			await handler.NotificationReceived.Task.WaitAsync(TimeSpan.FromSeconds(5));
			Assert.Equal("textDocument/publishDiagnostics", handler.NotificationMethod);
		}

		[Fact]
		public async Task FollowsTheDetectedLineFramingWhenAnswering()
		{
			using var feed = new FeedStream();
			var output = new CapturingStream();

			await using var connection = new JsonRpcConnection(
				feed,
				output,
				new Handler(onRequest: (_, _) => Task.FromResult<JsonNode?>(new JsonObject())),
				RpcFraming.Auto);

			connection.Start();

			feed.Write("{\"jsonrpc\":\"2.0\",\"id\":1,\"method\":\"m\"}\n");

			var frame = await output.WaitForFrameAsync();
			Assert.Equal(RpcFraming.Lines, connection.Framing);
			// No header block: the answer is the whole line.
			Assert.StartsWith("{\"jsonrpc\"", frame);
		}

		[Fact]
		public async Task FailsPendingRequestsWhenThePeerCloses()
		{
			using var feed = new FeedStream();
			await using var connection = new JsonRpcConnection(feed, new CapturingStream(), new Handler());
			connection.Start();

			var call = connection.InvokeAsync("never-answered", timeout: TimeSpan.FromSeconds(30));
			feed.Complete();

			var exception = await Assert.ThrowsAsync<JsonRpcException>(async () => await call.WaitAsync(TimeSpan.FromSeconds(5)));
			Assert.Equal(RpcErrorCodes.ConnectionClosed, exception.Code);
		}

		[Fact]
		public async Task TimesOutAnUnansweredRequest()
		{
			using var feed = new FeedStream();
			await using var connection = new JsonRpcConnection(feed, new CapturingStream(), new Handler());
			connection.Start();

			await Assert.ThrowsAsync<TimeoutException>(
				async () => await connection.InvokeAsync("slow", timeout: TimeSpan.FromMilliseconds(50)));
		}

		private sealed class Handler(Func<string, JsonNode?, Task<JsonNode?>>? onRequest = null) : IJsonRpcMessageHandler
		{
			public TaskCompletionSource NotificationReceived { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

			public string? NotificationMethod { get; private set; }

			public Task<JsonNode?> HandleRequestAsync(
				IJsonRpcPeer peer,
				string method,
				JsonNode? parameters,
				CancellationToken cancellationToken = default)
			{
				return onRequest is null
					? throw new JsonRpcException(RpcErrorCodes.MethodNotFound, $"No handler for '{method}'.")
					: onRequest(method, parameters);
			}

			public Task HandleNotificationAsync(
				IJsonRpcPeer peer,
				string method,
				JsonNode? parameters,
				CancellationToken cancellationToken = default)
			{
				NotificationMethod = method;
				NotificationReceived.TrySetResult();
				return Task.CompletedTask;
			}
		}

		private sealed class CapturingStream : Stream
		{
			private readonly MemoryStream _inner = new();

			public override bool CanRead => false;

			public override bool CanSeek => false;

			public override bool CanWrite => true;

			public override long Length => _inner.Length;

			public override long Position
			{
				get => _inner.Position;
				set => throw new NotSupportedException();
			}

			public string Text => Encoding.UTF8.GetString(_inner.ToArray());

			/// <summary>
			/// Waits for one complete frame and returns its payload.
			/// </summary>
			public async Task<string> WaitForFrameAsync()
			{
				var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);

				while (DateTime.UtcNow < deadline)
				{
					var text = Text;

					if (TryReadFirstFrame(text, out var frame))
						return frame!;

					await Task.Delay(10);
				}

				throw new TimeoutException($"No frame was written. Output so far: '{Text}'.");
			}

			private static bool TryReadFirstFrame(string text, out string? frame)
			{
				frame = null;

				if (text.StartsWith("Content-Length:", StringComparison.OrdinalIgnoreCase))
				{
					var separator = text.IndexOf("\r\n\r\n", StringComparison.Ordinal);
					if (separator < 0)
						return false;

					var header = text[..separator];
					var lengthText = header[(header.IndexOf(':') + 1)..].Trim();

					if (!int.TryParse(lengthText, out var length))
						return false;

					var bodyStart = separator + 4;
					if (text.Length < bodyStart + length)
						return false;

					frame = text.Substring(bodyStart, length);
					return true;
				}

				var newLine = text.IndexOf('\n');
				if (newLine < 0)
					return false;

				frame = text[..newLine].TrimEnd('\r');
				return true;
			}

			public override void Flush()
			{
			}

			public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

			public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

			public override void SetLength(long value) => throw new NotSupportedException();

			public override void Write(byte[] buffer, int offset, int count) => _inner.Write(buffer, offset, count);

			public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
			{
				_inner.Write(buffer, offset, count);
				return Task.CompletedTask;
			}
		}

		private sealed class FeedStream : Stream
		{
			private readonly Channel<byte[]> _chunks = Channel.CreateUnbounded<byte[]>();
			private byte[]? _current;
			private int _offset;

			public void Write(string text) => _chunks.Writer.TryWrite(Encoding.UTF8.GetBytes(text));

			public void Complete() => _chunks.Writer.TryComplete();

			public override bool CanRead => true;

			public override bool CanSeek => false;

			public override bool CanWrite => false;

			public override long Length => throw new NotSupportedException();

			public override long Position
			{
				get => throw new NotSupportedException();
				set => throw new NotSupportedException();
			}

			public override async ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
			{
				while (_current is null || _offset >= _current.Length)
				{
					if (!await _chunks.Reader.WaitToReadAsync(cancellationToken))
						return 0;

					if (_chunks.Reader.TryRead(out var next))
					{
						_current = next;
						_offset = 0;
					}
				}

				var count = Math.Min(buffer.Length, _current.Length - _offset);
				_current.AsMemory(_offset, count).CopyTo(buffer);
				_offset += count;
				return count;
			}

			public override int Read(byte[] buffer, int offset, int count)
				=> ReadAsync(buffer.AsMemory(offset, count)).AsTask().GetAwaiter().GetResult();

			public override void Flush()
			{
			}

			public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

			public override void SetLength(long value) => throw new NotSupportedException();

			public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

			protected override void Dispose(bool disposing)
			{
				if (disposing)
					Complete();

				base.Dispose(disposing);
			}
		}
	}
}
