namespace LLMDesktopAssistant.Utils.Web.Streaming
{
	/// <summary>
	/// The line-oriented wire format a streaming HTTP response is decoded from.
	/// </summary>
	public enum StreamMode
	{
		/// <summary>
		/// Server-Sent Events: <c>field: value</c> lines accumulated into a frame that is
		/// dispatched on a blank line or at the end of the stream.
		/// </summary>
		Sse,

		/// <summary>
		/// One JSON document per line, each line an independent event.
		/// </summary>
		JsonLines,

		/// <summary>
		/// One unparsed line per event.
		/// </summary>
		RawLines
	}
}
