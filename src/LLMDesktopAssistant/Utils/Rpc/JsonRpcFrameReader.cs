using System.Text;

namespace LLMDesktopAssistant.Utils.Rpc
{
	/// <summary>
	/// Reads JSON-RPC frames from a byte stream, one frame at a time.
	/// </summary>
	/// <remarks>
	/// The reader works on bytes rather than characters because a <c>Content-Length</c> counts bytes and
	/// a character-counting reader silently mis-slices every non-ASCII payload. It also accepts either
	/// CRLF or LF as a line ending, since real peers are inconsistent about it.
	/// </remarks>
	public sealed class JsonRpcFrameReader
	{
		/// <summary>
		/// The largest frame accepted, a guard against a rogue <c>Content-Length</c> that would otherwise
		/// allocate the value it names.
		/// </summary>
		public const int MaxFrameBytes = 16 * 1024 * 1024;

		private const string contentLengthHeader = "Content-Length";

		private readonly Stream _stream;
		private readonly byte[] _buffer = new byte[8192];

		private int _start;
		private int _end;
		private bool _completed;

		/// <summary>
		/// Initializes a reader over the given stream.
		/// </summary>
		/// <param name="stream">The stream to read frames from.</param>
		/// <param name="framing">The framing to expect, or <see cref="RpcFraming.Auto"/> to detect it.</param>
		public JsonRpcFrameReader(Stream stream, RpcFraming framing = RpcFraming.Auto)
		{
			_stream = stream ?? throw new ArgumentNullException(nameof(stream));
			Framing = framing;
		}

		/// <summary>
		/// Gets the framing in use; <see cref="RpcFraming.Auto"/> is replaced by the detected value once
		/// the first frame arrives.
		/// </summary>
		public RpcFraming Framing { get; private set; }

		/// <summary>
		/// Reads the next frame.
		/// </summary>
		/// <param name="cancellationToken">The token to cancel the read.</param>
		/// <returns>The frame payload, or <see langword="null"/> at the end of the stream.</returns>
		public async ValueTask<string?> ReadFrameAsync(CancellationToken cancellationToken = default)
		{
			if (_completed)
				return null;

			if (Framing == RpcFraming.Auto)
			{
				var first = await ReadLineAsync(cancellationToken).ConfigureAwait(false);
				if (first is null)
					return null;

				if (LooksLikeHeader(first))
				{
					Framing = RpcFraming.ContentLength;
					return await ReadContentLengthFrameAsync(first, cancellationToken).ConfigureAwait(false);
				}

				Framing = RpcFraming.Lines;
				return first;
			}

			if (Framing == RpcFraming.ContentLength)
				return await ReadContentLengthFrameAsync(null, cancellationToken).ConfigureAwait(false);

			return await ReadLineAsync(cancellationToken).ConfigureAwait(false);
		}

		/// <summary>
		/// Reads a header block and the body it announces.
		/// </summary>
		/// <param name="firstLine">A line already consumed by framing detection, or <see langword="null"/>.</param>
		/// <param name="cancellationToken">The token to cancel the read.</param>
		/// <returns>The body, or <see langword="null"/> when the stream ends before a frame starts.</returns>
		private async ValueTask<string?> ReadContentLengthFrameAsync(string? firstLine, CancellationToken cancellationToken)
		{
			int? length = null;
			string? line = firstLine;
			var sawHeader = false;

			while (true)
			{
				line ??= await ReadLineAsync(cancellationToken).ConfigureAwait(false);

				if (line is null)
				{
					// Blank lines between frames are tolerated; a frame that has begun is not.
					if (length is not null)
					{
						throw new JsonRpcException(
							RpcErrorCodes.ParseError,
							"The stream ended inside a Content-Length frame.");
					}

					if (!sawHeader)
						return null;

					throw new JsonRpcException(
						RpcErrorCodes.ParseError,
						"A Content-Length framed message announced no Content-Length header.");
				}

				if (line.Length == 0)
				{
					if (length is not null)
						break;

					// A blank line before any header: skip it and keep looking for the block.
					continue;
				}

				sawHeader = true;

				var colon = line.IndexOf(':');
				if (colon > 0 &&
					line.AsSpan(0, colon).Trim().Equals(contentLengthHeader, StringComparison.OrdinalIgnoreCase) &&
					int.TryParse(line.AsSpan(colon + 1).Trim(), out var parsed))
				{
					length = parsed;
				}

				line = null;
			}

			if (length is null)
			{
				throw new JsonRpcException(
					RpcErrorCodes.ParseError,
					"A Content-Length framed message announced no Content-Length header.");
			}

			if (length < 0 || length > MaxFrameBytes)
			{
				throw new JsonRpcException(
					RpcErrorCodes.ParseError,
					$"A Content-Length of {length} is outside the accepted range 0..{MaxFrameBytes}.");
			}

			var body = await ReadExactlyAsync(length.Value, cancellationToken).ConfigureAwait(false);
			return Encoding.UTF8.GetString(body);
		}

