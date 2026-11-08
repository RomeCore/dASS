using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using LLMDesktopAssistant.Utils.Rpc;
using Serilog;

namespace LLMDesktopAssistant.Desktop.Execution.Rpc
{
	/// <summary>
	/// A JSON-RPC channel over the standard streams of a child process.
	/// </summary>
	/// <remarks>
	/// Deliberately not built on <see cref="IProcessLauncher"/>: that launcher runs a process to
	/// completion and buffers its output, while an RPC peer must stay alive and stay readable while it
	/// runs. The kill reaches the whole process tree, because a language server started through a wrapper
	/// script leaves the real server behind otherwise.
	/// </remarks>
	internal sealed class StdioRpcTransport(Process process) : IRpcTransport
	{
		/// <inheritdoc/>
		public Stream Input => process.StandardOutput.BaseStream;

		/// <inheritdoc/>
		public Stream Output => process.StandardInput.BaseStream;

		/// <inheritdoc/>
		public Stream? StandardError => process.StandardError.BaseStream;

		/// <inheritdoc/>
		public int? ProcessId
		{
			get
			{
				try
				{
					return process.HasExited ? null : process.Id;
				}
				catch (InvalidOperationException)
				{
					return null;
				}
			}
		}

		/// <inheritdoc/>
		public int? ExitCode
		{
			get
			{
				try
				{
					return process.HasExited ? process.ExitCode : null;
				}
				catch (InvalidOperationException)
				{
					return null;
				}
			}
		}

		/// <inheritdoc/>
		public async Task<int> WaitForExitAsync(CancellationToken cancellationToken = default)
		{
			await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
			return process.ExitCode;
		}

		/// <inheritdoc/>
		public void Kill()
		{
			try
			{
				if (!process.HasExited)
					process.Kill(entireProcessTree: true);
			}
			catch (InvalidOperationException ex)
			{
				Log.Debug(ex, "An RPC process was already gone when it was killed.");
			}
			catch (Exception ex)
			{
				Log.Warning(ex, "An RPC process could not be killed.");
			}
		}

		/// <inheritdoc/>
		public ValueTask DisposeAsync()
		{
			process.Dispose();
			return ValueTask.CompletedTask;
		}
	}
}
