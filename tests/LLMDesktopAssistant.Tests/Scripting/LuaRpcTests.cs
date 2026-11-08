using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Channels;
using AsyncLua;
using AsyncLua.Values;
using LLMDesktopAssistant.LLM.Services;
using LLMDesktopAssistant.LLM.Settings;
using LLMDesktopAssistant.Scripting.Lua.API;
using LLMDesktopAssistant.Scripting.Lua.Rpc;
using LLMDesktopAssistant.Utils.Rpc;

namespace LLMDesktopAssistant.Tests.Scripting
{
	/// <summary>
	/// Drives the whole Lua surface of <c>rpc</c> against an in-memory peer: the routing table, the
	/// <c>invoke</c> proxy metatable and the error value are all only real once Lua touches them.
	/// </summary>
	public class LuaRpcTests
	{
		[Fact]
		public async Task TransportsAreListed()
		{
			var (lua, _, _) = CreateRuntime();

			var result = await lua.ExecuteAsync("local t = rpc.transports() return t[1]");

			Assert.Equal("fake", Assert.IsType<LuaString>(result[0]).Value);
		}

		[Fact]
		public async Task InvokeByIndexSendsTheMethodAndReturnsTheResult()
		{
			var (lua, _, _) = CreateRuntime();

			var result = await lua.ExecuteAsync("""
				local h = await rpc.connect("fake", "peer", {}, { framing = "content_length" })
				return await h.invoke:add({ a = 2, b = 3 })
				""");

			Assert.Equal(5, Assert.IsType<LuaNumber>(result[0]).Value);
		}

		[Fact]
		public async Task InvokeByCallSendsDottedNames()
		{
			var (lua, _, _) = CreateRuntime();

			var result = await lua.ExecuteAsync("""
				local h = await rpc.connect("fake", "peer", {}, { framing = "content_length" })
				return await h.invoke("textDocument/hover")
				""");

			Assert.Equal("textDocument/hover", Assert.IsType<LuaString>(result[0]).Value);
		}

		[Fact]
		public async Task AnErrorArrivesAsATableWithItsCode()
		{
			var (lua, _, _) = CreateRuntime();

			var result = await lua.ExecuteAsync("""
				local h = await rpc.connect("fake", "peer", {}, { framing = "content_length" })
				try
				  await h.invoke:explode()
				catch e do
				  return e.code, e.message
				end
				""");

			Assert.Equal(RpcErrorCodes.MethodNotFound, (int)Assert.IsType<LuaNumber>(result[0]).Value);
			Assert.Equal("nope", Assert.IsType<LuaString>(result[1]).Value);
		}

		[Fact]
		public async Task NotifySendsNoIdentifierInEitherNotation()
		{
			var (lua, output, feed) = CreateRuntime();

			await lua.ExecuteAsync("""
				local h = await rpc.connect("fake", "peer", {}, { framing = "content_length" })
				await h.notify:client_ready({ v = 1 })
				await h.notify("client/ready", { v = 2 })
				""");

			var first = JsonNode.Parse(await output.WaitForWrittenFrameAsync(0))!;
			var second = JsonNode.Parse(await output.WaitForWrittenFrameAsync(1))!;

			Assert.Null(first["id"]);
			Assert.Equal("client_ready", first["method"]!.GetValue<string>());
			Assert.Null(second["id"]);
			Assert.Equal("client/ready", second["method"]!.GetValue<string>());
		}

		[Fact]
		public async Task AnInboundRequestReachesTheFallbackAndIsAnswered()
		{
			var (lua, output, feed) = CreateRuntime();

			var script = lua.ExecuteAsync("""
				local seen = "none"
				local h, err = await rpc.connect("fake", "peer", {
				  function(method, params)
				    seen = method
				    return { handled = method }
				  end
				}, { framing = "content_length" })
				if not h then return "connect failed: " .. tostring(err) end
				return "connected"
				""");

			feed.Write(ContentLengthFrame("{\"jsonrpc\":\"2.0\",\"id\":\"srv-1\",\"method\":\"worker/do\"}"));
			await script;

			var frame = await output.WaitForWrittenFrameAsync();
			var body = JsonNode.Parse(frame)!;

			Assert.Equal("srv-1", body["id"]!.GetValue<string>());
			Assert.Equal("worker/do", body["result"]!["handled"]!.GetValue<string>());
		}

		private static (LuaState Lua, FrameCapturingStream Output, FeedStream Feed) CreateRuntime()
		{
			var feed = new FeedStream();
			var output = new FrameCapturingStream();

			output.OnFrame = frame => Answer(frame, feed);

			var lua = new LuaState().LoadDefaultLibraries();
			var registry = new LuaRpcHandleRegistry();
			var api = new LuaApiRpc([new FakeTransportProvider(feed, output)], registry, new FakeWorkingDirectory());

			var ns = new LuaTable();
			lua.Globals.Set("rpc", ns);
			api.Populate(lua.Globals, ns, null!);

			return (lua, output, feed);
		}

