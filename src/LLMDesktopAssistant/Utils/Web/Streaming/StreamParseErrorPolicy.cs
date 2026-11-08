namespace LLMDesktopAssistant.Utils.Web.Streaming
{
	/// <summary>
	/// What the reader does with input it cannot decode.
	/// </summary>
	public enum StreamParseErrorPolicy
	{
		/// <summary>
		/// Drop the offending item and continue reading.
		/// </summary>
		Ignore,

		/// <summary>
		/// Emit the item with <see cref="StreamEvent.Error"/> set and no payload, then continue.
		/// </summary>
		Report,

		/// <summary>
		/// Abort the whole stream with an <see cref="System.IO.InvalidDataException"/>.
		/// </summary>
		Abort
	}
}
