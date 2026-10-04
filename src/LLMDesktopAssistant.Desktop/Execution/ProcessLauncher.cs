using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using LLMDesktopAssistant.Services;
using LLMDesktopAssistant.Utils;
using Porta.Pty;
using Serilog;
using XTerm;
using XTerm.Options;

namespace LLMDesktopAssistant.Desktop.Execution
{
	[Service(typeof(IProcessLauncher))]
	public class ProcessLauncher : IProcessLauncher
	{
		private readonly IProcessDispatcher _dispatcher;

		public ProcessLauncher(IProcessDispatcher dispatcher)
		{
			_dispatcher = dispatcher;
		}

		public ProcessDescriptor Launch(ProcessLaunchParameters parameters, CancellationToken cancellationToken = default)
		{
			if (parameters.RunInTerminal)
				return LaunchTerminal(parameters, cancellationToken);
			else
				return LaunchNonTerminal(parameters, cancellationToken);
		}

		private ProcessDescriptor LaunchNonTerminal(ProcessLaunchParameters parameters, CancellationToken cancellationToken)
		{
			var encoding = Encoding.UTF8;

			var stdinAvailable = !string.IsNullOrEmpty(parameters.StdIn);
			var psi = new ProcessStartInfo
			{
				FileName = parameters.FileName,
				WorkingDirectory = parameters.WorkingDirectory,
				CreateNoWindow = true,
				UseShellExecute = false,
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				RedirectStandardInput = stdinAvailable,
				StandardOutputEncoding = encoding,
				StandardErrorEncoding = encoding,
				StandardInputEncoding = stdinAvailable ? encoding : null,
			};
			if (parameters.VerbatimArguments)
				psi.Arguments = string.Join(" ", parameters.Arguments);
			else
				foreach (var arg in parameters.Arguments)
					psi.ArgumentList.Add(arg);
			foreach (var envVar in parameters.EnvironmentVariables)
				psi.Environment.Add(envVar);

			var process = Process.Start(psi) ?? throw new InvalidOperationException("Failed to start process.");

			var tcs = new TaskCompletionSource<int>();
			var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
			if (parameters.TimeOut is { } timeout && timeout > TimeSpan.Zero)
				cts.CancelAfter(timeout);
			cancellationToken = cts.Token;

			var output = new ProcessOutput();
			var descriptor = new ProcessDescriptor
			{
				Id = Guid.NewGuid(),
				ProcessId = process.Id,
				LaunchParameters = parameters,
				ExitCodeTask = tcs.Task,
				CancellationTokenSource = cts,

				Status = ProcessStatus.Running,
				IsRunning = true,

				PlainOutput = output,
				TerminalSession = null
			};

			_dispatcher.OnProcessStart(descriptor);

			Task.Run(async () =>
			{
				async Task ProcessOutputAsync(StreamReader stream, RangeObservableCollection<string> lines, CancellationToken ct)
				{
					string? line;
					while ((line = await stream.ReadLineAsync(ct)) is not null)
					{
						lines.Add(line);
						output.Output.Add(line); // RangeObservableCollection is thread-safe
					}
				}

				try
				{
					using var killReg = cancellationToken.Register(() =>
					{
						try
						{
							process.Kill(entireProcessTree: true);
						}
						catch (Exception ex)
						{
							Log.Error(ex, "Failed to kill process: {Error}", ex.Message);
						}
					});

					cancellationToken.ThrowIfCancellationRequested();

					if (stdinAvailable)
					{
						await process.StandardInput.WriteLineAsync(parameters.StdIn);
						process.StandardInput.Close();
					}

					var stdoutTask = ProcessOutputAsync(process.StandardOutput, output.StdOut, cancellationToken);
					var stderrTask = ProcessOutputAsync(process.StandardError, output.StdErr, cancellationToken);
					var exitTask = process.WaitForExitAsync(cancellationToken);

					await Task.WhenAll(stdoutTask, stderrTask, exitTask);
					tcs.SetResult(process.ExitCode);
					descriptor.ExitCode = process.ExitCode;
					descriptor.Status = process.ExitCode == 0 ? ProcessStatus.Success : ProcessStatus.Failed;
				}
				catch (Exception) when (cancellationToken.IsCancellationRequested)
				{
					tcs.SetCanceled(cancellationToken);
					descriptor.Status = ProcessStatus.Cancelled;
				}
				catch (Exception ex)
				{
					tcs.SetException(ex);
					descriptor.Exception = ex;
					descriptor.Status = ProcessStatus.Failed;
				}
				finally
				{
					_dispatcher.OnProcessEnd(descriptor);
					descriptor.IsRunning = false;
					process.Dispose();
				}
			}, CancellationToken.None);

			return descriptor;
		}

