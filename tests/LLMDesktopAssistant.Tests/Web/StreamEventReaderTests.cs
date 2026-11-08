using LLMDesktopAssistant.Utils.Web.Streaming;

namespace LLMDesktopAssistant.Tests.Web
{
	public class StreamEventReaderTests
	{
		private static async Task<List<StreamEvent>> ReadAsync(string text, StreamReadOptions options)
		{
			var events = new List<StreamEvent>();

			using var body = new StringReader(text);
			await foreach (var item in StreamEventReader.ReadAsync(body, options))
				events.Add(item);

			return events;
		}

		private static StreamReadOptions Sse(bool comments = false, bool json = false,
			StreamParseErrorPolicy policy = StreamParseErrorPolicy.Report, TimeSpan? idleTimeout = null)
		{
			return new StreamReadOptions
			{
				Mode = StreamMode.Sse,
				Comments = comments,
				ParseJson = json,
				OnError = policy,
				IdleTimeout = idleTimeout
			};
		}

		private static StreamReadOptions JsonLines(StreamParseErrorPolicy policy = StreamParseErrorPolicy.Report)
		{
			return new StreamReadOptions { Mode = StreamMode.JsonLines, OnError = policy };
		}

		[Fact]
		public async Task Sse_SingleFrame_IsDispatched()
		{
			var events = await ReadAsync("data: hello\n\n", Sse());

			var item = Assert.Single(events);
			Assert.Equal(1, item.Index);
			Assert.Equal("hello", item.Data);
			Assert.Null(item.Event);
			Assert.Null(item.Error);
		}

		[Fact]
		public async Task Sse_MultipleDataLines_AreJoinedByNewline()
		{
			var events = await ReadAsync("data: one\ndata: two\n\n", Sse());

			var item = Assert.Single(events);
			Assert.Equal("one\ntwo", item.Data);
		}

		[Fact]
		public async Task Sse_EventAndIdAreExposed()
		{
			var events = await ReadAsync("event: delta\nid: 42\ndata: x\n\n", Sse());

			var item = Assert.Single(events);
			Assert.Equal("delta", item.Event);
			Assert.Equal("42", item.Id);
			Assert.Equal("x", item.Data);
		}

		[Fact]
		public async Task Sse_CommentIsNotAnEvent()
		{
			var events = await ReadAsync(": keep-alive\n\n", Sse());

			Assert.Empty(events);
		}

		[Fact]
		public async Task Sse_CommentIsEmittedWhenAsked()
		{
			var events = await ReadAsync(": keep-alive\n\n", Sse(comments: true));

			var item = Assert.Single(events);
			Assert.Equal("keep-alive", item.Comment);
			Assert.Null(item.Event);
			Assert.Null(item.Data);
		}

		[Fact]
		public async Task Sse_LastFrameWithoutBlankLine_IsDispatched()
		{
			var events = await ReadAsync("data: tail", Sse());

			var item = Assert.Single(events);
			Assert.Equal("tail", item.Data);
		}

		[Fact]
		public async Task Sse_CrlfLineEndings_AreHandled()
		{
			var events = await ReadAsync("data: a\r\n\r\n", Sse());

			var item = Assert.Single(events);
			Assert.Equal("a", item.Data);
		}

		[Fact]
		public async Task Sse_IndexCountsEmittedEventsOnly()
		{
			var events = await ReadAsync("data: a\n\n: c\n\ndata: b\n\n", Sse(comments: true));

			Assert.Equal(3, events.Count);
			Assert.Equal([1, 2, 3], events.Select(e => e.Index));
		}

		[Fact]
		public async Task Sse_JsonPayloadIsDecoded()
		{
			var events = await ReadAsync("data: {\"a\":1}\n\n", Sse(json: true));

			var item = Assert.Single(events);
			Assert.True(item.IsJsonDecoded);
			Assert.Equal(1, item.Json!["a"]!.GetValue<int>());
			Assert.Equal("{\"a\":1}", item.Data);
		}

