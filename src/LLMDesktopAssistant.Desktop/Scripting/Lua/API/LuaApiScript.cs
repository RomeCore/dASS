using System.Collections.Immutable;
using AsyncLua;
using AsyncLua.Values;
using LLMDesktopAssistant.Desktop.Scripting.ExternalProcess;
using LLMDesktopAssistant.Desktop.Scripting.Lua;
using LLMDesktopAssistant.LLM.Services;
using LLMDesktopAssistant.Scripting;
using LLMDesktopAssistant.Scripting.Lua;
using LLMDesktopAssistant.Scripting.Lua.API;
using LLMDesktopAssistant.StructuredValues;
using LLMDesktopAssistant.StructuredValues.Converters;

namespace LLMDesktopAssistant.Desktop.Scripting.Lua.API
{
	/// <summary>
	/// Lua API for the out-of-process script languages: <c>script.&lt;language&gt;.*</c>.
	/// </summary>
	/// <remarks>
	/// One namespace holds one subtable per language this platform can run. The body is written to a temporary file and
	/// executed inside the chat's addon environment, so a python snippet sees the same venv an addon does.
	/// </remarks>
	[LuaApi(chatScoped: true)]
	public class LuaApiScript(
		ExternalProcessEngineRegistry registry,
		IExternalProcessAvailabilityService availability,
		IChatSettingsService chatSettings,
		IServiceProvider services) : LuaApiBase
	{
		private const string namespaceName = "script";
		private const int defaultTimeoutSeconds = 300;
		private const int maximumErrorLength = 4000;
		private const string timeoutOption = "timeout_seconds";

		/// <inheritdoc/>
		public override string? Namespace => namespaceName;

		/// <inheritdoc/>
		public override string? Manuals => """
			--- script - out-of-process script languages

			Each language this machine can run is a subtable of `script`: $SCRIPT_ENGINES$.
			A language that is not listed there cannot run.

			The body runs from a temporary file inside the chat's addon environment (the python
			venv included) and with the chat's working directory.

			FUNCTIONS (each language):

			--- async script.<lang>.exec(code, [variables], [pattern], [options]) -> table, error
			  Runs the code and returns the JSON it printed.

			  Parameters:
			    - code: string - the script body.
			    - variables: table (optional) - globals assigned before the body, by name.
			      Python and PowerShell receive native values (dict/list/number); bash and batch
			      receive JSON text. Names must be identifiers and may not collide with the names
			      the language's own preamble uses (os, sys, PATH, ...).
			    - pattern: string (optional) - for example "@@*@@". The JSON is then taken between
			      the first and the last marker, so the body may print other lines. Without a
			      pattern the whole standard output must be the JSON.
			    - options: table (optional) - only { timeout_seconds = N } (default 300).

			  Returns: table - the parsed JSON, or nil plus the reason. A non-zero exit code, an
			  unparsable promise or a timeout is that second value, not an exception. Cancellation
			  (Stop, closed chat) and a malformed argument are exceptions.

			--- script.<lang>.run_terminal(code, [variables]) -> terminal, error
			  Runs the code in an interactive terminal and returns a handle WITHOUT waiting.
			  Attach it with msg:append_data(term). The handle is the one process.run_terminal
			  returns: pid, is_running, exit_code, text(), write(), write_line(), kill(), and
			  `await term:wait()` for the exit code. There is no JSON channel and no timeout here.

			--- script.<lang>.available() -> boolean
			  Whether the interpreter was found. Cached after the first call.

			--- script.<lang>.version() -> string or nil
			  What the interpreter prints for its version, or nil when it is unavailable.

			A prompt context runs on every prompt build, so a process started there is a process
			per request.

			EXAMPLES:

			  local err, r = await script.python.exec([[
			  import json
			  print(json.dumps({"sum": sum(xs), "n": len(xs)}))
			  ]], { xs = {1, 2, 3} })
			  if err then print("failed:", err) else print(r.sum, r.n) end

			  -- the body may print around the payload
			  local e, r2 = await script.python.exec([[
			  import json
			  print("debug")
			  print("@@", json.dumps({"ok": True}), "@@")
			  ]], nil, "@@*@@")

			  -- an interactive session
			  local term, terr = script.powershell.run_terminal("Write-Host 'hello'; Read-Host")
			  if term then msg:append_data(term) else print(terr) end
			""".Replace("$SCRIPT_ENGINES$", string.Join(", ", registry.LanguageNames.Select(name => $"{namespaceName}.{name}")));

		/// <inheritdoc/>
		public override Action? Populate(LuaTable globals, LuaTable ns, LuaService luaService)
		{
			foreach (var name in registry.LanguageNames)
			{
				if (registry.ResolveLanguage(name) is not { } language)
					continue;

				var table = luaService.ResolveNamespace($"{namespaceName}.{name}");
				table["exec"] = new LuaCallbackFunction(
					(ctx, args) => ExecAsync(language.Engine, name, ctx, args));
				table["run_terminal"] = new LuaCallbackFunction(
					(ctx, args) => RunTerminal(language.Engine, name, ctx, args));
				// isAsync: false - an auto-await: the caller writes available(), not await available().
				table["available"] = new LuaCallbackFunction(
					(ctx, _) => AvailableAsync(language.Descriptor, ctx), isAsync: false);
				table["version"] = new LuaCallbackFunction(
					(ctx, _) => VersionAsync(language.Descriptor, ctx), isAsync: false);

				// The manual lives on `script`; the subtable points at the same text so manuals(script.python) works.
				table.Set(LuaVariables.NamespaceManuals, ManualsTable());
			}

			return null;
		}

		private LuaTable ManualsTable()
		{
			var manuals = new LuaTable();
			if (Manuals is not null)
				manuals.Append(new LuaString(Manuals));

			return manuals;
		}

		private async Task<LuaTuple> ExecAsync(IExternalProcessScriptEngine engine, string language,
			LuaCallingContext ctx, LuaValue[] args)
		{
			var function = $"{namespaceName}.{language}.exec";
			var code = ReadCode(function, args);
			var variables = ReadVariables(args.Length > 1 ? args[1] : LuaNil.Instance);
			var (outputMode, pattern) = ReadPattern(args.Length > 2 ? args[2] : LuaNil.Instance);
			var timeout = ReadTimeout(args.Length > 3 ? args[3] : LuaNil.Instance);

			ExternalProcessScriptResult result;
			try
			{
				result = await engine.RunAsync(new ExternalProcessScriptParameters
				{
					Kind = ScriptableAddonKind.None,
					Body = code,
					Variables = variables,
					InputMode = ScriptInputMode.Auto,
					OutputMode = outputMode,
					OutputPattern = pattern,
					ProcessName = $"{namespaceName}.{language}",
					TimeOut = TimeSpan.FromSeconds(timeout)
				}, services, ctx.CancellationToken);
			}
			catch (OperationCanceledException)
			{
				throw;
			}
			catch (Exception ex)
			{
				// A rejected variable name is the author's mistake, so it is an exception like any bad argument.
				throw new LuaRuntimeException(ex.Message);
			}

			if (result.Status is ExternalProcessStatus.Cancelled)
				throw new OperationCanceledException(ctx.CancellationToken);

			if (result.Status is not ExternalProcessStatus.Succeeded)
				return new LuaTuple(LuaNil.Instance, DescribeFailure(result));

			return new LuaTuple(result.Json.ToConstNodeValue().ToLuaValue());
		}

		private LuaTuple RunTerminal(IExternalProcessScriptEngine engine, string language,
			LuaCallingContext ctx, LuaValue[] args)
		{
			var function = $"{namespaceName}.{language}.run_terminal";
			var code = ReadCode(function, args);
			var variables = ReadVariables(args.Length > 1 ? args[1] : LuaNil.Instance);

			ExternalProcessScriptStartResult start;
			try
			{
				start = engine.StartInTerminal(new ExternalProcessScriptTerminalParameters
				{
					Kind = ScriptableAddonKind.None,
					Body = code,
					Variables = variables,
					InputMode = ScriptInputMode.Auto,
					ProcessName = $"{namespaceName}.{language}",
					Wait = false
				}, services, ctx.CancellationToken);
			}
			catch (OperationCanceledException)
			{
				throw;
			}
			catch (Exception ex)
			{
				throw new LuaRuntimeException(ex.Message);
			}

			if (!start.Started)
				return new LuaTuple(LuaNil.Instance, new LuaString(start.Error ?? "The script did not start."));

			return new LuaTuple(LuaTerminal.Wrap(new LuaTerminal(start.Process!)));
		}

		private async Task<LuaTuple> AvailableAsync(IScriptEngineDescriptor descriptor, LuaCallingContext ctx)
		{
			var check = await CheckAsync(descriptor, ctx);
			return new LuaTuple(LuaBoolean.FromBoolean(check.Available));
		}

		private async Task<LuaTuple> VersionAsync(IScriptEngineDescriptor descriptor, LuaCallingContext ctx)
		{
			var check = await CheckAsync(descriptor, ctx);
			return new LuaTuple(check.Version is null ? LuaNil.Instance : new LuaString(check.Version));
		}

		private Task<ScriptEngineAvailability> CheckAsync(IScriptEngineDescriptor descriptor, LuaCallingContext ctx)
			=> availability.CheckAsync(descriptor.Language, chatSettings.Settings.Environment, ctx.CancellationToken);

		private static string ReadCode(string function, LuaValue[] args)
		{
			if (args.Length < 1 || args[0] is not LuaString code)
				throw new LuaRuntimeException($"{function}(code, ...): the first argument must be the script text.");

			return code.Value;
		}

		private static ImmutableDictionary<string, INodeValue?> ReadVariables(LuaValue value)
		{
			if (value is LuaNil)
				return ImmutableDictionary<string, INodeValue?>.Empty;

			if (value is not LuaTable table)
				throw new LuaRuntimeException("Variables must be a table of name -> value.");

			var variables = ImmutableDictionary.CreateBuilder<string, INodeValue?>();
			foreach (var entry in table.Entries)
			{
				if (entry.Key is not LuaString name)
					throw new LuaRuntimeException("Variable names must be strings.");

				variables[name.Value] = entry.Value.ToConstNodeValue(tolerant: true);
			}

			return variables.ToImmutable();
		}

		private static (ScriptOutputMode Mode, string? Pattern) ReadPattern(LuaValue value)
		{
			if (value is LuaNil)
				return (ScriptOutputMode.Whole, null);

			if (value is not LuaString pattern)
				throw new LuaRuntimeException("The output pattern must be a string like \"@@*@@\".");

			return (ScriptOutputMode.Pattern, pattern.Value);
		}

		private static int ReadTimeout(LuaValue value)
		{
			if (value is LuaNil)
				return defaultTimeoutSeconds;

			if (value is not LuaTable options)
				throw new LuaRuntimeException("Options must be a table.");

			var timeout = defaultTimeoutSeconds;
			foreach (var entry in options.Entries)
			{
				if (entry.Key is not LuaString key || key.Value != timeoutOption)
					throw new LuaRuntimeException($"Unknown option '{entry.Key}'.");

				if (entry.Value is not LuaNumber seconds)
					throw new LuaRuntimeException($"Option '{timeoutOption}' must be a number.");

				timeout = Math.Max(1, (int)seconds.Value);
			}

			return timeout;
		}

		private static LuaString DescribeFailure(ExternalProcessScriptResult result)
		{
			// The exit code and stderr are what a Lua author can act on; the engine's own message names the internal
			// command line, which is noise (and a path leak) from here.
			if (result.Status is ExternalProcessStatus.ProcessFailed && result.ExitCode is { } exitCode)
			{
				var stderr = Tail(result.Stderr);
				return new LuaString(stderr.Length > 0
					? $"exit code {exitCode}: {stderr}"
					: $"exit code {exitCode} with no output on standard error.");
			}

			var message = Tail(result.Error);
			return new LuaString(message.Length > 0 ? message : "The script failed.");
		}

		private static string Tail(string? text)
		{
			var value = text?.Trim() ?? string.Empty;
			return value.Length <= maximumErrorLength ? value : value[^maximumErrorLength..];
		}
	}
}
