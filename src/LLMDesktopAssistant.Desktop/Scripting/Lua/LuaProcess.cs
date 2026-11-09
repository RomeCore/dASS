using System.Collections.Specialized;
using System.Diagnostics;
using AsyncLua;
using AsyncLua.Values;
using LLMDesktopAssistant.Desktop.Execution;
using LLMDesktopAssistant.Utils;

namespace LLMDesktopAssistant.Desktop.Scripting.Lua
{
	/// <summary>
	/// A live pipe-based process exposed to Lua as the <c>process</c> userdata returned by
	/// <c>process.exec()</c>.
	/// </summary>
	/// <remarks>
	/// The handle owns cursors over the output the launcher already collects, so snapshots and incremental
	/// reads never consume anything a C# consumer of the same descriptor may still need. Every reader is
	/// woken by a pulse raised from <c>CollectionChanged</c>, never by polling.
	/// </remarks>
	public sealed class LuaProcess
	{
		private const string stringMode = "string";
		private const string tableMode = "table";

		private readonly ProcessDescriptor _descriptor;
		private readonly ProcessOutput _output;
		private readonly Stopwatch _stopwatch = Stopwatch.StartNew();
		private readonly Task _exited;
		private readonly LineView _stdout;
		private readonly LineView _stderr;
		private readonly LineView _merged;

		private TaskCompletionSource _pulse = NewPulse();
		private int _timedOut;
		private int _unsubscribed;
		private long _durationMs;

		/// <summary>
		/// Initializes a handle over a launched process.
		/// </summary>
		/// <param name="descriptor">The descriptor produced by the launcher.</param>
		/// <param name="timeoutSeconds">The wall-clock limit of the run, or <see langword="null"/> for none.</param>
		public LuaProcess(ProcessDescriptor descriptor, int? timeoutSeconds)
		{
			_descriptor = descriptor;
			_output = descriptor.PlainOutput ?? new ProcessOutput();
			// A non-faulting view of the exit: WhenAny must never surface a cancelled or failed handler.
			_exited = descriptor.ExitCodeTask.ContinueWith(static _ => { }, TaskScheduler.Default);
			_stdout = new LineView(_output.StdOut);
			_stderr = new LineView(_output.StdErr);
			_merged = new LineView(_output.Output);

			_output.StdOut.CollectionChanged += OnOutputChanged;
			_output.StdErr.CollectionChanged += OnOutputChanged;
			_output.Output.CollectionChanged += OnOutputChanged;

			_ = TrackExitAsync();

			if (timeoutSeconds is > 0)
				_ = EnforceTimeoutAsync(TimeSpan.FromSeconds(timeoutSeconds.Value));
		}

		/// <summary>
		/// The descriptor produced by the launcher. Hidden from Lua.
		/// </summary>
		[LuaHidden]
		public ProcessDescriptor Descriptor => _descriptor;

		/// <summary>
		/// Wraps the handle into a <c>process</c> userdata.
		/// </summary>
		[LuaHidden]
		public static LuaUserData Wrap(LuaProcess process)
			=> new(process, "process")
			{
				Metatable = UserDataMetatableGenerator.GetOrCreate(typeof(LuaProcess))
			};

		/// <summary>
		/// The OS process id.
		/// </summary>
		public int Pid => _descriptor.ProcessId;

		/// <summary>
		/// The lifecycle state: <c>running</c>, <c>success</c>, <c>failed</c>, <c>cancelled</c> or <c>timed_out</c>.
		/// </summary>
		public string Status
		{
			get
			{
				var exit = _descriptor.ExitCodeTask;
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
				var exit = _descriptor.ExitCodeTask;
				if (!exit.IsCompleted)
					return null;
				return exit.Status == TaskStatus.RanToCompletion ? exit.Result : -1;
			}
		}

		/// <summary>
		/// Whether the process is still running.
		/// </summary>
		public bool IsRunning => !_descriptor.ExitCodeTask.IsCompleted;

		/// <summary>
		/// Whether the process was killed by its own timeout rather than by a caller or the script's token.
		/// </summary>
		public bool TimedOut => Volatile.Read(ref _timedOut) == 1;

		/// <summary>
		/// The lifetime in milliseconds, or <see langword="null"/> while the process runs.
		/// </summary>
		public int? DurationMs => _descriptor.ExitCodeTask.IsCompleted ? (int)_durationMs : null;

		/// <summary>
		/// The command line as it was launched, for logging and error messages.
		/// </summary>
		public string Command
		{
			get
			{
				var parameters = _descriptor.LaunchParameters;
				return parameters.Arguments.Count == 0
					? parameters.FileName
					: parameters.FileName + " " + string.Join(" ", parameters.Arguments);
			}
		}

