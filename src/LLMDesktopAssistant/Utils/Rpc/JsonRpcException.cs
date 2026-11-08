using System.Text.Json.Nodes;

namespace LLMDesktopAssistant.Utils.Rpc
{
	/// <summary>
	/// A JSON-RPC error received from the peer, raised by a handler, or produced by the connection itself.
	/// </summary>
	/// <remarks>
	/// Thrown handlers are turned into an error response with the same <see cref="Code"/> and
	/// <see cref="Message"/>, and a failed request rethrows this exception on the caller's side, so a
	/// peer's error code survives the round trip in both directions.
	/// </remarks>
	public class JsonRpcException : Exception
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="JsonRpcException"/> class.
		/// </summary>
		/// <param name="code">The numeric error code.</param>
		/// <param name="message">The human-readable error message.</param>
		/// <param name="data">The optional error payload.</param>
		public JsonRpcException(int code, string message, JsonNode? data = null) : base(message)
		{
			Code = code;
			Data = data;
		}

		/// <summary>
		/// Gets the numeric error code.
		/// </summary>
		public int Code { get; }

		/// <summary>
		/// Gets the optional error payload the peer attached to the error.
		/// </summary>
		public new JsonNode? Data { get; }
	}
}
