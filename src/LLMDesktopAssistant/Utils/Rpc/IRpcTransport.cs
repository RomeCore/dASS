namespace LLMDesktopAssistant.Utils.Rpc
{
	/// <summary>
	/// A live bidirectional byte channel to a JSON-RPC peer.
	/// </summary>
	/// <remarks>
	/// The transport owns whatever keeps the channel open (a child process, a socket) and nothing about
	/// the protocol: framing, ids and routing live above it, so a new transport costs a provider and no
	/// protocol code.
	/// </remarks>
	public interface IRpcTransport : IAsyncDisposable
	{
		/// <summary>
		/// Gets the stream the peer writes to.
		/// </summary>
		Stream Input { get; }

		/// <summary>
		/// Gets the stream the peer reads from.
		/// </summary>
		Stream Output { get; }

		/// <summary>
		/// Gets the stream the peer reports problems on, when the transport has one.
		/// </summary>
		/// <remarks>
		/// Nothing about the protocol depends on this stream, but it must be drained: a peer that fills
		/// its diagnostic pipe blocks instead of answering.
		/// </remarks>
		Stream? StandardError { get; }

		/// <summary>
		/// Gets the operating system process id behind the transport, when there is one.
		/// </summary>
		int? ProcessId { get; }

		/// <summary>
		/// Gets the process exit code, or <see langword="null"/> when the process is still running or
		/// the transport has no process.
		/// </summary>
		int? ExitCode { get; }

		/// <summary>
		/// Completes when the transport ends.
		/// </summary>
		/// <param name="cancellationToken">The token to cancel the wait.</param>
		/// <returns>The process exit code, or <c>0</c> for a transport without a process.</returns>
		Task<int> WaitForExitAsync(CancellationToken cancellationToken = default);

		/// <summary>
		/// Ends the transport at once, including the processes it spawned.
		/// </summary>
		void Kill();
	}
}
