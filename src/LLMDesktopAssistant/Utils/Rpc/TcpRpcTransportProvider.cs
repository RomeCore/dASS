using System.Net.Sockets;
using LLMDesktopAssistant.Services;

namespace LLMDesktopAssistant.Utils.Rpc
{
	/// <summary>
	/// Connects to a JSON-RPC peer listening on a TCP socket: the <c>tcp</c> transport.
	/// </summary>
	/// <remarks>
	/// The target is <c>host:port</c>, with an IPv6 literal in brackets (<c>[::1]:9333</c>). No process is
	/// started, so the handle reports no pid, no exit code and no standard error, and closing the handle
	/// closes the socket rather than killing anything.
	/// </remarks>
	[Service(typeof(IRpcTransportProvider))]
	public sealed class TcpRpcTransportProvider : IRpcTransportProvider
	{
		/// <inheritdoc/>
		public string Name => "tcp";

		/// <inheritdoc/>
		public async Task<IRpcTransport> ConnectAsync(
			RpcTransportRequest request,
			CancellationToken cancellationToken = default)
		{
			if (request.Arguments.Count > 0)
			{
				throw new ArgumentException(
					"The tcp transport takes its whole target as host:port and starts no process, so it accepts no arguments.");
			}

			var (host, port) = RpcEndpoint.Split(request.Target);

			var client = new TcpClient { NoDelay = true };

			try
			{
				await client.ConnectAsync(host, port, cancellationToken).ConfigureAwait(false);
			}
			catch
			{
				client.Dispose();
				throw;
			}

			return new TcpRpcTransport(client);
		}
	}
}