		[Fact]
		public async Task Sse_InvalidJson_IsReported()
		{
			var events = await ReadAsync("data: [DONE]\n\n", Sse(json: true));

			var item = Assert.Single(events);
			Assert.NotNull(item.Error);
			Assert.Null(item.Data);
			Assert.False(item.IsJsonDecoded);
		}

		[Fact]
		public async Task Sse_InvalidJson_Aborts()
		{
			await Assert.ThrowsAsync<InvalidDataException>(
				() => ReadAsync("data: [DONE]\n\n", Sse(json: true, policy: StreamParseErrorPolicy.Abort)));
		}

		[Fact]
		public async Task Sse_UnknownField_IsReportedAsAnError()
		{
			var events = await ReadAsync("foo: bar\n\n", Sse());

			var item = Assert.Single(events);
			Assert.Contains("unknown field", item.Error);
		}

		[Fact]
		public async Task Sse_UnknownField_IsDroppedWhenIgnored()
		{
			var events = await ReadAsync("foo: bar\n\n", Sse(policy: StreamParseErrorPolicy.Ignore));

			Assert.Empty(events);
		}

		[Fact]
		public async Task JsonLines_EachLineIsAnEvent()
		{
			var events = await ReadAsync("{\"a\":1}\n{\"b\":2}\n", JsonLines());

			Assert.Equal(2, events.Count);
			Assert.Equal([1, 2], events.Select(e => e.Index));
			Assert.Equal(2, events[1].Json!["b"]!.GetValue<int>());
		}

		[Fact]
		public async Task JsonLines_BlankLinesAreSkipped()
		{
			var events = await ReadAsync("\n\n{\"a\":1}\n\n", JsonLines());

			var item = Assert.Single(events);
			Assert.Equal(1, item.Index);
		}

		[Fact]
		public async Task JsonLines_InvalidLine_IsReported()
		{
			var events = await ReadAsync("not json\n{\"a\":1}\n", JsonLines());

			Assert.Equal(2, events.Count);
			Assert.NotNull(events[0].Error);
			Assert.Null(events[0].Data);
			Assert.Equal(1, events[1].Json!["a"]!.GetValue<int>());
		}

		[Fact]
		public async Task JsonLines_InvalidLine_Aborts()
		{
			await Assert.ThrowsAsync<InvalidDataException>(
				() => ReadAsync("not json\n", JsonLines(StreamParseErrorPolicy.Abort)));
		}

		[Fact]
		public async Task RawLines_EmitsEveryLine()
		{
			var options = new StreamReadOptions { Mode = StreamMode.RawLines };

			var events = await ReadAsync("a\nb\nc", options);

			Assert.Equal(["a", "b", "c"], events.Select(e => e.Line));
			Assert.Equal([1, 2, 3], events.Select(e => e.Index));
			Assert.All(events, e => Assert.Equal(e.Line, e.Data));
		}

		[Fact]
		public async Task IdleTimeout_FailsTheStream()
		{
			var options = new StreamReadOptions
			{
				Mode = StreamMode.RawLines,
				IdleTimeout = TimeSpan.FromMilliseconds(50)
			};

			using var body = new SlowReader("a\n", TimeSpan.FromSeconds(5));

			await Assert.ThrowsAsync<TimeoutException>(async () =>
			{
				await foreach (var _ in StreamEventReader.ReadAsync(body, options))
				{
				}
			});
		}

		[Fact]
		public async Task CountingStream_CountsBytesActuallyRead()
		{
			var payload = "привет"u8.ToArray();
			using var counter = new CountingStream(new MemoryStream(payload));

			var buffer = new byte[3];
			while (await counter.ReadAsync(buffer) > 0)
			{
			}

			Assert.Equal(payload.Length, counter.BytesRead);
		}

		/// <summary>
		/// A line source that never answers in time, standing in for a server that went silent.
		/// </summary>
		private sealed class SlowReader(string text, TimeSpan delay) : StringReader(text)
		{
			public override async ValueTask<string?> ReadLineAsync(CancellationToken cancellationToken = default)
			{
				await Task.Delay(delay, cancellationToken);
				return await base.ReadLineAsync(cancellationToken);
			}
		}
	}
}
