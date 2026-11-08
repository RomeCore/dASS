using System.Text.Json.Nodes;

namespace LLMDesktopAssistant.Utils.Rpc
{
	/// <summary>
	/// The sending half of a JSON-RPC connection, handed to a message handler so it can answer the peer
	/// with requests and notifications of its own.
	/// </summary>
	/// <remarks>
	/// JSON-RPC is symmetric, so a handler is not a leaf: answering <c>workspace/configuration</c> may
	/// require asking the peer something first. A handler that only received messages would have to
	/// smuggle the connection in from outside to do that.
	/// </remarks>
	public interface IJsonRpcPeer
	{
		/// <summary>
		/// Gets a value indicating whether the connection can still be used.
		/// </summary>
		bool IsAlive { get; }

		/// <summary>
		/// Gets the framing actually in use, after auto-detection has run.
		/// </summary>
		RpcFraming Framing { get; }

		/// <summary>
		/// Gets the number of requests waiting for a response.
		/// </summary>
		int PendingCount { get; }

		/// <summary>
		/// Sends a request and waits for its response.
		/// </summary>
		/// <param name="method">The method name.</param>
		/// <param name="parameters">The parameters to send, or <see langword="null"/> for none. The node
		/// must not already belong to another JSON tree.</param>
		/// <param name="timeout">How long to wait, or <see langword="null"/> to wait for the connection.</param>
		/// <param name="cancellationToken">The token to cancel the call.</param>
		/// <returns>The decoded result, or <see langword="null"/> when the peer sent a JSON <c>null</c>.</returns>
		Task<JsonNode?> InvokeAsync(
			string method,
			JsonNode? parameters = null,
			TimeSpan? timeout = null,
			CancellationToken cancellationToken = default);

		/// <summary>
		/// Sends a notification: no identifier, no response.
		/// </summary>
		/// <param name="method">The method name.</param>
		/// <param name="parameters">The parameters to send, or <see langword="null"/> for none.</param>
		/// <param name="cancellationToken">The token to cancel the write.</param>
		Task NotifyAsync(string method, JsonNode? parameters = null, CancellationToken cancellationToken = default);
	}
}
