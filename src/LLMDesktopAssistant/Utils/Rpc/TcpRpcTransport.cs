using System.Net.Sockets;

namespace LLMDesktopAssistant.Utils.Rpc
{
	/// <summary>
	/// A JSON-RPC channel over a connected TCP socket.
	/// </summary>
	/// <remarks>
	/// Read and write share one duplex stream, so the transport exposes the same instance twice. A socket
	/// has no exit code, so ending it is the only lifecycle event there is.
	/// </remarks>
	internal sealed class TcpRpcTransport : IRpcTransport
	{
		private readonly TcpClient _client;
		private readonly NetworkStream _stream;

		/// <summary>
		/// Initializes a transport over a connected client.
		/// </summary>
		/// <param name="client">The connected socket.</param>
		public TcpRpcTransport(TcpClient client)
		{
			_client = client;
			_stream = client.GetStream();
		}

		/// <inheritdoc/>
		public Stream Input => _stream;

		/// <inheritdoc/>
		public Stream Output => _stream;

		/// <inheritdoc/>
		public Stream? StandardError => null;

		/// <inheritdoc/>
		public int? ProcessId => null;

		/// <inheritdoc/>
		public int? ExitCode => null;

		/// <inheritdoc/>
		/// <remarks>
		/// A socket has nothing to await: it either is open or is not, so this reports <c>0</c> at once.
		/// </remarks>
		public Task<int> WaitForExitAsync(CancellationToken cancellationToken = default) => Task.FromResult(0);

		/// <inheritdoc/>
		public void Kill() => _client.Close();

		/// <inheritdoc/>
		public ValueTask DisposeAsync()
		{
			_client.Dispose();
			return ValueTask.CompletedTask;
		}
	}
}
