using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using AsyncLua;
using AsyncLua.Values;
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
			--- process — process execution API

			Provides command execution with timeout, output capture,
			path resolution, and basic process management.

			FUNCTIONS:

			--- async process.exec(command, [args], [options])
			  Executes a command and waits for completion.
			  Returns a table with the execution result.

			  Parameters:
			    - command: string — The command or executable to run (e.g. "git", "node").
			      On Windows, also searches PATHEXT for executables.
			    - args: string or table (optional) — Arguments for the command.
			      As string: "status --short" (quoted tokens supported: '--grep "fix bug"').
			      As table: {"status", "--short"}.
			      If omitted, the command runs with no arguments.
			    - options: table (optional) — Execution options:
			      - cwd: string — Working directory (default: current directory)
			      - env: table — Additional environment variables as key-value pairs
			      - stdin: string — Data to write to the process standard input
			      - timeout_seconds: number (default: 30, max: 600) — Maximum execution time.
			        If exceeded, the process is killed and timed_out is true.

			  Returns: table with:
			    - exit_code: number — Process exit code (0 = success)
			    - stdout: string — Standard output of the command
			    - stderr: string — Standard error of the command
			    - timed_out: boolean — Whether the process was killed due to timeout
			    - duration_ms: number — Wall-clock execution time in milliseconds
			    - command: string — The full command that was executed (for logging)

			  Throws: If the command cannot be started or arguments are invalid.

			--- process.which(command)
			  Resolves the full path of an executable by searching the PATH
			  environment variable (and PATHEXT on Windows).
			  Returns nil if the command is not found.

			  Parameters:
			    - command: string — The command name to locate (e.g. "git", "python")

			  Returns: string or nil — Full path to the executable, or nil if not found.

			--- process.kill(pid)
			  Kills a process by its process ID. Returns true if successful.

			  Parameters:
			    - pid: number — The process ID to kill

			  Returns: boolean — Whether the process was killed.

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

			  -- Find a command
			  local git_path = process.which("git")
			  if git_path then
			    print("Git found at:", git_path)
			  end

			  -- Kill a process
			  print("Killed:", process.kill(12345))
			""";

		private readonly IWorkingDirectoryAccessService _fileAccess;

		public LuaApiProcess(IWorkingDirectoryAccessService fileAccess)
		{
			_fileAccess = fileAccess;
		}

		private const int DefaultTimeoutSeconds = 30;

		public override Action? Populate(LuaTable globals, LuaTable ns, LuaService luaService)
		{
			ns["exec"] = new LuaCallbackFunction(ExecAsync);
			ns["which"] = new LuaCallbackFunction(Which);
			ns["kill"] = new LuaCallbackFunction(Kill);
			return null;
		}

		private async Task<LuaTuple> ExecAsync(LuaCallingContext ctx, LuaValue[] args)
		{
			if (args.Length < 1)
				throw new LuaRuntimeException("process.exec(command, [args], [options]): at least 1 argument expected.");

			if (args[0] is not LuaString commandVal)
				throw new LuaRuntimeException("process.exec(): first argument must be a string (command).");

			var command = commandVal.Value;
			var cmdArgs = Array.Empty<string>();
			string? cwd = null;
			string? stdin = null;
			int timeoutSeconds = DefaultTimeoutSeconds;
			var envVars = new Dictionary<string, string>();

			// Parse args: string or table
			if (args.Length > 1 && args[1] is not LuaNil)
			{
				if (args[1] is LuaString argsString)
				{
					cmdArgs = TokenizeArgs(argsString.Value);
				}
				else if (args[1] is LuaTable argsTable)
				{
					cmdArgs = new string[argsTable.Entries.Count()];
					int i = 0;
					foreach (var kv in argsTable.Entries.OrderBy(kv => kv.Key.TryToNumber()))
					{
						if (kv.Value is LuaString s)
							cmdArgs[i++] = s.Value;
						else
							throw new LuaRuntimeException($"process.exec(): args[{i}] must be a string.");
					}
				}
				else
				{
					throw new LuaRuntimeException("process.exec(): second argument must be a string or table (args).");
				}
			}

			// Parse options (may be at index 1 or 2 depending on whether args were provided)
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

			var resolvedCommand = ResolveCommandPath(command);
			var displayCommand = resolvedCommand ?? command;

			var psi = new ProcessStartInfo
			{
				FileName = resolvedCommand ?? command,
				RedirectStandardOutput = true,
				RedirectStandardError = true,
				RedirectStandardInput = stdin != null,
				UseShellExecute = false,
				CreateNoWindow = true,
				StandardOutputEncoding = Encoding.UTF8,
				StandardErrorEncoding = Encoding.UTF8
			};

			foreach (var arg in cmdArgs)
				psi.ArgumentList.Add(arg);

			cwd = _fileAccess.TryAccessPath(cwd ?? "", DirectoryAccessMode.Execute);
			if (string.IsNullOrEmpty(cwd))
				throw new LuaRuntimeException("process.exec(): unable to access working directory.");
			if (!Directory.Exists(cwd))
				throw new LuaRuntimeException("process.exec(): working directory does not exist.");
			psi.WorkingDirectory = cwd;

			foreach (var envVar in envVars)
				psi.Environment[envVar.Key] = envVar.Value;

			var startTime = DateTime.UtcNow;
			Process? process = null;

			try
			{
				process = Process.Start(psi);
			}
			catch (Exception ex)
			{
				return BuildErrorResult($"Failed to start process '{command}': {ex.Message}",
					displayCommand, (int)(DateTime.UtcNow - startTime).TotalMilliseconds);
			}

			if (process == null)
			{
				return BuildErrorResult($"Failed to start process: {command}", displayCommand,
					(int)(DateTime.UtcNow - startTime).TotalMilliseconds);
			}

			using (process)
			{
				using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));

				if (stdin != null)
				{
					try
					{
						await process.StandardInput.WriteAsync(stdin);
						process.StandardInput.Close();
					}
					catch
					{
						// Process may have exited before we could write
					}
				}

				var stdoutTask = process.StandardOutput.ReadToEndAsync(cts.Token);
				var stderrTask = process.StandardError.ReadToEndAsync(cts.Token);

				bool timedOut = false;

				try
				{
					await process.WaitForExitAsync(cts.Token);
				}
				catch (OperationCanceledException)
				{
					timedOut = true;
					try { process.Kill(entireProcessTree: true); } catch { /* best effort */ }
				}
				catch (Exception)
				{
					try { process.Kill(entireProcessTree: true); } catch { /* best effort */ }
				}

				string stdout;
				string stderr;

				try
				{
					stdout = await stdoutTask;
				}
				catch (OperationCanceledException)
				{
					stdout = "";
				}
				catch (IOException)
				{
					stdout = "";
				}

				try
				{
					stderr = await stderrTask;
				}
				catch (OperationCanceledException)
				{
					stderr = "";
				}
				catch (IOException)
				{
					stderr = "";
				}

				var durationMs = (int)(DateTime.UtcNow - startTime).TotalMilliseconds;
				var exitCode = timedOut ? -1 : process.ExitCode;

				var result = new LuaTable();
				result["exit_code"] = new LuaNumber(exitCode);
				result["stdout"] = new LuaString(stdout);
				result["stderr"] = new LuaString(stderr);
				result["timed_out"] = LuaBoolean.FromBoolean(timedOut);
				result["duration_ms"] = new LuaNumber(durationMs);
				result["command"] = new LuaString(displayCommand + (cmdArgs.Length > 0 ? " " + string.Join(" ", cmdArgs) : ""));

				return new LuaTuple(result);
			}
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
	}
}
