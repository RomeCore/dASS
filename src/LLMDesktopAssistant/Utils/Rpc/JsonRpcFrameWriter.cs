using System.Globalization;
using System.Text;

namespace LLMDesktopAssistant.Utils.Rpc
{
	/// <summary>
	/// Writes JSON-RPC frames to a byte stream.
	/// </summary>
	/// <remarks>
	/// The framing is mutable because <see cref="RpcFraming.Auto"/> starts as <c>Content-Length</c> and
	/// switches to lines once the reader has recognised a line-delimited peer. Writes are serialized, so
	/// a response produced by the dispatcher cannot interleave with a request sent by a script.
	/// </remarks>
	public sealed class JsonRpcFrameWriter
	{
		private readonly Stream _stream;
		private readonly SemaphoreSlim _mutex = new(1, 1);

		/// <summary>
		/// Initializes a writer over the given stream.
		/// </summary>
		/// <param name="stream">The stream to write frames to.</param>
		/// <param name="framing">The framing to use; <see cref="RpcFraming.Auto"/> writes
		/// <c>Content-Length</c> until the reader detects otherwise.</param>
		public JsonRpcFrameWriter(Stream stream, RpcFraming framing = RpcFraming.ContentLength)
		{
			_stream = stream ?? throw new ArgumentNullException(nameof(stream));
			Framing = framing == RpcFraming.Auto ? RpcFraming.ContentLength : framing;
		}

		/// <summary>
		/// Gets or sets the framing used for the next write.
		/// </summary>
		public RpcFraming Framing { get; set; }

		/// <summary>
		/// Writes one payload as a frame.
		/// </summary>
		/// <param name="payload">The JSON text to send.</param>
		/// <param name="cancellationToken">The token to cancel the write.</param>
		public async Task WriteAsync(string payload, CancellationToken cancellationToken = default)
		{
			var body = Encoding.UTF8.GetBytes(payload);

			byte[] frame;
			if (Framing == RpcFraming.ContentLength)
			{
				var header = Encoding.ASCII.GetBytes(
					$"Content-Length: {body.Length.ToString(CultureInfo.InvariantCulture)}\r\n\r\n");

				frame = new byte[header.Length + body.Length];
				header.CopyTo(frame, 0);
				body.CopyTo(frame, header.Length);
			}
			else
			{
				frame = new byte[body.Length + 1];
				body.CopyTo(frame, 0);
				frame[^1] = (byte)'\n';
			}

			await _mutex.WaitAsync(cancellationToken).ConfigureAwait(false);
			try
			{
				await _stream.WriteAsync(frame, cancellationToken).ConfigureAwait(false);
				await _stream.FlushAsync(cancellationToken).ConfigureAwait(false);
			}
			finally
			{
				_mutex.Release();
			}
		}
	}
}
