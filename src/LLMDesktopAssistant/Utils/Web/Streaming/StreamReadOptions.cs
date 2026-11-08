namespace LLMDesktopAssistant.Utils.Web.Streaming
{
	/// <summary>
	/// How a streaming response is decoded into events.
	/// </summary>
	public sealed record StreamReadOptions
	{
		/// <summary>
		/// Gets the wire format to decode.
		/// </summary>
		public required StreamMode Mode { get; init; }

		/// <summary>
		/// Gets a value indicating whether Server-Sent Events comment lines are emitted as events.
		/// </summary>
		public bool Comments { get; init; }

		/// <summary>
		/// Gets a value indicating whether the Server-Sent Events payload is parsed as JSON.
		/// </summary>
		/// <remarks>
		/// Applies to <see cref="StreamMode.Sse"/> only: <see cref="StreamMode.JsonLines"/> always parses
		/// and <see cref="StreamMode.RawLines"/> never does.
		/// </remarks>
		public bool ParseJson { get; init; }

		/// <summary>
		/// Gets what happens to input that cannot be decoded.
		/// </summary>
		public StreamParseErrorPolicy OnError { get; init; } = StreamParseErrorPolicy.Report;

		/// <summary>
		/// Gets how long the reader waits for the next line before giving up, or
		/// <see langword="null"/> to wait indefinitely.
		/// </summary>
		/// <remarks>
		/// A server that keeps the connection open but stops sending is indistinguishable from a slow
		/// one, so the only way to notice a dead stream is an idle deadline.
		/// </remarks>
		public TimeSpan? IdleTimeout { get; init; }
	}
}
