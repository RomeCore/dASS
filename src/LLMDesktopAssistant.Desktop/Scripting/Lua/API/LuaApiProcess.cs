using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using AsyncLua;
using AsyncLua.Values;
using LLMDesktopAssistant.Desktop.Execution;
using LLMDesktopAssistant.Desktop.Scripting.Lua;
using LLMDesktopAssistant.LLM.Services;
using LLMDesktopAssistant.LLM.Settings;
using LLMDesktopAssistant.Scripting;
using LLMDesktopAssistant.Scripting.Lua;
using LLMDesktopAssistant.Scripting.Lua.API;

namespace LLMDesktopAssistant.Desktop.Scripting.Lua.API
{
	/// <summary>
	/// Lua API for process execution: <c>process.*</c>.
	/// Provides command execution, path resolution, and process management.
	/// </summary>
	[LuaApi(chatScoped: true)]
	public class LuaApiProcess : LuaApiBase
	{
		public override string? Namespace => "process";

		public override string? Manuals => """
			--- process - process execution API

			Provides command execution with timeout, output capture,
			path resolution, and basic process management.

			FUNCTIONS:

			--- process.which(command)
			  Resolves the full path of an executable by searching the PATH
			  environment variable (and PATHEXT on Windows).
			  Returns nil if the command is not found.

			  Parameters:
			    - command: string - The command name to locate (e.g. "git", "python")

			  Returns: string or nil - Full path to the executable, or nil if not found.

			--- process.kill(pid)
			  Kills a process by its process ID. Returns true if successful.

			  Parameters:
				- pid: number - The process ID to kill

			  Returns: boolean - Whether the process was killed.
			
			--- process.exec(command, [args], [options]) -> handle, error
			  Starts a process and returns a handle to it IMMEDIATELY, without waiting. Read its output,
			  write to its input, kill it and wait for the exit through the handle (see PROCESS HANDLE).
			
			  Parameters:
			    - command: string - The command or executable to run (e.g. "git", "node").
			      On Windows, also searches PATHEXT for executables.
			    - args: string or table (optional) - Arguments for the command.
				  As string: "status --short" - passed to the process VERBATIM as its command line, so the
					quoting you write is exactly what the program parses; no tokenizer sits in between.
				  As table: {"status", "--short"} - each element is one argument, escaped by the launcher.
				  If omitted, the command runs with no arguments.
			    - options: table (optional) - Execution options:
			      - cwd: string - Working directory (default: current directory)
			      - env: table - Additional environment variables as key-value pairs
			      - stdin: string - Data to write to the process standard input. It is written and the pipe
			        is closed at once, so a payload is the whole input; use handle:write() to keep writing.
			      - timeout_seconds: number (default: 30) - Wall-clock limit of the run; 0 disables it.
			        When it passes, the process is killed and its status becomes "timed_out".
			
			  Returns: handle, or nil plus the reason when the process cannot be started.
			
			  Throws: only when the arguments themselves are malformed (wrong types, unknown mode).
			
			--- process.run_terminal(command, [args], [options]) -> terminal, error
			  Starts an interactive terminal (a PTY) and returns a handle to it. A PTY merges the streams
			  and emulates a screen, so it is for interactive programs, not for reading output: use
			  process.exec when you want stdout and stderr separately. Nothing is attached for you: pass
			  the handle to an append_data() of the host you want it in (chat_message:append_data,
			  dass.chat.append_data, dass.tool.result.append_data). An unattached terminal still runs,
			  but the user cannot see or stop it - kill it yourself when you are done.
			
			  Parameters:
				- command: string - the executable to run
				- args: string or table (optional) - arguments; a string is passed verbatim as the command line,
				  a table element is one argument
				- options: table (optional):
				  - cwd: string - working directory (default: the current one)
				  - env: table - additional environment variables (key-value pairs)
				  - cols: number - initial width in columns (default: the emulation's own)
				  - rows: number - initial height in rows (default: the emulation's own)
				  - timeout_seconds: number (default: nil - infinite; 0 also means infinite)
			
			  Returns: terminal, or nil plus the error message.
				A missing executable, an inaccessible working directory or a failed spawn
				returns nil and the reason instead of throwing.
			
			PROCESS HANDLE ('process'):

			  Properties (read-only):
				- pid: number - the OS process id
				- status: string - "running", "success", "failed", "cancelled" or "timed_out"
				- exit_code: number or nil - nil while the process runs; -1 when it was killed
				- is_running: boolean - whether the process is still running
				- timed_out: boolean - whether its own deadline killed it
				- duration_ms: number or nil - the lifetime, nil while it runs
				- command: string - the command line as it was launched
				- stdin_closed: boolean - whether the standard input is closed or was never redirected

			  Snapshots (the whole output; the read cursors are not touched):
				- h:stdout([mode]) -> string | table - standard output
				- h:stderr([mode]) -> string | table - standard error
				- h:output([mode]) -> string | table - both streams in arrival order
				  mode is "string" (default, lines joined by "\n") or "table" (an array of lines);
				  an empty stream is "" or {}.

			  Incremental reads (independent cursors, non-destructive to every other consumer):
				- async h:read_stdout([ms], [mode]) -> text, state
				- async h:read_stderr([ms], [mode]) -> text, state
				- async h:read_output([ms], [mode]) -> text, state
				  Returns everything accumulated since the previous read of the same view.
				  state is "data" (text is non-empty), "eof" (the process is gone and everything was
				  read) or "timeout" (ms passed with nothing new). ms omitted waits forever, 0 returns
				  at once with whatever is buffered.

			  Control:
				- h:write(value) -> boolean - a string goes as UTF-8 text, a table of integers 0..255 as raw bytes;
				  the call returns only once the bytes are in the pipe, so ordering and EOF are guaranteed
				- h:write_line([text]) -> boolean - the text followed by "\n"
				- h:close_stdin() -> boolean - sends end-of-file
				- h:kill() -> boolean - kills the process tree; false when it had already exited
				- async h:wait([ms]) -> exit_code | nil - nil when the wait timed out; the process keeps running

			  NOTES:
				- The process is bound to the script's cancellation token: Stop, a cancelled tool or a
				  closed chat kills it. Launch it under task.without_cancellation() to detach it.
				- A process still running when the script ends keeps running; kill it if you care.
				- Nothing is attached to the UI: the output is read through the handle, not rendered.
				- Hosts without the desktop process launcher have no 'process' namespace at all.

			TERMINAL HANDLE ('terminal'):

			  Properties (read-only):
				- pid: number - the OS process id
				- status: string - "running", "success", "failed", "cancelled" or "timed_out"
				- exit_code: number or nil - nil while the process runs; -1 when it was killed
				- is_running: boolean - whether the process is still running
				- timed_out: boolean - whether its own deadline killed it
				- duration_ms: number or nil - the lifetime, nil while it runs
				- command: string - the command line as it was launched

			  Screen:
				- t:text() -> string - the rendered buffer: scrollback plus the visible screen, escape sequences
				  already interpreted and repaints already applied
				- async t:read([ms]) -> text, state - waits until the screen CHANGES and returns the whole buffer
				  (a PTY has no stream to take a delta from). state is "data", "timeout" (ms passed with no
				  change) or "eof" (the process is gone). ms omitted waits forever, 0 returns at once.
				- t:resize(cols, rows) -> boolean - resizes the screen and the PTY behind it

			  Input:
				- t:write(text) -> boolean - raw text, escape sequences sent as is
				- t:write_line([text]) -> boolean - the text followed by a carriage return
				- t:send_key(name) -> boolean - Enter, Tab, Escape, Backspace, Delete, Insert, Up, Down, Left,
				  Right, Home, End, PageUp, PageDown, F1-F12, Space or Ctrl+A..Ctrl+Z
				- t:paste(text) -> boolean - wraps the text in the bracketed-paste markers

			  Control:
				- t:kill() -> boolean - kills the process tree; false when it had already exited
				- async t:wait([ms]) -> exit_code | nil - nil when the wait timed out; the process keeps running

			  NOTES:
				- Removing the attachment that holds a terminal kills its process; the card's Leave button frees
				  it instead and lets it outlive the call.
				- The screen is re-rendered from the emulation, so a read returns a snapshot, never a delta.
				- Hosts without the desktop process launcher have no 'process' namespace at all.

			EXAMPLES:

			  -- Run and collect
			  local h, err = process.exec("git", {"status", "--short"}, { cwd = "/path/to/repo" })
			  if not h then print("Cannot start:", err) return end
			  local code = await h:wait()
			  print("Exit:", code, "in", h.duration_ms, "ms")
			  if h.stderr ~= "" then print("Errors:", h.stderr) end
			  for _, line in ipairs(h:stdout("table")) do print(line) end

			  -- Watch a long process live
			  local h2 = process.exec("npm", { "install" }, { timeout_seconds = 0 })
			  while true do
				local chunk, state = await h2:read_output(1000)
				if chunk ~= "" then print(chunk) end
				if state == "eof" then break end
			  end
			  print("npm finished:", h2.status)

			  -- Drive a child that reads its input
			  local h3 = process.exec("sort")
			  h3:write_line("banana")
			  h3:write_line("apple")
			  h3:close_stdin()
			  await h3:wait()
			  print(h3:stdout())

			  -- With environment variables
			  local h4 = process.exec("node", "script.js", {
				cwd = "/project",
				env = { NODE_ENV = "production", DEBUG = "true" },
				timeout_seconds = 60
			  })
			  await h4:wait()

			  -- Start an interactive terminal, attach it to the command's message and wait for it
			  local term, err = process.run_terminal("claude", nil, { cwd = "/project" })
			  if not term then
				print("Cannot start:", err)
			  else
				local msg = dass.command.context.get_message()
				if msg then msg:append_data(term) end
				print("Exit code:", await term:wait())
			  end
			
			  -- Find a command
			  local git_path = process.which("git")
			  if git_path then
			    print("Git found at:", git_path)
			  end
			
			  -- Kill a process
			  print("Killed:", process.kill(12345))
			""";

