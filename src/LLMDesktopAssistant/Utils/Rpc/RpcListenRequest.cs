namespace LLMDesktopAssistant.Utils.Rpc
{
	/// <summary>
	/// What a listener provider needs to bind: where, and how deep the accept backlog is.
	/// </summary>
	public sealed class RpcListenRequest
	{
		/// <summary>
		/// Gets the endpoint to bind, in the provider's own notation - <c>host:port</c> for <c>tcp</c>.
		/// </summary>
		/// <remarks>
		/// Port <c>0</c> asks the operating system for a free port; the bound one is reported by
		/// <see cref="IRpcListener.Port"/>.
		/// </remarks>
		public required string Target { get; init; }

		/// <summary>
		/// Gets the number of connections the operating system may queue before an accept, or <c>0</c>
		/// for the platform default.
		/// </summary>
		public int Backlog { get; init; }
	}
}
