namespace LLMDesktopAssistant.Utils.Rpc
{
	/// <summary>
	/// The error codes JSON-RPC 2.0 reserves, plus the one this implementation raises on its own.
	/// </summary>
	/// <remarks>
	/// The <c>-32000</c> range belongs to the implementation rather than to the specification, which is
	/// why the connection's own failures live there instead of colliding with a peer's error codes.
	/// </remarks>
	public static class RpcErrorCodes
	{
		/// <summary>
		/// The received payload was not valid JSON.
		/// </summary>
		public const int ParseError = -32700;

		/// <summary>
		/// The payload is JSON but not a valid request, notification or response.
		/// </summary>
		public const int InvalidRequest = -32600;

		/// <summary>
		/// The peer was not routed to any handler.
		/// </summary>
		public const int MethodNotFound = -32601;

		/// <summary>
		/// The handler rejected the parameters it was given.
		/// </summary>
		public const int InvalidParams = -32602;

		/// <summary>
		/// The handler failed for any other reason.
		/// </summary>
		public const int InternalError = -32603;

		/// <summary>
		/// The connection is closed, never opened, or lost its transport.
		/// </summary>
		public const int ConnectionClosed = -32000;

		/// <summary>
		/// The peer did not answer within the configured timeout.
		/// </summary>
		public const int RequestTimeout = -32001;

		/// <summary>
		/// The request was cancelled by its sender, as LSP defines it.
		/// </summary>
		public const int RequestCancelled = -32800;
	}
}
