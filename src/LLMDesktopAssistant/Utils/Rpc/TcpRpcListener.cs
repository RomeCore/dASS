using System.Net;
using System.Net.Sockets;

namespace LLMDesktopAssistant.Utils.Rpc
{
	/// <summary>
	/// Accepts JSON-RPC connections on a bound TCP socket.
	/// </summary>
	/// <remarks>
	/// Each accepted socket becomes an ordinary <see cref="TcpRpcTransport"/>, so framing, identifiers,
	/// routing and errors are the same code the outbound side runs. Stopping the listener is also what
	/// wakes a pending accept: there is no such thing as accepting on a stopped socket.
	/// </remarks>
	internal sealed class TcpRpcListener(TcpListener listener) : IRpcListener
	{
		private int _stopped;

		/// <inheritdoc/>
		public string Address => (listener.LocalEndpoint as IPEndPoint)?.Address.ToString() ?? string.Empty;

		/// <inheritdoc/>
		public int Port => (listener.LocalEndpoint as IPEndPoint)?.Port ?? 0;

		/// <inheritdoc/>
		public bool IsListening => _stopped == 0;

		/// <inheritdoc/>
		public async Task<IRpcTransport> AcceptAsync(CancellationToken cancellationToken = default)
		{
			if (_stopped != 0)
				throw new InvalidOperationException("The listener is stopped.");

			var client = await listener.AcceptTcpClientAsync(cancellationToken).ConfigureAwait(false);
			client.NoDelay = true;

			return new TcpRpcTransport(client);
		}

		/// <inheritdoc/>
		public void Stop()
		{
			if (Interlocked.Exchange(ref _stopped, 1) != 0)
				return;

			listener.Stop();
		}
	}
}