		/// <summary>
		/// Plays the peer: answers a request by name, and fails the method whose name says so.
		/// </summary>
		private static void Answer(string frame, FeedStream feed)
		{
			var message = JsonNode.Parse(frame);

			// Responses and notifications have nothing to answer: only requests do.
			if (message is not JsonObject request || request["id"] is null || request["method"] is null)
				return;

			var method = request["method"]!.GetValue<string>();
			var id = request["id"]!.ToJsonString();

			if (method == "explode")
			{
				feed.Write(ContentLengthFrame(
					$"{{\"jsonrpc\":\"2.0\",\"id\":{id},\"error\":{{\"code\":-32601,\"message\":\"nope\"}}}}"));
				return;
			}

			var result = method switch
			{
				"add" => Sum(request["params"]),
				_ => $"\"{method}\""
			};

			feed.Write(ContentLengthFrame($"{{\"jsonrpc\":\"2.0\",\"id\":{id},\"result\":{result}}}"));
		}

		private static string Sum(JsonNode? parameters)
		{
			var a = parameters?["a"]?.GetValue<double>() ?? 0;
			var b = parameters?["b"]?.GetValue<double>() ?? 0;
			return (a + b).ToString(CultureInfo.InvariantCulture);
		}

		private static string ContentLengthFrame(string payload)
		{
			var body = Encoding.UTF8.GetBytes(payload);
			return $"Content-Length: {body.Length}\r\n\r\n{payload}";
		}

		private sealed class FakeTransportProvider(Stream input, Stream output) : IRpcTransportProvider
		{
			public string Name => "fake";

			public Task<IRpcTransport> ConnectAsync(
				RpcTransportRequest request,
				CancellationToken cancellationToken = default)
				=> Task.FromResult<IRpcTransport>(new FakeTransport(input, output));
		}

		private sealed class FakeTransport(Stream input, Stream output) : IRpcTransport
		{
			public Stream Input => input;

			public Stream Output => output;

			public Stream? StandardError => null;

			public int? ProcessId => 4242;

			public int? ExitCode => null;

			public Task<int> WaitForExitAsync(CancellationToken cancellationToken = default) => Task.FromResult(0);

			public void Kill()
			{
			}

			public ValueTask DisposeAsync() => ValueTask.CompletedTask;
		}

		private sealed class FakeWorkingDirectory : IWorkingDirectoryAccessService
		{
			public string CheckedAccessPath(string path, DirectoryAccessMode mode, out bool isAccessed)
			{
				isAccessed = true;
				return Path.GetFullPath(path);
			}

			public string GetWorkingDirectory() => Path.GetTempPath();
		}

		/// <summary>
		/// Collects what the connection writes and hands every complete <c>Content-Length</c> frame to a
		/// callback, which is how the fake peer reacts without a timing race.
		/// </summary>
		private sealed class FrameCapturingStream : Stream
		{
			private readonly MemoryStream _inner = new();
			private readonly List<string> _frames = [];
			private int _consumed;

			public Action<string>? OnFrame { get; set; }

			/// <summary>
			/// The frames written so far, in order; they stay readable after being handed to the callback.
			/// </summary>
			public IReadOnlyList<string> Frames => _frames;

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
			/// Waits until the frame at the given position has been written and returns it.
			/// </summary>
			/// <param name="index">The zero-based position of the frame to return.</param>
			public async Task<string> WaitForWrittenFrameAsync(int index = 0)
			{
				var deadline = DateTime.UtcNow + TimeSpan.FromSeconds(5);

				while (DateTime.UtcNow < deadline)
				{
					if (_frames.Count > index)
						return _frames[index];

					await Task.Delay(10);
				}

				throw new TimeoutException(
					$"Frame {index} was never written; {_frames.Count} frame(s) so far: '{Text}'.");
			}

			public override void Flush()
			{
			}

			public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

			public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

			public override void SetLength(long value) => throw new NotSupportedException();

			public override void Write(byte[] buffer, int offset, int count)
			{
				_inner.Write(buffer, offset, count);

				while (TryReadFrame(out var frame))
				{
					_frames.Add(frame!);
					OnFrame?.Invoke(frame!);
				}
			}

			public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
			{
				Write(buffer, offset, count);
				return Task.CompletedTask;
			}

			private bool TryReadFrame(out string? frame)
			{
				frame = null;

				var text = Text;
				var separator = text.IndexOf("\r\n\r\n", _consumed, StringComparison.Ordinal);
				if (separator < 0)
					return false;

				var header = text[_consumed..separator];
				var lengthText = header[(header.IndexOf(':') + 1)..].Trim();

				if (!int.TryParse(lengthText, out var length))
					return false;

				var bodyStart = separator + 4;
				if (text.Length < bodyStart + length)
					return false;

				frame = text.Substring(bodyStart, length);
				_consumed = bodyStart + length;
				return true;
			}
		}

		private sealed class FeedStream : Stream
		{
			private readonly Channel<byte[]> _chunks = Channel.CreateUnbounded<byte[]>();
			private byte[]? _current;
			private int _offset;

			public void Write(string text) => _chunks.Writer.TryWrite(Encoding.UTF8.GetBytes(text));

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
		}
	}
}