		private readonly IWorkingDirectoryAccessService _fileAccess;
		private readonly IProcessLauncher _processLauncher;

		public LuaApiProcess(IWorkingDirectoryAccessService fileAccess, IProcessLauncher processLauncher)
		{
			_fileAccess = fileAccess;
			_processLauncher = processLauncher;
		}

		private const int DefaultTimeoutSeconds = 30;

		public override Action? Populate(LuaTable globals, LuaTable ns, LuaService luaService)
		{
			ns["exec"] = new LuaCallbackFunction(Exec);
			ns["run_terminal"] = new LuaCallbackFunction(RunTerminal);
			ns["which"] = new LuaCallbackFunction(Which);
			ns["kill"] = new LuaCallbackFunction(Kill, isAsync: false);
			return null;
		}

		private LuaTuple Exec(LuaCallingContext ctx, LuaValue[] args)
		{
			var invocation = ParseInvocation("process.exec", args);
			var verbatim = !string.IsNullOrWhiteSpace(invocation.CommandLine);

			var resolvedCommand = ResolveCommandPath(invocation.Command);
			var displayCommand = resolvedCommand ?? invocation.Command;

			var cwd = _fileAccess.GetFullPath(invocation.WorkingDirectory ?? "");
			if (string.IsNullOrEmpty(cwd))
				return InvocationError("process.exec(): unable to access working directory.");
			if (!Directory.Exists(cwd))
				return InvocationError("process.exec(): working directory does not exist.");

			ProcessDescriptor descriptor;
			try
			{
				descriptor = _processLauncher.Launch(new ProcessLaunchParameters
				{
					ProcessName = invocation.Command,
					FileName = displayCommand,
					Arguments = verbatim ? [invocation.CommandLine!] : [.. invocation.Arguments],
					VerbatimArguments = verbatim,
					WorkingDirectory = cwd,
					EnvironmentVariables = invocation.Environment.ToImmutableDictionary(),
					StdIn = invocation.StdIn,
					// The handle owns the deadline, so a timeout can be told apart from a kill.
					RedirectStdIn = true,
					TimeOut = null
				}, ctx.CancellationToken);
			}
			catch (Exception ex)
			{
				return InvocationError($"Failed to start process '{invocation.Command}': {ex.Message}");
			}

			var timeoutSeconds = invocation.TimeoutSeconds ?? DefaultTimeoutSeconds;
			var handle = new LuaProcess(descriptor, timeoutSeconds > 0 ? timeoutSeconds : null);
			return new LuaTuple(LuaProcess.Wrap(handle));
		}

