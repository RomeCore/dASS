using System.Text;
using AsyncLua;
using AsyncLua.Values;
using LLMDesktopAssistant.Desktop.Execution;
using LLMDesktopAssistant.Utils.Attachments;

namespace LLMDesktopAssistant.Desktop.Scripting.Lua
{
	/// <summary>
	/// A live interactive terminal exposed to Lua as the <c>terminal</c> userdata returned by
	/// <c>process.run_terminal()</c>.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The handle owns nothing it did not launch: it wraps the <see cref="ProcessDescriptor"/> created by the
	/// launcher. The process dies when the descriptor's cancellation token is cancelled — which is what the
	/// terminal view's Cancel button and <see cref="Kill"/> both do, and what disposing the attached
	/// view model does.
	/// </para>
	/// <para>
	/// The handle is a marker <see cref="IChatAttachment"/>: it proves to the Lua API that it can be
	/// attached, and the desktop attachment handler turns it into a terminal view model.
	/// </para>
	/// </remarks>
	public sealed class LuaTerminal : IChatAttachment
	{
		private static readonly UTF8Encoding _utf8 = new(encoderShouldEmitUTF8Identifier: false);
		private readonly SemaphoreSlim _writeLock = new(1, 1);

		/// <summary>
		/// Initializes a new handle over a launched terminal process.
		/// </summary>
		/// <param name="descriptor">The descriptor produced by the launcher.</param>
		public LuaTerminal(ProcessDescriptor descriptor)
		{
			Descriptor = descriptor;
			_ = TrackExitAsync();
		}

		private int? _exitCode;

		/// <summary>
		/// Caches the exit code as soon as the process task completes, so <see cref="ExitCode"/> and
		/// <see cref="IsRunning"/> are accurate even when the script never calls <see cref="Wait"/>.
		/// </summary>
		private async Task TrackExitAsync()
		{
			try
			{
				_exitCode = await Descriptor.ExitCodeTask.ConfigureAwait(false);
			}
			catch (Exception)
			{
				_exitCode = -1;
			}
		}

		/// <summary>
		/// The descriptor produced by the launcher. Hidden from Lua.
		/// </summary>
		[LuaHidden]
		public ProcessDescriptor Descriptor { get; }

		/// <summary>
		/// Wraps the handle into a <c>terminal</c> userdata.
		/// </summary>
		[LuaHidden]
		public static LuaUserData Wrap(LuaTerminal terminal)
			=> new(terminal, "terminal")
			{
				Metatable = UserDataMetatableGenerator.GetOrCreate(typeof(LuaTerminal))
			};

		/// <summary>
		/// The OS process id of the terminal process.
		/// </summary>
		public int Pid => Descriptor.ProcessId;

		/// <summary>
		/// Whether the process is still running.
		/// </summary>
		public bool IsRunning => _exitCode is null && Descriptor.IsRunning;

		/// <summary>
		/// The exit code, or <see langword="null"/> while the process is still running.
		/// </summary>
		public int? ExitCode => _exitCode ?? (Descriptor.IsRunning ? null : Descriptor.ExitCode);

		/// <summary>
		/// Returns the terminal buffer contents (scrollback plus the visible screen).
		/// </summary>
		/// <returns>The buffer as text.</returns>
		public string Text() => Descriptor.TerminalSession?.Output ?? string.Empty;

		/// <summary>
		/// Writes raw text into the terminal standard input. Escape sequences are sent as is.
		/// </summary>
		/// <param name="text">The text to write.</param>
		/// <param name="cancellationToken">The cancellation token of the running script (injected by the Lua runtime).</param>
		public async Task Write(string? text, CancellationToken cancellationToken)
		{
			var stream = Descriptor.TerminalSession?.Pty.WriterStream;
			if (stream is null || string.IsNullOrEmpty(text))
				return;

			await _writeLock.WaitAsync(cancellationToken).ConfigureAwait(false);
			try
			{
				var bytes = _utf8.GetBytes(text);
				await stream.WriteAsync(bytes, 0, bytes.Length, cancellationToken).ConfigureAwait(false);
				await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
			}
			catch (OperationCanceledException)
			{
				throw;
			}
			catch (Exception)
			{
				// The process may have exited between the check and the write; nothing to report.
			}
			finally
			{
				_writeLock.Release();
			}
		}

		/// <summary>
		/// Writes a line into the terminal standard input (the text followed by a carriage return).
		/// </summary>
		/// <param name="text">The line to write.</param>
		/// <param name="cancellationToken">The cancellation token of the running script (injected by the Lua runtime).</param>
		public Task WriteLine(string? text, CancellationToken cancellationToken)
			=> Write((text ?? string.Empty) + "\r", cancellationToken);

		/// <summary>
		/// Kills the process behind this terminal.
		/// </summary>
		public void Kill()
		{
			try
			{
				Descriptor.CancellationTokenSource.Cancel();
			}
			catch (Exception)
			{
				// Nothing to do: the process is already gone.
			}
		}

		/// <summary>
		/// Waits for the process to exit and returns its exit code.
		/// </summary>
		/// <param name="cancellationToken">
		/// The cancellation token of the running script (injected by the Lua runtime). When it trips, the wait
		/// is abandoned with <see cref="OperationCanceledException"/> instead of holding the script forever.
		/// </param>
		/// <returns>
		/// The exit code, or <c>-1</c> when the process was killed or never started.
		/// Every call on the same handle returns the same value.
		/// </returns>
		public async Task<int> Wait(CancellationToken cancellationToken)
		{
			cancellationToken.ThrowIfCancellationRequested();

			if (_exitCode is { } known)
				return known;

			try
			{
				_exitCode = await Descriptor.ExitCodeTask.WaitAsync(cancellationToken).ConfigureAwait(false);
			}
			catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
			{
				// The script itself is being cancelled (Stop, chat closed): surface it, so the command
				// settles as cancelled instead of inventing an exit code.
				throw;
			}
			catch (Exception)
			{
				// The process was killed: that is a real outcome, not a cancellation of the script.
				_exitCode = -1;
			}

			return _exitCode.Value;
		}
	}
}
