using System.Text.Json.Nodes;

namespace LLMDesktopAssistant.Utils.Web.Streaming
{
	/// <summary>
	/// One decoded item of a streaming response.
	/// </summary>
	/// <remarks>
	/// The populated members depend on the mode: <see cref="Event"/>, <see cref="Id"/> and
	/// <see cref="Comment"/> come from Server-Sent Events, <see cref="Json"/> from a successful JSON
	/// parse of the payload, <see cref="Error"/> from a payload that could not be decoded.
	/// </remarks>
	public sealed class StreamEvent
	{
		/// <summary>
		/// Gets the 1-based ordinal of this event among the events actually emitted.
		/// </summary>
		/// <remarks>
		/// Empty lines and unnamed Server-Sent Events fields are not events, so this is not a line number.
		/// </remarks>
		public required int Index { get; init; }

		/// <summary>
		/// Gets the raw text this event was decoded from.
		/// </summary>
		public string? Line { get; init; }

		/// <summary>
		/// Gets the Server-Sent Events <c>event</c> field, or <see langword="null"/> when absent.
		/// </summary>
		public string? Event { get; init; }

		/// <summary>
		/// Gets the undecoded payload: the joined Server-Sent Events <c>data</c> lines or the JSON line.
		/// </summary>
		public string? Data { get; init; }

		/// <summary>
		/// Gets the Server-Sent Events <c>id</c> field, or <see langword="null"/> when absent.
		/// </summary>
		/// <remarks>
		/// The last id seen is what a resuming client sends back as <c>Last-Event-ID</c>.
		/// </remarks>
		public string? Id { get; init; }

		/// <summary>
		/// Gets the text of a Server-Sent Events comment line.
		/// </summary>
		/// <remarks>
		/// A comment is not an event: <see cref="Event"/> stays <see langword="null"/> on such a record.
		/// </remarks>
		public string? Comment { get; init; }

		/// <summary>
		/// Gets the reason the payload could not be decoded, or <see langword="null"/> when it was.
		/// </summary>
		public string? Error { get; init; }

		/// <summary>
		/// Gets the decoded payload, or <see langword="null"/> when the payload was not parsed.
		/// </summary>
		public JsonNode? Json { get; init; }

		/// <summary>
		/// Gets a value indicating whether <see cref="Json"/> is the result of a successful parse.
		/// </summary>
		/// <remarks>
		/// A JSON <c>null</c> payload parses to a <see langword="null"/> node, so the node alone cannot
		/// tell "decoded null" from "not decoded".
		/// </remarks>
		public bool IsJsonDecoded { get; init; }
	}
}
