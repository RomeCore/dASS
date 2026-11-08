namespace LLMDesktopAssistant.Utils.Rpc
{
	/// <summary>
	/// Creates one kind of <see cref="IRpcTransport"/>, named so a script can ask for it.
	/// </summary>
	/// <remarks>
	/// Registered implementations are the whole list of transports available on the current platform,
	/// the same way registered database connectors define <c>db.connectors()</c>.
	/// </remarks>
	public interface IRpcTransportProvider
	{
		/// <summary>
		/// Gets the name used to request this transport, e.g. <c>stdio</c>.
		/// </summary>
		string Name { get; }

		/// <summary>
		/// Opens a transport.
		/// </summary>
		/// <param name="request">The target and the options of the transport.</param>
		/// <param name="cancellationToken">The token to cancel the operation.</param>
		/// <returns>The connected transport.</returns>
		Task<IRpcTransport> ConnectAsync(RpcTransportRequest request, CancellationToken cancellationToken = default);
	}
}
