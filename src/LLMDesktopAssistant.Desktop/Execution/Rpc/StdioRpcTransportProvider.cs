using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using LLMDesktopAssistant.Services;
using LLMDesktopAssistant.Utils.Rpc;

namespace LLMDesktopAssistant.Desktop.Execution.Rpc
{
	/// <summary>
	/// Opens a JSON-RPC channel over the standard streams of a process it starts: the <c>stdio</c> transport.
	/// </summary>
	/// <remarks>
	/// The peer's own process is not the application's business beyond its lifetime, so this provider only
	/// starts it; stderr draining, framing and routing all live above the transport.
	/// </remarks>
	[Service(typeof(IRpcTransportProvider))]
	public sealed class StdioRpcTransportProvider : IRpcTransportProvider
	{
		/// <inheritdoc/>
		public string Name => "stdio";

		/// <inheritdoc/>
		public Task<IRpcTransport> ConnectAsync(
			RpcTransportRequest request,
			CancellationToken cancellationToken = default)
		{
			cancellationToken.ThrowIfCancellationRequested();

			var startInfo = new ProcessStartInfo
			{
				FileName = request.Target,
				WorkingDirectory = request.WorkingDirectory ?? Directory.GetCurrentDirectory(),
				RedirectStandardInput = true,
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				UseShellExecute = false,
				CreateNoWindow = true,
				StandardOutputEncoding = Encoding.UTF8,
				StandardErrorEncoding = Encoding.UTF8
			};

			if (!string.IsNullOrEmpty(request.CommandLine))
			{
				// The caller already wrote the whole argument string: hand it over as is, so the quoting it
				// wrote is what the peer parses.
				startInfo.Arguments = request.CommandLine;
			}
			else
			{
				foreach (var argument in request.Arguments)
					startInfo.ArgumentList.Add(argument);
			}

			foreach (var (name, value) in request.Environment)
			{
				if (value is null)
					startInfo.Environment.Remove(name);
				else
					startInfo.Environment[name] = value;
			}

			var process = new Process { StartInfo = startInfo };

			if (!process.Start())
			{
				process.Dispose();
				throw new InvalidOperationException($"The process '{request.Target}' did not start.");
			}

			return Task.FromResult<IRpcTransport>(new StdioRpcTransport(process));
		}
	}
}