		/// <summary>
		/// Whether the standard input is closed, either by the caller or because it was never redirected.
		/// </summary>
		public bool StdinClosed => _descriptor.StandardInput is null || _descriptor.StandardInput.IsClosed;

		/// <summary>
		/// The whole standard output as a string (default) or as a table of lines.
		/// </summary>
		public LuaTuple Stdout(string? mode = null, CancellationToken cancellationToken = default)
			=> new(Shape(_output.StdOut, ParseMode(mode, "process:stdout")));

		/// <summary>
		/// The whole standard error as a string (default) or as a table of lines.
		/// </summary>
		public LuaTuple Stderr(string? mode = null, CancellationToken cancellationToken = default)
			=> new(Shape(_output.StdErr, ParseMode(mode, "process:stderr")));

		/// <summary>
		/// Both streams in arrival order as a string (default) or as a table of lines.
		/// </summary>
		public LuaTuple Output(string? mode = null, CancellationToken cancellationToken = default)
			=> new(Shape(_output.Output, ParseMode(mode, "process:output")));

		/// <summary>
		/// Reads the standard output lines accumulated since the previous read.
		/// </summary>
		public Task<LuaTuple> ReadStdout(object? ms = null, string? mode = null,
			CancellationToken cancellationToken = default)
			=> ReadAsync(_stdout, ms, mode, "process:read_stdout", cancellationToken);

		/// <summary>
		/// Reads the standard error lines accumulated since the previous read.
		/// </summary>
		public Task<LuaTuple> ReadStderr(object? ms = null, string? mode = null,
			CancellationToken cancellationToken = default)
			=> ReadAsync(_stderr, ms, mode, "process:read_stderr", cancellationToken);

		/// <summary>
		/// Reads the lines of both streams, in arrival order, accumulated since the previous read.
		/// </summary>
		public Task<LuaTuple> ReadOutput(object? ms = null, string? mode = null,
			CancellationToken cancellationToken = default)
			=> ReadAsync(_merged, ms, mode, "process:read_output", cancellationToken);

		/// <summary>
		/// Writes a string as UTF-8 text or a table of byte values into the standard input.
		/// </summary>
		/// <remarks>
		/// Synchronous on purpose: when the call returns, the bytes are in the pipe, so a script cannot lose a
		/// line by forgetting an await, and end-of-file cannot overtake data still in flight.
		/// </remarks>
		/// <returns><see langword="false"/> when the input is closed or was never redirected.</returns>
		public bool Write(object? value)
		{
			switch (value)
			{
				case string text:
					return WriteText(text);
				case LuaTable bytes:
					return WriteBytes(bytes);
				default:
					throw new LuaRuntimeException("process:write(value): a string or a table of bytes expected.");
			}
		}

		/// <summary>
		/// Writes a line into the standard input, terminated with a newline.
		/// </summary>
		public bool WriteLine(string? text = null)
			=> WriteText((text ?? string.Empty) + "\n");

		/// <summary>
		/// Closes the standard input, sending end-of-file to the process.
		/// </summary>
		/// <returns><see langword="true"/> when this call closed it.</returns>
		public bool CloseStdin(CancellationToken cancellationToken = default)
			=> _descriptor.StandardInput?.Close() ?? false;

		/// <summary>
		/// Kills the process tree.
		/// </summary>
		/// <returns><see langword="true"/> when the process was still running.</returns>
		public bool Kill(CancellationToken cancellationToken = default)
		{
			if (_descriptor.ExitCodeTask.IsCompleted)
				return false;

			try
			{
				_descriptor.CancellationTokenSource.Cancel();
				return true;
			}
			catch (Exception)
			{
				// The process is already gone; Cancel on a disposed source is not an error worth surfacing.
				return false;
			}
		}

		/// <summary>
		/// Waits for the exit, optionally giving up after <paramref name="ms"/> milliseconds.
		/// </summary>
		/// <returns>The exit code, or <c>nil</c> when the wait timed out without killing the process.</returns>
		public async Task<LuaTuple> Wait(object? ms = null, CancellationToken cancellationToken = default)
		{
			var timeout = LuaProcessArguments.ParseTimeout(ms, "process:wait");
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
		/// Keeps the buffer independent of the caller and fires the pulse of every reader.
		/// </summary>
		private void OnOutputChanged(object? sender, NotifyCollectionChangedEventArgs e)
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

			_output.StdOut.CollectionChanged -= OnOutputChanged;
			_output.StdErr.CollectionChanged -= OnOutputChanged;
			_output.Output.CollectionChanged -= OnOutputChanged;

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
			if (_descriptor.ExitCodeTask.IsCompleted)
				return;

			Interlocked.Exchange(ref _timedOut, 1);
			Kill();
		}

