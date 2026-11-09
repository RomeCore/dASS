using System.IO;
using System.Text;

namespace LLMDesktopAssistant.Desktop.Execution
{
	/// <summary>
	/// The writable standard input of a launched process, kept open for the whole run.
	/// </summary>
	/// <remarks>
	/// A process launched without an initial payload used to inherit the host's stdin and could never be
	/// fed later. This wrapper owns the redirect instead: the launcher writes its payload through it and a
	/// caller may keep writing afterwards. Text and raw bytes share one gate, so they never interleave.
	/// <para>
	/// The writes are synchronous on purpose. A pipe write returns only once the child has taken the bytes,
	/// so ordering and end-of-file are settled when the call returns; an asynchronous write hands a script
	/// a task it can forget, and a forgotten write is silently cut short by <see cref="Close"/>.
	/// </para>
	/// <para>
	/// The stream writer the process created is never written through: it emits a BOM before its first
	/// write whenever <c>StandardInputEncoding</c> carries one, and a child that sorts or hashes its input
	/// sees that BOM as data. Text is encoded here, without a BOM, straight into the underlying stream.
	/// </para>
	/// </remarks>
	public sealed class ProcessStandardInput : IDisposable
	{
		private static readonly UTF8Encoding _utf8 = new(encoderShouldEmitUTF8Identifier: false);
		private readonly Lock _gate = new();
		private readonly StreamWriter _writer;
		private readonly Stream _stream;

		/// <summary>
		/// Wraps the redirected standard input of a started process.
		/// </summary>
		/// <remarks>
		/// The stream is taken once, here: the <see cref="StreamWriter.BaseStream"/> getter flushes the writer,
		/// and a flush writes the encoding preamble - the BOM a child would then read as data.
		/// </remarks>
		/// <param name="writer">The writer produced by the process start information.</param>
		public ProcessStandardInput(StreamWriter writer)
		{
			_writer = writer;
			_stream = writer.BaseStream;
		}

		/// <summary>
		/// Whether the input was closed and accepts no more writes.
		/// </summary>
		public bool IsClosed { get; private set; }

		/// <summary>
		/// Writes text into the input as BOM-less UTF-8, without adding a line ending.
		/// </summary>
		/// <param name="text">The text to write.</param>
		/// <returns><see langword="true"/> when the text was written and flushed.</returns>
		public bool WriteText(string text)
			=> string.IsNullOrEmpty(text) || WriteBytes(_utf8.GetBytes(text));

		/// <summary>
		/// Writes raw bytes into the input verbatim.
		/// </summary>
		/// <param name="bytes">The bytes to write.</param>
		/// <returns><see langword="true"/> when the bytes were written and flushed.</returns>
		public bool WriteBytes(ReadOnlySpan<byte> bytes)
		{
			if (bytes.IsEmpty)
				return false;

			lock (_gate)
			{
				if (IsClosed)
					return false;

				try
				{
					_stream.Write(bytes);
					_stream.Flush();
					return true;
				}
				catch (Exception ex) when (ex is IOException or ObjectDisposedException or InvalidOperationException)
				{
					// The process exited between the check and the write; broken pipe is not an error.
					return false;
				}
			}
		}

		/// <summary>
		/// Closes the input, sending end-of-file to the process once every pending write is done.
		/// </summary>
		/// <returns><see langword="true"/> when this call closed it.</returns>
		public bool Close()
		{
			lock (_gate)
			{
				if (IsClosed)
					return false;

				IsClosed = true;
				try
				{
					_writer.Close();
					return true;
				}
				catch (Exception)
				{
					return false;
				}
			}
		}

		/// <summary>
		/// Closes the input.
		/// </summary>
		public void Dispose() => Close();
	}
}
