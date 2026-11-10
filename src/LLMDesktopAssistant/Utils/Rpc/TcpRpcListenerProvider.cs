using System.Net.Sockets;
using LLMDesktopAssistant.Services;

namespace LLMDesktopAssistant.Utils.Rpc
{
	/// <summary>
	/// Listens for JSON-RPC peers on a TCP socket: the <c>tcp</c> listener.
	/// </summary>
	/// <remarks>
	/// The target is <c>host:port</c> like the client transport, plus <c>*</c> for every IPv4 interface
	/// and a port of <c>0</c> for an ephemeral one. No process is involved, so a connection has no pid,
	/// no exit code and no standard error.
	/// </remarks>
	[Service(typeof(IRpcListenerProvider))]
	public sealed class TcpRpcListenerProvider : IRpcListenerProvider
	{
		/// <inheritdoc/>
		public string Name => "tcp";

		/// <inheritdoc/>
		public IRpcListener Listen(RpcListenRequest request)
		{
			var (host, port) = RpcEndpoint.Split(request.Target);
			var address = RpcEndpoint.Resolve(host);

			var listener = new TcpListener(address, port);

			try
			{
				if (request.Backlog > 0)
					listener.Start(request.Backlog);
				else
					listener.Start();
			}
			catch
			{
				listener.Stop();
				throw;
			}

			return new TcpRpcListener(listener);
		}
	}
}