		/// <summary>
		/// Reads one line without its terminator.
		/// </summary>
		/// <param name="cancellationToken">The token to cancel the read.</param>
		/// <returns>The line, or <see langword="null"/> at the end of the stream.</returns>
		private async ValueTask<string?> ReadLineAsync(CancellationToken cancellationToken)
		{
			var bytes = new List<byte>(128);

			while (true)
			{
				if (_start == _end && !await FillAsync(cancellationToken).ConfigureAwait(false))
					return bytes.Count == 0 ? null : Encoding.UTF8.GetString(bytes.ToArray());

				var value = _buffer[_start++];

				if (value == (byte)'\n')
				{
					if (bytes.Count > 0 && bytes[^1] == (byte)'\r')
						bytes.RemoveAt(bytes.Count - 1);

					return Encoding.UTF8.GetString(bytes.ToArray());
				}

				bytes.Add(value);

				if (bytes.Count > MaxFrameBytes)
				{
					throw new JsonRpcException(
						RpcErrorCodes.ParseError,
						$"A single line exceeded {MaxFrameBytes} bytes without a line break.");
				}
			}
		}

		/// <summary>
		/// Reads exactly the requested number of bytes.
		/// </summary>
		/// <param name="count">The number of bytes to read.</param>
		/// <param name="cancellationToken">The token to cancel the read.</param>
		/// <returns>The bytes read.</returns>
		private async ValueTask<byte[]> ReadExactlyAsync(int count, CancellationToken cancellationToken)
		{
			var result = new byte[count];
			var read = 0;

			while (read < count)
			{
				if (_start == _end && !await FillAsync(cancellationToken).ConfigureAwait(false))
				{
					throw new JsonRpcException(
						RpcErrorCodes.ParseError,
						$"The stream ended after {read} of {count} announced bytes.");
				}

				var available = Math.Min(_end - _start, count - read);
				Buffer.BlockCopy(_buffer, _start, result, read, available);
				_start += available;
				read += available;
			}

			return result;
		}

		/// <summary>
		/// Refills the read buffer.
		/// </summary>
		/// <param name="cancellationToken">The token to cancel the read.</param>
		/// <returns><see langword="false"/> at the end of the stream.</returns>
		private async ValueTask<bool> FillAsync(CancellationToken cancellationToken)
		{
			_start = 0;
			_end = await _stream.ReadAsync(_buffer, cancellationToken).ConfigureAwait(false);

			if (_end == 0)
			{
				_completed = true;
				return false;
			}

			return true;
		}

		/// <summary>
		/// Tells a header line from a payload line.
		/// </summary>
		/// <param name="line">The first line of a frame.</param>
		/// <returns><see langword="true"/> when the line is a <c>Token: value</c> header.</returns>
		/// <remarks>
		/// JSON objects contain colons too, so the test requires the part before the colon to be a bare
		/// token: that rejects <c>{"a":1}</c> while accepting <c>Content-Length: 12</c>.
		/// </remarks>
		private static bool LooksLikeHeader(string line)
		{
			if (line.Length == 0 || line[0] is '{' or '[')
				return false;

			var colon = line.IndexOf(':');
			if (colon <= 0)
				return false;

			for (var i = 0; i < colon; i++)
			{
				var value = line[i];
				if (!char.IsLetterOrDigit(value) && value != '-' && value != '_')
					return false;
			}

			return true;
		}
	}
}
