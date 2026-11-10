namespace LLMDesktopAssistant.Utils.Rpc
{
	/// <summary>
	/// Binds one kind of <see cref="IRpcListener"/>, named so a script can ask for it.
	/// </summary>
	/// <remarks>
	/// Registered implementations are the whole list of endpoints this platform can listen on, the same
	/// way <see cref="IRpcTransportProvider"/> implementations are the list it can connect to. The two
	/// lists are separate because connecting and listening are separate permissions.
	/// </remarks>
	public interface IRpcListenerProvider
	{
		/// <summary>
		/// Gets the name used to request this listener, e.g. <c>tcp</c>.
		/// </summary>
		string Name { get; }

		/// <summary>
		/// Binds a listener.
		/// </summary>
		/// <param name="request">The endpoint and the backlog.</param>
		/// <returns>The bound listener.</returns>
		/// <remarks>
		/// Synchronous on purpose: binding a socket is, and a caller that only has a synchronous cleanup
		/// block must still be able to stop one.
		/// </remarks>
		IRpcListener Listen(RpcListenRequest request);
	}
}