		/// <summary>
		/// Heuristic to distinguish an options table from an args table.
		/// An args table is a Lua array (consecutive integer keys starting at 1).
		/// An options table has string keys.
		/// </summary>
		private static bool IsArgsTable(LuaTable table)
		{
			foreach (var kv in table.Entries)
			{
				if (kv.Key is LuaString)
					return false;
			}
			return true;
		}

		/// <summary>
		/// Starts an interactive terminal and returns a handle to it: <c>process.run_terminal</c>.
		/// </summary>
		private LuaTuple RunTerminal(LuaCallingContext ctx, LuaValue[] args)
		{
			Invocation invocation;
			try
			{
				invocation = ParseInvocation("process.run_terminal", args);
			}
			catch (LuaRuntimeException ex)
			{
				return InvocationError(ex.Message);
			}

			// Do NOT require PATH resolution here: the OS resolves a bare name on its own
			// (cmd.exe is a classic case where PATH does not list System32), and a spawn
			// failure below reports the real reason.
			var resolved = ResolveCommandPath(invocation.Command);
			var verbatim = !string.IsNullOrWhiteSpace(invocation.CommandLine);

			var workDir = _fileAccess.GetFullPath(invocation.WorkingDirectory ?? "");
			if (string.IsNullOrEmpty(workDir))
				return InvocationError("process.run_terminal(): unable to access working directory.");
			if (!Directory.Exists(workDir))
				return InvocationError("process.run_terminal(): working directory does not exist.");

			ProcessDescriptor descriptor;
			try
			{
				// The script's own token: cancelling the command (Stop, chat closed) kills its terminal,
				// and nothing links the two otherwise.
				descriptor = _processLauncher.Launch(new ProcessLaunchParameters
				{
					ProcessName = invocation.Command,
					FileName = resolved ?? invocation.Command,
					Arguments = verbatim ? [invocation.CommandLine!] : [.. invocation.Arguments],
					VerbatimArguments = verbatim,
					WorkingDirectory = workDir,
					RunInTerminal = true,
					Columns = invocation.Columns,
					Rows = invocation.Rows,
					EnvironmentVariables = invocation.Environment.ToImmutableDictionary(),
					// The handle owns the deadline, so a timeout can be told apart from a kill.
					TimeOut = null
				}, ctx.CancellationToken);
			}
			catch (Exception ex)
			{
				return InvocationError($"Failed to start terminal '{invocation.Command}': {ex.Message}");
			}

			return new LuaTuple(LuaTerminal.Wrap(new LuaTerminal(descriptor, invocation.TimeoutSeconds)));
		}

