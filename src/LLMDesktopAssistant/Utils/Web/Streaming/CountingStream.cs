namespace LLMDesktopAssistant.Utils.Web.Streaming
{
	/// <summary>
	/// Pass-through stream that counts the bytes actually read through it.
	/// </summary>
	/// <remarks>
	/// A decoded event cannot be measured in bytes after the fact: the decoder reports characters.
	/// Counting at the stream keeps the reported size exact for multi-byte text.
	/// </remarks>
	public sealed class CountingStream(Stream inner) : Stream
	{
		private long _bytesRead;

		/// <summary>
		/// Gets the number of bytes read from the inner stream so far.
		/// </summary>
		public long BytesRead => _bytesRead;

		/// <inheritdoc />
		public override bool CanRead => true;

		/// <inheritdoc />
		public override bool CanSeek => false;

		/// <inheritdoc />
		public override bool CanWrite => false;

		/// <inheritdoc />
		public override long Length => throw new NotSupportedException();

		/// <inheritdoc />
		public override long Position
		{
			get => throw new NotSupportedException();
			set => throw new NotSupportedException();
		}

		/// <inheritdoc />
		public override int Read(byte[] buffer, int offset, int count) => Count(inner.Read(buffer, offset, count));

		/// <inheritdoc />
		public override int Read(Span<byte> buffer) => Count(inner.Read(buffer));

		/// <inheritdoc />
		public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
		{
			return ReadCoreAsync(buffer.AsMemory(offset, count), cancellationToken).AsTask();
		}

		/// <inheritdoc />
		public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default)
		{
			return ReadCoreAsync(buffer, cancellationToken);
		}

		/// <inheritdoc />
		public override void Flush() => inner.Flush();

		/// <inheritdoc />
		public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

		/// <inheritdoc />
		public override void SetLength(long value) => throw new NotSupportedException();

		/// <inheritdoc />
		public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

		/// <inheritdoc />
		protected override void Dispose(bool disposing)
		{
			if (disposing)
				inner.Dispose();

			base.Dispose(disposing);
		}

		/// <inheritdoc />
		public override async ValueTask DisposeAsync()
		{
			await inner.DisposeAsync();
			await base.DisposeAsync();
		}

		private async ValueTask<int> ReadCoreAsync(Memory<byte> buffer, CancellationToken cancellationToken)
		{
			var read = await inner.ReadAsync(buffer, cancellationToken);
			_bytesRead += read;
			return read;
		}

		private int Count(int read)
		{
			_bytesRead += read;
			return read;
		}
	}
}
