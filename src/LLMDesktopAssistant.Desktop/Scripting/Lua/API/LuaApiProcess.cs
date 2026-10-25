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

			--- async process.exec(command, [args], [options])
			  Executes a command and waits for completion.
			  Returns a table with the execution result.

			  Parameters:
			    - command: string - The command or executable to run (e.g. "git", "node").
			      On Windows, also searches PATHEXT for executables.
			    - args: string or table (optional) - Arguments for the command.
			      As string: "status --short" (quoted tokens supported: '--grep "fix bug"').
			      As table: {"status", "--short"}.
			      If omitted, the command runs with no arguments.
			    - options: table (optional) - Execution options:
			      - cwd: string - Working directory (default: current directory)
			      - env: table - Additional environment variables as key-value pairs
			      - stdin: string - Data to write to the process standard input
			      - timeout_seconds: number (default: 30, max: 600) - Maximum execution time.
			        If exceeded, the process is killed and timed_out is true.

			  Returns: table with:
			    - exit_code: number - Process exit code (0 = success)
			    - stdout: string - Standard output of the command
			    - stderr: string - Standard error of the command
			    - timed_out: boolean - Whether the process was killed due to timeout
			    - duration_ms: number - Wall-clock execution time in milliseconds
			    - command: string - The full command that was executed (for logging)

			  Throws: If the command cannot be started or arguments are invalid.

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

			--- process.run_terminal(command, [args], [options]) -> terminal, error
			  Starts an interactive terminal (a PTY) and returns a handle to it. Unlike process.exec
			  it does NOT wait and never times out: the process lives until it exits or is killed.
			  Nothing is attached for you: pass the handle to an append_data() of the host you want
			  it in (chat_message:append_data, dass.chat.append_data, dass.tool.result.append_data).

			  Parameters:
				- command: string - the executable to run
				- args: string or table (optional) - arguments
				- options: table (optional):
				  - cwd: string - working directory (default: the current one)
				  - env: table - additional environment variables (key-value pairs)

			  Returns: terminal, or nil plus the error message.
				A missing executable, an inaccessible working directory or a failed spawn
				returns nil and the reason instead of throwing.

			TERMINAL HANDLE ('terminal'):

			  Properties (read-only):
				- pid: number - the OS process id
				- is_running: boolean - whether the process is still running
				- exit_code: number or nil - nil while the process runs

			  Methods:
				- t:text() -> string - the current buffer contents (scrollback plus the visible screen)
				- t:write(text) - writes raw text into the terminal input
				- t:write_line(text) - writes the text followed by a carriage return
				- t:kill() - kills the process
				- t:wait() -> number - waits for the exit and returns the exit code
				  (the same value on every call; -1 when the process was killed)

			  NOTES:
				- Removing the attachment that holds a terminal kills its process.
				- Hosts without the desktop process launcher have no 'process' namespace at all.

			EXAMPLES:

			  -- Args as table
			  local r = await process.exec("git", {"status", "--short"}, {
			    cwd = "/path/to/repo"
			  })

			  -- Args as string
			  local r = await process.exec("git", "status --short", {
			    cwd = "/path/to/repo"
			  })

			  -- No args at all
			  local r = await process.exec("ls", nil, { cwd = "/tmp" })

			  -- With stdin input
			  local r = await process.exec("python", {"-c", "print(input().upper())"}, {
			    stdin = "hello world"
			  })

			  -- With environment variables
			  local r = await process.exec("node", "script.js", {
			    cwd = "/project",
			    env = { NODE_ENV = "production", DEBUG = "true" },
			    timeout_seconds = 60
			  })

			  -- Inspect result
			  print("Exit:", r.exit_code)
			  print("Took:", r.duration_ms, "ms")
			  if r.stderr ~= "" then
			    print("Errors:", r.stderr)
			  end

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
			ns["exec"] = new LuaCallbackFunction(ExecAsync);
			ns["run_terminal"] = new LuaCallbackFunction(RunTerminal);
			ns["which"] = new LuaCallbackFunction(Which);
			ns["kill"] = new LuaCallbackFunction(Kill);
			return null;
		}

		private async Task<LuaTuple> ExecAsync(LuaCallingContext ctx, LuaValue[] args)
		{
			var invocation = ParseInvocation("process.exec", args);

			var resolvedCommand = ResolveCommandPath(invocation.Command);
			var displayCommand = resolvedCommand ?? invocation.Command;

			var cwd = _fileAccess.TryAccessPath(invocation.WorkingDirectory ?? "", DirectoryAccessMode.Execute);
			if (string.IsNullOrEmpty(cwd))
				throw new LuaRuntimeException("process.exec(): unable to access working directory.");
			if (!Directory.Exists(cwd))
				throw new LuaRuntimeException("process.exec(): working directory does not exist.");

			var startTime = DateTime.UtcNow;
			ProcessDescriptor descriptor;
			try
			{
				descriptor = _processLauncher.Launch(new ProcessLaunchParameters
				{
					ProcessName = invocation.Command,
					FileName = displayCommand,
					Arguments = [.. invocation.Arguments],
					WorkingDirectory = cwd,
					EnvironmentVariables = invocation.Environment.ToImmutableDictionary(),
					StdIn = invocation.StdIn,
					TimeOut = TimeSpan.FromSeconds(invocation.TimeoutSeconds)
				}, ctx.CancellationToken);
			}
			catch (Exception ex)
			{
				return BuildErrorResult($"Failed to start process '{invocation.Command}': {ex.Message}",
					displayCommand, (int)(DateTime.UtcNow - startTime).TotalMilliseconds);
			}

			var timedOut = false;
			var exitCode = -1;
			try
			{
				exitCode = await descriptor.ExitCodeTask.ConfigureAwait(false);
			}
			catch (OperationCanceledException)
			{
				// The launcher cancels the task when the timeout fires.
				timedOut = true;
			}
			catch (Exception)
			{
				// The process died with an error; its captured output is still readable below.
			}

			var output = descriptor.PlainOutput;
			var stdout = output is null ? string.Empty : string.Join("\n", output.StdOut);
			var stderr = output is null ? string.Empty : string.Join("\n", output.StdErr);

			var result = new LuaTable();
			result["exit_code"] = new LuaNumber(timedOut ? -1 : exitCode);
			result["stdout"] = new LuaString(stdout);
			result["stderr"] = new LuaString(stderr);
			result["timed_out"] = LuaBoolean.FromBoolean(timedOut);
			result["duration_ms"] = new LuaNumber((int)(DateTime.UtcNow - startTime).TotalMilliseconds);
			result["command"] = new LuaString(displayCommand +
				(invocation.Arguments.Length > 0 ? " " + string.Join(" ", invocation.Arguments) : ""));

			return new LuaTuple(result);
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

		private static LuaTuple BuildErrorResult(string errorMessage, string command, int durationMs)
		{
			var result = new LuaTable();
			result["exit_code"] = new LuaNumber(-1);
			result["stdout"] = new LuaString("");
			result["stderr"] = new LuaString(errorMessage);
			result["timed_out"] = LuaBoolean.False;
			result["duration_ms"] = new LuaNumber(durationMs);
			result["command"] = new LuaString(command);
			return new LuaTuple(result);
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

			var workDir = _fileAccess.TryAccessPath(invocation.WorkingDirectory ?? "", DirectoryAccessMode.Execute);
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
					Arguments = [.. invocation.Arguments],
					WorkingDirectory = workDir,
					RunInTerminal = true,
					EnvironmentVariables = invocation.Environment.ToImmutableDictionary(),
					TimeOut = null
				}, ctx.CancellationToken);
			}
			catch (Exception ex)
			{
				return InvocationError($"Failed to start terminal '{invocation.Command}': {ex.Message}");
			}

			return new LuaTuple(LuaTerminal.Wrap(new LuaTerminal(descriptor)));
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
			var timeoutSeconds = DefaultTimeoutSeconds;
			var envVars = new Dictionary<string, string?>();

			if (args.Length > 1 && args[1] is not LuaNil)
			{
				if (args[1] is LuaString argsString)
				{
					cmdArgs = TokenizeArgs(argsString.Value);
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
					timeoutSeconds = Math.Max(1, Math.Min(600, (int)tsVal.Value));

				if (opts.Get("env") is LuaTable envTable)
				{
					foreach (var kv in envTable.Entries)
					{
						if (kv.Key is LuaString keyStr && kv.Value is LuaString valStr)
							envVars[keyStr.Value] = valStr.Value;
					}
				}
			}

			return new Invocation(command, cmdArgs, cwd, envVars, stdin, timeoutSeconds);
		}

		/// <summary>
		/// The parsed <c>(command, args, options)</c> of a process or terminal launch.
		/// </summary>
		/// <remarks>
		/// <paramref name="StdIn"/> and <paramref name="TimeoutSeconds"/> only make sense for
		/// <c>process.exec</c>: a terminal has no initial stdin and never times out.
		/// </remarks>
		private sealed record Invocation(string Command, string[] Arguments, string? WorkingDirectory,
			Dictionary<string, string?> Environment, string? StdIn, int TimeoutSeconds);

		/// <summary>
		/// Tokenizes a command-line string into individual arguments,
		/// respecting single and double quotes.
		/// </summary>
		private static string[] TokenizeArgs(string argsString)
		{
			var tokens = new List<string>();
			var current = new StringBuilder();
			var inQuotes = false;
			var quoteChar = '\0';

			for (var i = 0; i < argsString.Length; i++)
			{
				var c = argsString[i];

				if (inQuotes)
				{
					if (c == quoteChar)
						inQuotes = false;
					else
						current.Append(c);
				}
				else if (c is '"' or '\'')
				{
					inQuotes = true;
					quoteChar = c;
				}
				else if (char.IsWhiteSpace(c))
				{
					if (current.Length > 0)
					{
						tokens.Add(current.ToString());
						current.Clear();
					}
				}
				else
				{
					current.Append(c);
				}
			}

			if (current.Length > 0)
				tokens.Add(current.ToString());

			return tokens.ToArray();
		}

		/// <summary>
		/// Resolves the full path of a command by searching PATH (and PATHEXT on Windows).
		/// </summary>
		private static string? ResolveCommandPath(string command)
		{
			if (Path.IsPathFullyQualified(command))
				return File.Exists(command) ? command : null;

			if (command.Contains(Path.DirectorySeparatorChar) || command.Contains(Path.AltDirectorySeparatorChar))
			{
				var fullPath = Path.GetFullPath(command);
				return File.Exists(fullPath) ? fullPath : null;
			}

			var pathEnv = Environment.GetEnvironmentVariable("PATH");
			if (string.IsNullOrEmpty(pathEnv))
				return null;

			var pathSeparator = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ';' : ':';
			var paths = pathEnv.Split(pathSeparator, StringSplitOptions.RemoveEmptyEntries);

			var extensions = new[] { string.Empty };
			if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
			{
				var pathext = Environment.GetEnvironmentVariable("PATHEXT");
				if (!string.IsNullOrEmpty(pathext))
					extensions = pathext.Split(';', StringSplitOptions.RemoveEmptyEntries);
			}

			foreach (var path in paths)
			{
				foreach (var ext in extensions)
				{
					var fullPath = Path.Combine(path.Trim(), command + ext);
					if (File.Exists(fullPath))
						return fullPath;
				}
			}

			return null;
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

		private static LuaTuple Kill(LuaCallingContext ctx, LuaValue[] args)
		{
			if (args.Length < 1)
				throw new LuaRuntimeException("process.kill(pid): at least 1 argument expected.");

			if (args[0] is not LuaNumber pidVal)
				throw new LuaRuntimeException("process.kill(): first argument must be a number (PID).");

			try
			{
				using var process = Process.GetProcessById((int)pidVal.Value);
				process.Kill(entireProcessTree: true);
				process.WaitForExit(3000);
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
