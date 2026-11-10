namespace LLMDesktopAssistant.Utils.Rpc
{
	/// <summary>
	/// A bound endpoint that turns each inbound connection into an <see cref="IRpcTransport"/>.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Deliberately not an <see cref="IRpcTransport"/>: a listener has no peer until one connects, and it
	/// outlives every connection it accepts. Everything above the transport - framing, identifiers,
	/// routing, errors - is the same code the outbound side runs.
	/// </para>
	/// <para>
	/// Binding and releasing a socket are synchronous, so only <see cref="AcceptAsync"/> is a task. That
	/// is what lets a script close a listener from a synchronous cleanup block.
	/// </para>
	/// </remarks>
	public interface IRpcListener
	{
		/// <summary>
		/// Gets the address the listener is bound to.
		/// </summary>
		string Address { get; }

		/// <summary>
		/// Gets the port the listener is bound to, which is the real one when port <c>0</c> was requested.
		/// </summary>
		int Port { get; }

		/// <summary>
		/// Gets a value indicating whether the listener still accepts connections.
		/// </summary>
		bool IsListening { get; }

		/// <summary>
		/// Waits for the next inbound connection.
		/// </summary>
		/// <param name="cancellationToken">The token to cancel the wait.</param>
		/// <returns>The connected transport, which the caller owns.</returns>
		Task<IRpcTransport> AcceptAsync(CancellationToken cancellationToken = default);

		/// <summary>
		/// Stops listening, releases the port and wakes a pending accept.
		/// </summary>
		void Stop();
	}
}
