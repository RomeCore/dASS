using System.Net;
using System.Net.Sockets;

namespace LLMDesktopAssistant.Utils.Rpc
{
	/// <summary>
	/// The <c>host:port</c> notation shared by the TCP transport and the TCP listener.
	/// </summary>
	internal static class RpcEndpoint
	{
		/// <summary>
		/// Splits <c>host:port</c>, keeping a bracketed IPv6 literal intact.
		/// </summary>
		/// <param name="target">The target to split.</param>
		/// <returns>The host and the port.</returns>
		public static (string Host, int Port) Split(string target)
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

		/// <summary>
		/// Resolves a host to the address a listener binds to.
		/// </summary>
		/// <param name="host">A literal address, <c>*</c> for every IPv4 interface, or a host name.</param>
		/// <returns>The address to bind.</returns>
		/// <remarks>
		/// A listener needs one concrete address up front, so a name is resolved here instead of by the
		/// socket. The lookup is synchronous: a literal address - the normal case - never reaches it, and
		/// a name only costs the caller a block before the bind.
		/// </remarks>
		public static IPAddress Resolve(string host)
		{
			if (host is "*" or "0.0.0.0")
				return IPAddress.Any;

			if (host is "::" or "[::]")
				return IPAddress.IPv6Any;

			if (string.Equals(host, "localhost", StringComparison.OrdinalIgnoreCase))
				return IPAddress.Loopback;

			if (IPAddress.TryParse(host, out var address))
				return address;

			var addresses = Dns.GetHostAddresses(host);

			var preferred = addresses.FirstOrDefault(candidate => candidate.AddressFamily == AddressFamily.InterNetwork)
				?? addresses.FirstOrDefault();

			return preferred
				?? throw new FormatException($"The host '{host}' does not resolve to an address.");
		}
	}
}
