using System.Diagnostics;
using System.IO;
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
	/// launcher. The process dies when the descriptor's cancellation token is cancelled - which is what the
	/// terminal view's Cancel button and <see cref="Kill"/> both do, and what disposing the attached
	/// view model does.
	/// </para>
	/// <para>
	/// The handle is a marker <see cref="IChatAttachment"/>: it proves to the Lua API that it can be
	/// attached, and the desktop attachment handler turns it into a terminal view model.
	/// </para>
	/// <para>
	/// Unlike the pipe handle of <c>process.exec</c>, there are no streams here: a PTY merges them and emulates
	/// a screen, so <see cref="Text"/> is the rendered buffer - escaped sequences already interpreted and
	/// repaints already applied. That is why a read returns the whole screen and not a delta.
	/// </para>
	/// </remarks>
	public sealed class LuaTerminal : IChatAttachment
	{
		private static readonly UTF8Encoding _utf8 = new(encoderShouldEmitUTF8Identifier: false);

		/// <summary>
		/// The key sequences <see cref="SendKey"/> understands, keyed by their normalised name.
		/// </summary>
		private static readonly Dictionary<string, string> _namedKeys = new(StringComparer.Ordinal)
		{
			["enter"] = "\r",
			["return"] = "\r",
			["tab"] = "\t",
			["escape"] = "\x1b",
			["esc"] = "\x1b",
			["space"] = " ",
			["backspace"] = "\x7f",
			["delete"] = "\x1b[3~",
			["del"] = "\x1b[3~",
			["insert"] = "\x1b[2~",
			["up"] = "\x1b[A",
			["down"] = "\x1b[B",
			["right"] = "\x1b[C",
			["left"] = "\x1b[D",
			["home"] = "\x1b[H",
			["end"] = "\x1b[F",
			["pageup"] = "\x1b[5~",
			["pagedown"] = "\x1b[6~",
			["f1"] = "\x1bOP",
			["f2"] = "\x1bOQ",
			["f3"] = "\x1bOR",
			["f4"] = "\x1bOS",
			["f5"] = "\x1b[15~",
			["f6"] = "\x1b[17~",
			["f7"] = "\x1b[18~",
			["f8"] = "\x1b[19~",
			["f9"] = "\x1b[20~",
			["f10"] = "\x1b[21~",
			["f11"] = "\x1b[23~",
			["f12"] = "\x1b[24~"
		};

		private readonly Lock _writeLock = new();
		private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
		private readonly Task _exited;

		private TaskCompletionSource _pulse = NewPulse();
		private string _lastText = string.Empty;
		private int _timedOut;
		private int _unsubscribed;
		private long _durationMs;

		/// <summary>
		/// Initializes a new handle over a launched terminal process.
		/// </summary>
		/// <param name="descriptor">The descriptor produced by the launcher.</param>
		/// <param name="timeoutSeconds">The wall-clock limit of the run, or <see langword="null"/> for none.</param>
		public LuaTerminal(ProcessDescriptor descriptor, int? timeoutSeconds = null)
		{
			Descriptor = descriptor;
			// A non-faulting view of the exit: WhenAny must never surface a cancelled or failed handler.
			_exited = descriptor.ExitCodeTask.ContinueWith(static _ => { }, TaskScheduler.Default);

			if (descriptor.TerminalSession is { } session)
				session.OutputUpdated += OnOutputUpdated;

			_ = TrackExitAsync();

			if (timeoutSeconds is > 0)
				_ = EnforceTimeoutAsync(TimeSpan.FromSeconds(timeoutSeconds.Value));
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
		/// The lifecycle state: <c>running</c>, <c>success</c>, <c>failed</c>, <c>cancelled</c> or <c>timed_out</c>.
		/// </summary>
		public string Status
		{
			get
			{
				var exit = Descriptor.ExitCodeTask;
				if (!exit.IsCompleted)
					return "running";
				if (Volatile.Read(ref _timedOut) == 1)
					return "timed_out";
				if (exit.IsCanceled)
					return "cancelled";
				if (exit.IsFaulted)
					return "failed";
				return exit.Result == 0 ? "success" : "failed";
			}
		}

		/// <summary>
		/// The exit code, or <see langword="null"/> while the process runs. <c>-1</c> when it was killed.
		/// </summary>
		public int? ExitCode
		{
			get
			{
				var exit = Descriptor.ExitCodeTask;
				if (!exit.IsCompleted)
					return null;
				return exit.Status == TaskStatus.RanToCompletion ? exit.Result : -1;
			}
		}

		/// <summary>
		/// Whether the process is still running.
		/// </summary>
		public bool IsRunning => !Descriptor.ExitCodeTask.IsCompleted;

		/// <summary>
		/// Whether the process was killed by its own timeout rather than by a caller or the script's token.
		/// </summary>
		public bool TimedOut => Volatile.Read(ref _timedOut) == 1;

		/// <summary>
		/// The lifetime in milliseconds, or <see langword="null"/> while the process runs.
		/// </summary>
		public int? DurationMs => Descriptor.ExitCodeTask.IsCompleted ? (int)_durationMs : null;

		/// <summary>
		/// The command line as it was launched, for logging and error messages.
		/// </summary>
		public string Command
		{
			get
			{
				var parameters = Descriptor.LaunchParameters;
				return parameters.Arguments.Count == 0
					? parameters.FileName
					: parameters.FileName + " " + string.Join(" ", parameters.Arguments);
			}
		}

		/// <summary>
		/// Returns the terminal buffer contents (scrollback plus the visible screen).
		/// </summary>
		public string Text() => CurrentText();

		/// <summary>
		/// Waits for the screen to change and returns the whole buffer.
		/// </summary>
		/// <remarks>
		/// A PTY has no stream to take a delta from: the buffer is a rendered screen that repaints overwrite.
		/// The returned text is therefore the full current screen, and it is returned only when it differs from
		/// the previous read.
		/// </remarks>
		/// <param name="ms">How long to wait: omitted waits forever, zero returns at once.</param>
		/// <param name="cancellationToken">The cancellation token of the running script (injected by the Lua runtime).</param>
		/// <returns>The buffer and a state: <c>data</c>, <c>timeout</c> or <c>eof</c>.</returns>
		public async Task<LuaTuple> Read(object? ms = null, CancellationToken cancellationToken = default)
		{
			var timeout = LuaProcessArguments.ParseTimeout(ms, "process.run_terminal read");
			cancellationToken.ThrowIfCancellationRequested();

			while (true)
			{
				// Capture the pulse BEFORE the snapshot: a change that lands in between is either already
				// visible below or still completes the task we are about to await.
				var pulse = Volatile.Read(ref _pulse).Task;

				var text = CurrentText();
				if (text != _lastText)
				{
					_lastText = text;
					return new LuaTuple(new LuaString(text), new LuaString("data"));
				}

				if (Descriptor.ExitCodeTask.IsCompleted)
				{
					_lastText = text;
					return new LuaTuple(new LuaString(text), new LuaString("eof"));
				}

				if (timeout == 0)
					return new LuaTuple(new LuaString(text), new LuaString("timeout"));

				var winner = timeout is { } milliseconds
					? await Task.WhenAny(pulse, _exited, Task.Delay(milliseconds))
						.WaitAsync(cancellationToken).ConfigureAwait(false)
					: await Task.WhenAny(pulse, _exited)
						.WaitAsync(cancellationToken).ConfigureAwait(false);

				if (winner != pulse && winner != _exited)
					return new LuaTuple(new LuaString(CurrentText()), new LuaString("timeout"));
			}
		}

		/// <summary>
		/// Writes raw text into the terminal standard input. Escape sequences are sent as is.
		/// </summary>
		/// <remarks>
		/// Synchronous on purpose: when the call returns, the bytes are in the PTY, so a script cannot lose a
		/// line by forgetting an await, and a following <see cref="SendKey"/> keeps its order.
		/// </remarks>
		/// <param name="text">The text to write.</param>
		/// <returns><see langword="false"/> when the process is already gone.</returns>
		public bool Write(string? text)
		{
			if (string.IsNullOrEmpty(text))
				return false;

			var stream = Descriptor.TerminalSession?.Pty.WriterStream;
			if (stream is null)
				return false;

			lock (_writeLock)
			{
				try
				{
					stream.Write(_utf8.GetBytes(text));
					stream.Flush();
					return true;
				}
				catch (Exception ex) when (ex is IOException or ObjectDisposedException or InvalidOperationException)
				{
					// The process may have exited between the check and the write; nothing to report.
					return false;
				}
			}
		}

		/// <summary>
		/// Writes a line into the terminal standard input (the text followed by a carriage return).
		/// </summary>
		/// <param name="text">The line to write.</param>
		/// <returns><see langword="false"/> when the process is already gone.</returns>
		public bool WriteLine(string? text = null) => Write((text ?? string.Empty) + "\r");

		/// <summary>
		/// Sends one named key, so a script never has to spell an escape sequence itself.
		/// </summary>
		/// <param name="name">
		/// A key name: <c>Enter</c>, <c>Tab</c>, <c>Escape</c>, <c>Backspace</c>, <c>Delete</c>, <c>Insert</c>,
		/// <c>Up</c>/<c>Down</c>/<c>Left</c>/<c>Right</c>, <c>Home</c>/<c>End</c>, <c>PageUp</c>/<c>PageDown</c>,
		/// <c>F1</c>-<c>F12</c>, <c>Space</c>, or <c>Ctrl+A</c>-<c>Ctrl+Z</c>. Case, dashes and spaces are ignored.
		/// </param>
		/// <returns><see langword="false"/> when the process is already gone.</returns>
		public bool SendKey(string? name)
		{
			if (string.IsNullOrWhiteSpace(name))
				throw new LuaRuntimeException("process.run_terminal send_key(name): a key name expected.");

			var key = NormaliseKey(name);
			if (_namedKeys.TryGetValue(key, out var sequence))
				return Write(sequence);

			// Ctrl+<letter> is the letter with its top three bits cleared, which is what terminals send.
			if (key.Length == 5 && key.StartsWith("ctrl", StringComparison.Ordinal))
			{
				var letter = char.ToUpperInvariant(key[4]);
				if (letter is >= 'A' and <= 'Z')
					return Write(((char)(letter & 0x1F)).ToString());
			}

			throw new LuaRuntimeException($"process.run_terminal send_key(name): unknown key '{name}'.");
		}

		/// <summary>
		/// Sends text wrapped in the bracketed-paste markers, so an interactive program pastes instead of typing.
		/// </summary>
		/// <param name="text">The text to paste.</param>
		/// <returns><see langword="false"/> when the process is already gone.</returns>
		public bool Paste(string? text) => Write("\x1b[200~" + (text ?? string.Empty) + "\x1b[201~");

		/// <summary>
		/// Resizes the screen and the PTY behind it.
		/// </summary>
		/// <param name="columns">The new width in columns.</param>
		/// <param name="rows">The new height in rows.</param>
		/// <returns><see langword="false"/> when the process is already gone.</returns>
		public bool Resize(int columns, int rows)
		{
			if (columns is < 1 or > 1000)
				throw new LuaRuntimeException("process.run_terminal resize(): columns must be between 1 and 1000.");
			if (rows is < 1 or > 500)
				throw new LuaRuntimeException("process.run_terminal resize(): rows must be between 1 and 500.");

			var session = Descriptor.TerminalSession;
			if (session is null)
				return false;

			try
			{
				session.Terminal.Resize(columns, rows);
				session.Pty.Resize(columns, rows);
				return true;
			}
			catch (Exception)
			{
				// The process is already gone; a resize nobody reads is not an error worth surfacing.
				return false;
			}
		}

		/// <summary>
		/// Kills the process behind this terminal.
		/// </summary>
		/// <returns><see langword="true"/> when the process was still running.</returns>
		public bool Kill()
		{
			if (Descriptor.ExitCodeTask.IsCompleted)
				return false;

			try
			{
				Descriptor.CancellationTokenSource.Cancel();
				return true;
			}
			catch (Exception)
			{
				return false;
			}
		}

		/// <summary>
		/// Waits for the process to exit and returns its exit code.
		/// </summary>
		/// <param name="ms">How long to wait: omitted waits forever, zero returns at once.</param>
		/// <param name="cancellationToken">
		/// The cancellation token of the running script (injected by the Lua runtime). When it trips, the wait
		/// is abandoned with <see cref="OperationCanceledException"/> instead of holding the script forever.
		/// </param>
		/// <returns>
		/// The exit code, or <c>nil</c> when the wait timed out without killing the process.
		/// Every completed wait on the same handle returns the same value.
		/// </returns>
		public async Task<LuaTuple> Wait(object? ms = null, CancellationToken cancellationToken = default)
		{
			var timeout = LuaProcessArguments.ParseTimeout(ms, "process.run_terminal wait");
			if (timeout is { } milliseconds)
			{
				var winner = await Task.WhenAny(_exited, Task.Delay(milliseconds))
					.WaitAsync(cancellationToken).ConfigureAwait(false);
				if (winner != _exited)
					return new LuaTuple(LuaNil.Instance);
			}
			else
			{
				await _exited.WaitAsync(cancellationToken).ConfigureAwait(false);
			}

			return new LuaTuple(new LuaNumber(ExitCode ?? -1));
		}

		/// <summary>
		/// Reads the current terminal buffer.
		/// </summary>
		private string CurrentText() => Descriptor.TerminalSession?.Output ?? string.Empty;

		/// <summary>
		/// Fires the pulse of every reader; the PTY pump raises this on each chunk.
		/// </summary>
		private void OnOutputUpdated(object? sender, EventArgs e)
			=> Interlocked.Exchange(ref _pulse, NewPulse()).TrySetResult();

		/// <summary>
		/// Records the lifetime and releases the readers once nothing more can arrive.
		/// </summary>
		private async Task TrackExitAsync()
		{
			try
			{
				await _exited.ConfigureAwait(false);
			}
			catch (Exception)
			{
				// The exit observer swallows faults; nothing to record.
			}

			_durationMs = _stopwatch.ElapsedMilliseconds;

			if (Interlocked.Exchange(ref _unsubscribed, 1) != 0)
				return;

			if (Descriptor.TerminalSession is { } session)
				session.OutputUpdated -= OnOutputUpdated;

			// Wake everything still waiting: the exit is what they are waiting for now.
			Volatile.Read(ref _pulse).TrySetResult();
		}

		/// <summary>
		/// Kills the process when its own deadline passes, marking the reason first so the status can tell
		/// a timeout from a kill.
		/// </summary>
		private async Task EnforceTimeoutAsync(TimeSpan timeout)
		{
			await Task.Delay(timeout).ConfigureAwait(false);
			if (Descriptor.ExitCodeTask.IsCompleted)
				return;

			Interlocked.Exchange(ref _timedOut, 1);
			Kill();
		}

		/// <summary>
		/// Strips separators and lowercases a key name, so <c>Ctrl+C</c>, <c>ctrl-c</c> and <c>CTRL+C</c> agree.
		/// </summary>
		private static string NormaliseKey(string name)
		{
			var builder = new StringBuilder(name.Length);
			foreach (var c in name)
			{
				if (c is not ('-' or '+' or '_' or ' '))
					builder.Append(char.ToLowerInvariant(c));
			}

			return builder.ToString();
		}

		/// <summary>
		/// A fresh uncompleted pulse; completions never run inline on the pump thread.
		/// </summary>
		private static TaskCompletionSource NewPulse()
			=> new(TaskCreationOptions.RunContinuationsAsynchronously);
	}
}
