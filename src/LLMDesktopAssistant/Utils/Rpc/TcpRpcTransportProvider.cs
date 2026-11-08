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

			var (host, port) = ParseTarget(request.Target);

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

		/// <summary>
		/// Splits <c>host:port</c>, keeping a bracketed IPv6 literal intact.
		/// </summary>
		/// <param name="target">The target to split.</param>
		/// <returns>The host and the port.</returns>
		private static (string Host, int Port) ParseTarget(string target)
		{
			var separator = target.LastIndexOf(':');

			if (separator <= 0 || separator == target.Length - 1)
				throw new FormatException($"The tcp target '{target}' is not host:port.");

			var host = target[..separator];
			var portText = target[(separator + 1)..];

			if (host.StartsWith('[') && host.EndsWith(']'))
				host = host[1..^1];

			if (!int.TryParse(portText, out var port) || port is < 0 or > 65535)
				throw new FormatException($"The tcp target '{target}' has no valid port.");

			return (host, port);
		}
	}
}
