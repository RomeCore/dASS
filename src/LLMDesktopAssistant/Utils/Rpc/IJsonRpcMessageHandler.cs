using System.Text.Json.Nodes;

namespace LLMDesktopAssistant.Utils.Rpc
{
	/// <summary>
	/// Receives the messages a peer sends on its own initiative: requests it expects an answer to and
	/// notifications it expects nothing for.
	/// </summary>
	/// <remarks>
	/// Answers to requests this side sent never reach this interface: the connection resolves them from
	/// its own pending map, without entering whatever runtime the handler lives in. That separation is
	/// what lets a handler await a nested call through <see cref="IJsonRpcPeer"/> without deadlocking the
	/// connection.
	/// </remarks>
	public interface IJsonRpcMessageHandler
	{
		/// <summary>
		/// Handles a request from the peer.
		/// </summary>
		/// <param name="peer">The connection, so the handler can send its own messages while answering.</param>
		/// <param name="method">The method name.</param>
		/// <param name="parameters">The parameters, or <see langword="null"/> when absent.</param>
		/// <param name="cancellationToken">The token to cancel the handling.</param>
		/// <returns>The result to send back, or <see langword="null"/> for a JSON <c>null</c> result.</returns>
		/// <exception cref="JsonRpcException">The request is answered with an error instead of a result.</exception>
		Task<JsonNode?> HandleRequestAsync(
			IJsonRpcPeer peer,
			string method,
			JsonNode? parameters,
			CancellationToken cancellationToken = default);

		/// <summary>
		/// Handles a notification from the peer.
		/// </summary>
		/// <param name="peer">The connection, so the handler can react with messages of its own.</param>
		/// <param name="method">The method name.</param>
		/// <param name="parameters">The parameters, or <see langword="null"/> when absent.</param>
		/// <param name="cancellationToken">The token to cancel the handling.</param>
		Task HandleNotificationAsync(
			IJsonRpcPeer peer,
			string method,
			JsonNode? parameters,
			CancellationToken cancellationToken = default);
	}
}