		private ProcessDescriptor LaunchTerminal(ProcessLaunchParameters parameters, CancellationToken cancellationToken)
		{
			var terminal = new Terminal(new TerminalOptions
			{
				ConvertEol = !RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
			});

			var ptyOptions = new PtyOptions
			{
				Name = parameters.FileName,
				App = parameters.FileName,
				Cols = terminal.Cols,
				Rows = terminal.Rows,
				Cwd = parameters.WorkingDirectory,
				// Porta.Pty's own escaping (VerbatimCommandLine = false) only implements cmd.exe-style
				// quoting, which corrupts embedded quotes, drops empty arguments and mangles trailing
				// backslashes for regular CRT-parsed processes. We therefore always build the command line
				// ourselves and hand it over verbatim; see BuildTerminalArguments.
				VerbatimCommandLine = true
			};
			if (parameters.Arguments.Count > 0)
				ptyOptions.CommandLine = BuildTerminalArguments(parameters);
			if (parameters.EnvironmentVariables.Count > 0)
			{
				var environment = new Dictionary<string, string>();
				foreach (var (key, value) in parameters.EnvironmentVariables)
				{
					if (value is not null)
						environment[key] = value;
				}
				ptyOptions.Environment = environment;
			}

			var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
			if (parameters.TimeOut is { } timeout && timeout > TimeSpan.Zero)
				cts.CancelAfter(timeout);

			IPtyConnection pty;
			try
			{
				pty = PtyProvider.SpawnAsync(ptyOptions, cts.Token).GetAwaiter().GetResult();
			}
			catch
			{
				cts.Dispose();
				throw;
			}

			var session = new ProcessTerminalSession
			{
				Pty = pty,
				Terminal = terminal
			};

			var tcs = new TaskCompletionSource<int>();
			var descriptor = new ProcessDescriptor
			{
				Id = Guid.NewGuid(),
				ProcessId = pty.Pid,
				LaunchParameters = parameters,
				ExitCodeTask = tcs.Task,
				CancellationTokenSource = cts,

				Status = ProcessStatus.Running,
				IsRunning = true,

				PlainOutput = null,
				TerminalSession = session
			};

			_dispatcher.OnProcessStart(descriptor);

			Task.Run(async () =>
			{
				var exitHandled = 0;
				void Complete(int exitCode)
				{
					if (Interlocked.Exchange(ref exitHandled, 1) != 0)
						return;

					descriptor.ExitCode = exitCode;
					descriptor.Status = exitCode == 0 ? ProcessStatus.Success : ProcessStatus.Failed;
					tcs.TrySetResult(exitCode);
				}
				async void OnPtyExited(object? sender, PtyExitedEventArgs e)
				{
					// Wait a bit to ensure that all output has been readed.
					// This is because pty.ProcessExited is fired BEFORE all output has been readed.
					await Task.Delay(500);
					Complete(e.ExitCode);
				}

				try
				{
					pty.ProcessExited += OnPtyExited;

					// The process might have exited before the event subscription — check for it.
					if (pty.WaitForExit(0))
						Complete(pty.ExitCode);

					using var killReg = cts.Token.Register(() =>
					{
						try
						{
							pty.Kill();
						}
						catch (Exception ex)
						{
							Log.Error(ex, "Failed to kill terminal process: {Error}", ex.Message);
						}
					});

					// Pump PTY output into the terminal emulation and the session output collection.
					// A streaming decoder preserves multi-byte sequences split across reads,
					// unlike per-chunk Encoding.UTF8.GetString which would garble them.
					var buffer = new byte[0x4000];
					var decoder = Encoding.UTF8.GetDecoder();
					var chars = new char[Encoding.UTF8.GetMaxCharCount(buffer.Length)];
					while (true)
					{
						int bytesRead;
						try
						{
							bytesRead = await pty.ReaderStream.ReadAsync(buffer, cts.Token);
						}
						catch (OperationCanceledException)
						{
							break;
						}

						if (bytesRead == 0)
						{
							// Flush any bytes left in the middle of a sequence at EOF.
							var trailingChars = decoder.GetChars(buffer, 0, 0, chars, 0, flush: true);
							if (trailingChars > 0)
							{
								terminal.Write(new string(chars, 0, trailingChars));
								session.RaiseOutputUpdated();
							}
							break;
						}

						var charCount = decoder.GetChars(buffer, 0, bytesRead, chars, 0, flush: false);
						if (charCount == 0)
							continue;

						terminal.Write(new string(chars, 0, charCount));

						// Keep the viewport pinned to the bottom unless a full-screen app
						// (alternate buffer) is controlling the cursor itself.
						if (terminal.ActiveBuffer != XTerm.Common.BufferType.Alternate)
							terminal.Buffer.ScrollToBottom();

						// Notify views so they can repaint — XTerm.Terminal.BufferChanged
						// only fires on buffer switches, not on writes.
						session.RaiseOutputUpdated();
					}

					if (cts.IsCancellationRequested)
					{
						tcs.TrySetCanceled(cts.Token);
						descriptor.Status = ProcessStatus.Cancelled;
					}
					else
					{
						Complete(pty.ExitCode);
					}
				}
				catch (Exception) when (cts.IsCancellationRequested)
				{
					tcs.TrySetCanceled(cts.Token);
					descriptor.Status = ProcessStatus.Cancelled;
				}
				catch (Exception ex)
				{
					tcs.TrySetException(ex);
					descriptor.Exception = ex;
					descriptor.Status = ProcessStatus.Failed;
				}
				finally
				{
					pty.ProcessExited -= OnPtyExited;
					_dispatcher.OnProcessEnd(descriptor);
					descriptor.IsRunning = false;
					try
					{
						pty.Dispose();
					}
					catch (Exception ex)
					{
						Log.Error(ex, "Failed to dispose terminal process: {Error}", ex.Message);
					}
				}
			}, CancellationToken.None);

			return descriptor;
		}

		/// <summary>
		/// Builds the raw <see cref="PtyOptions.CommandLine"/> array for the terminal path.
		/// On Windows the array is joined by Porta.Pty into a single CreateProcess command line, so
		/// ordinary (non-verbatim) arguments must be escaped first. On Unix the array is passed straight
		/// to execvp, so the arguments must stay untouched regardless of the flag.
		/// </summary>
		private static string[] BuildTerminalArguments(ProcessLaunchParameters parameters)
		{
			if (parameters.VerbatimArguments || !RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
				return [.. parameters.Arguments];

			var escaped = new string[parameters.Arguments.Count];
			for (var i = 0; i < escaped.Length; i++)
				escaped[i] = CommandLineEscaping.EscapeArgument(parameters.Arguments[i]);
			return escaped;
		}
	}
}