		/// <summary>
		/// Reads one view incrementally, waiting for a pulse, the exit or the deadline.
		/// </summary>
		private async Task<LuaTuple> ReadAsync(LineView view, object? ms, string? mode, string function,
			CancellationToken cancellationToken)
		{
			var asTable = ParseMode(mode, function);
			var timeout = LuaProcessArguments.ParseTimeout(ms, function);
			cancellationToken.ThrowIfCancellationRequested();

			while (true)
			{
				// Capture the pulse BEFORE looking at the collection: a line that arrives in between is
				// then either already visible below or still completes the task we are about to await.
				var pulse = Volatile.Read(ref _pulse).Task;

				var batch = Take(view);
				if (batch.Count > 0)
					return new LuaTuple(Shape(batch, asTable), new LuaString("data"));
				if (_descriptor.ExitCodeTask.IsCompleted)
					return new LuaTuple(Shape([], asTable), new LuaString("eof"));
				if (timeout == 0)
					return new LuaTuple(Shape([], asTable), new LuaString("timeout"));

				var winner = timeout is { } milliseconds
					? await Task.WhenAny(pulse, _exited, Task.Delay(milliseconds))
						.WaitAsync(cancellationToken).ConfigureAwait(false)
					: await Task.WhenAny(pulse, _exited)
						.WaitAsync(cancellationToken).ConfigureAwait(false);

				if (winner != pulse && winner != _exited)
					return new LuaTuple(Shape([], asTable), new LuaString("timeout"));
			}
		}

		/// <summary>
		/// Takes every line past the cursor of the view and advances it.
		/// </summary>
		private static List<string> Take(LineView view)
		{
			view.Gate.Wait();
			try
			{
				var lines = view.Lines;
				var count = lines.Count;
				if (view.Position >= count)
					return [];

				var batch = new List<string>(count - view.Position);
				for (var i = view.Position; i < count; i++)
					batch.Add(lines[i]);
				view.Position = count;
				return batch;
			}
			finally
			{
				view.Gate.Release();
			}
		}

		/// <summary>
		/// Writes text into the standard input.
		/// </summary>
		private bool WriteText(string text)
			=> _descriptor.StandardInput?.WriteText(text) ?? false;

		/// <summary>
		/// Writes a table of byte values into the standard input.
		/// </summary>
		private bool WriteBytes(LuaTable table)
		{
			var buffer = new byte[table.Length];
			for (var i = 0; i < buffer.Length; i++)
			{
				if (table[i + 1] is not LuaNumber number || number.Value < 0 || number.Value > 255
					|| number.Value != Math.Floor(number.Value))
					throw new LuaRuntimeException("process:write(value): a byte table must contain integers 0..255.");

				buffer[i] = (byte)number.Value;
			}

			return _descriptor.StandardInput?.WriteBytes(buffer) ?? false;
		}

		/// <summary>
		/// Renders lines as a Lua string or as a Lua array of strings.
		/// </summary>
		private static LuaValue Shape(IReadOnlyList<string> lines, bool asTable)
		{
			if (!asTable)
				return new LuaString(string.Join("\n", lines));

			var table = new LuaTable();
			foreach (var line in lines)
				table.Append(new LuaString(line));
			return table;
		}

		/// <summary>
		/// Parses the optional <c>mode</c> argument of a snapshot or a read.
		/// </summary>
		private static bool ParseMode(string? mode, string function)
		{
			switch (mode)
			{
				case null:
				case stringMode:
					return false;
				case tableMode:
					return true;
				default:
					throw new LuaRuntimeException($"{function}([mode]): mode must be \"string\" or \"table\".");
			}
		}

		/// <summary>
		/// A fresh uncompleted pulse; completions never run inline on the pump thread.
		/// </summary>
		private static TaskCompletionSource NewPulse()
			=> new(TaskCreationOptions.RunContinuationsAsynchronously);

		/// <summary>
		/// One stream of output with its own read cursor and gate.
		/// </summary>
		private sealed class LineView(RangeObservableCollection<string> lines)
		{
			/// <summary>
			/// The lines the launcher keeps appending to.
			/// </summary>
			public RangeObservableCollection<string> Lines { get; } = lines;

			/// <summary>
			/// Keeps two concurrent readers from taking the same line twice.
			/// </summary>
			public SemaphoreSlim Gate { get; } = new(1, 1);

			/// <summary>
			/// The index of the first line this view has not read yet.
			/// </summary>
			public int Position { get; set; }
		}
	}
}