		/// <summary>
		/// Builds the <c>nil, error</c> result of a failed terminal launch.
		/// </summary>
		private static LuaTuple InvocationError(string message)
			=> new(LuaNil.Instance, new LuaString(message));

		/// <summary>
		/// Parses the <c>(command, [args], [options])</c> argument shape of <c>process.run_terminal</c>.
		/// </summary>
		private static Invocation ParseInvocation(string functionName, LuaValue[] args)
		{
			if (args.Length < 1)
				throw new LuaRuntimeException($"{functionName}(command, [args], [options]): at least 1 argument expected.");

			if (args[0] is not LuaString commandVal)
				throw new LuaRuntimeException($"{functionName}(): first argument must be a string (command).");

			var command = commandVal.Value;
			var cmdArgs = Array.Empty<string>();
			string? cwd = null;
			string? stdin = null;
			string? commandLine = null;
			int? timeoutSeconds = null;
			var envVars = new Dictionary<string, string?>();
			int? columns = null;
			int? rows = null;

			if (args.Length > 1 && args[1] is not LuaNil)
			{
				if (args[1] is LuaString argsString)
				{
					// A whole command line: it goes to the child verbatim, so the quoting the caller wrote is what
					// the program parses - no tokenizer, no round trip, no mangled backslashes.
					commandLine = argsString.Value;
				}
				else if (args[1] is LuaTable argsTable)
				{
					cmdArgs = new string[argsTable.Entries.Count()];
					var i = 0;
					foreach (var kv in argsTable.Entries.OrderBy(kv => kv.Key.TryToNumber()))
					{
						if (kv.Value is LuaString s)
							cmdArgs[i++] = s.Value;
						else
							throw new LuaRuntimeException($"{functionName}(): args[{i}] must be a string.");
					}
				}
				else
				{
					throw new LuaRuntimeException($"{functionName}(): second argument must be a string or table (args).");
				}
			}

			var optsIndex = args.Length > 1 && args[1] is LuaTable table && !IsArgsTable(table) ? 1
				: args.Length > 2 && args[2] is LuaTable ? 2
				: -1;

			if (optsIndex >= 0 && args[optsIndex] is LuaTable opts)
			{
				if (opts.Get("cwd") is LuaString cwdVal)
					cwd = cwdVal.Value;

				if (opts.Get("stdin") is LuaString stdinVal)
					stdin = stdinVal.Value;

				if (opts.Get("timeout_seconds") is LuaNumber tsVal)
					timeoutSeconds = Math.Max(0, (int)tsVal.Value);

				if (opts.Get("cols") is LuaNumber colsVal)
					columns = Math.Clamp((int)colsVal.Value, 1, 1000);

				if (opts.Get("rows") is LuaNumber rowsVal)
					rows = Math.Clamp((int)rowsVal.Value, 1, 500);

				if (opts.Get("env") is LuaTable envTable)
				{
					foreach (var kv in envTable.Entries)
					{
						if (kv.Key is LuaString keyStr && kv.Value is LuaString valStr)
							envVars[keyStr.Value] = valStr.Value;
					}
				}
			}

			return new Invocation(command, cmdArgs, commandLine, cwd, envVars, stdin, timeoutSeconds, columns, rows);
		}

