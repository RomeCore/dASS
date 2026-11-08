using System.Text;
using LLMDesktopAssistant.Utils.Rpc;

namespace LLMDesktopAssistant.Tests.Utils
{
	public class JsonRpcFrameReaderTests
	{
		private static JsonRpcFrameReader Reader(string content, RpcFraming framing = RpcFraming.Auto)
		{
			return new JsonRpcFrameReader(new MemoryStream(Encoding.UTF8.GetBytes(content)), framing);
		}

		[Fact]
		public async Task ReadsContentLengthFrames()
		{
			var reader = Reader("Content-Length: 7\r\n\r\n{\"a\":1}", RpcFraming.ContentLength);

			Assert.Equal("{\"a\":1}", await reader.ReadFrameAsync());
			Assert.Null(await reader.ReadFrameAsync());
		}

		[Fact]
		public async Task ReadsSeveralContentLengthFrames()
		{
			var reader = Reader(
				"Content-Length: 2\r\n\r\n{}Content-Length: 2\r\n\r\n[]",
				RpcFraming.ContentLength);

			Assert.Equal("{}", await reader.ReadFrameAsync());
			Assert.Equal("[]", await reader.ReadFrameAsync());
			Assert.Null(await reader.ReadFrameAsync());
		}

		[Fact]
		public async Task CountsBytesRatherThanCharacters()
		{
			// "Привет" is 6 characters and 12 UTF-8 bytes; a character-counting reader slices it in half.
			const string body = "{\"т\":1}";
			var bodyBytes = Encoding.UTF8.GetByteCount(body);

			var reader = Reader($"Content-Length: {bodyBytes}\r\n\r\n{body}", RpcFraming.ContentLength);

			Assert.Equal(body, await reader.ReadFrameAsync());
		}

		[Fact]
		public async Task ReadsLineDelimitedFrames()
		{
			var reader = Reader("{\"a\":1}\r\n{\"b\":2}\n", RpcFraming.Lines);

			Assert.Equal("{\"a\":1}", await reader.ReadFrameAsync());
			Assert.Equal("{\"b\":2}", await reader.ReadFrameAsync());
			Assert.Null(await reader.ReadFrameAsync());
		}

		[Fact]
		public async Task AutoDetectsContentLength()
		{
			var reader = Reader("Content-Length: 2\r\n\r\n{}");

			Assert.Equal("{}", await reader.ReadFrameAsync());
			Assert.Equal(RpcFraming.ContentLength, reader.Framing);
		}

		[Fact]
		public async Task AutoDetectsLineDelimited()
		{
			var reader = Reader("{\"jsonrpc\":\"2.0\",\"id\":1}\n");

			Assert.NotNull(await reader.ReadFrameAsync());
			Assert.Equal(RpcFraming.Lines, reader.Framing);
		}

		[Fact]
		public async Task AutoDetectionIsNotFooledByAColonInJson()
		{
			var reader = Reader("{\"jsonrpc\":\"2.0\"}\n");

			Assert.Equal("{\"jsonrpc\":\"2.0\"}", await reader.ReadFrameAsync());
			Assert.Equal(RpcFraming.Lines, reader.Framing);
		}

		[Fact]
		public async Task ReturnsNullOnAnEmptyStream()
		{
			var reader = Reader(string.Empty);

			Assert.Null(await reader.ReadFrameAsync());
		}

		[Fact]
		public async Task ThrowsOnATruncatedFrame()
		{
			var reader = Reader("Content-Length: 10\r\n\r\n{}", RpcFraming.ContentLength);

			var exception = await Assert.ThrowsAsync<JsonRpcException>(async () => await reader.ReadFrameAsync());
			Assert.Equal(RpcErrorCodes.ParseError, exception.Code);
		}

		[Fact]
		public async Task ThrowsOnAnOversizedLength()
		{
			var reader = Reader(
				$"Content-Length: {JsonRpcFrameReader.MaxFrameBytes + 1}\r\n\r\n",
				RpcFraming.ContentLength);

			var exception = await Assert.ThrowsAsync<JsonRpcException>(async () => await reader.ReadFrameAsync());
			Assert.Equal(RpcErrorCodes.ParseError, exception.Code);
		}
	}
}