		/// <summary>
		/// The parsed <c>(command, args, options)</c> of a process or terminal launch.
		/// </summary>
		/// <remarks>
		/// <paramref name="StdIn"/> and <paramref name="TimeoutSeconds"/> only make sense for
		/// <c>process.exec</c>: a terminal has no initial stdin and never times out. A null timeout
		/// means the default of 30 seconds, and zero means no limit at all.
		/// </remarks>
		private sealed record Invocation(string Command, string[] Arguments, string? CommandLine,
			string? WorkingDirectory, Dictionary<string, string?> Environment, string? StdIn, int? TimeoutSeconds,
			int? Columns, int? Rows);

		/// <summary>
		/// Resolves the full path of a command by searching PATH (and PATHEXT on Windows).
		/// </summary>
		private static string? ResolveCommandPath(string command)
		{
			return ExecutableLookup.FindOnPath(command);
		}

		private static LuaTuple Which(LuaCallingContext ctx, LuaValue[] args)
		{
			if (args.Length < 1)
				throw new LuaRuntimeException("process.which(command): at least 1 argument expected.");

			if (args[0] is not LuaString commandVal)
				throw new LuaRuntimeException("process.which(): first argument must be a string.");

			var resolved = ResolveCommandPath(commandVal.Value);
			if (resolved == null)
				return new LuaTuple(LuaNil.Instance);

			return new LuaTuple(new LuaString(resolved));
		}

		private static async Task<LuaTuple> Kill(LuaCallingContext ctx, LuaValue[] args)
		{
			if (args.Length < 1)
				throw new LuaRuntimeException("process.kill(pid): at least 1 argument expected.");

			if (args[0] is not LuaNumber pidVal)
				throw new LuaRuntimeException("process.kill(): first argument must be a number (PID).");

			try
			{
				using var process = Process.GetProcessById((int)pidVal.Value);
				process.Kill(entireProcessTree: true);
				using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(3));
				await process.WaitForExitAsync(cts.Token);
				return new LuaTuple(LuaBoolean.True);
			}
			catch (ArgumentException)
			{
				return new LuaTuple(LuaBoolean.False);
			}
			catch (InvalidOperationException)
			{
				return new LuaTuple(LuaBoolean.False);
			}
			catch (Exception)
			{
				return new LuaTuple(LuaBoolean.False);
			}
		}
	}
}
