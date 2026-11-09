using AsyncLua;
using AsyncLua.Values;
using LLMDesktopAssistant.LLM.Services;
using LLMDesktopAssistant.Scripting.Lua.Rpc;
using LLMDesktopAssistant.Utils.Rpc;
using Serilog;

namespace LLMDesktopAssistant.Scripting.Lua.API
{
	/// <summary>
	/// Lua API for JSON-RPC: the <c>rpc</c> namespace.
	/// </summary>
	/// <remarks>
	/// The namespace is a thin adapter over <see cref="Utils.Rpc"/>: transports come from registered
	/// providers, so this file knows no transport by name and the core knows nothing about Lua.
	/// </remarks>
	[LuaApi(chatScoped: true)]
	public class LuaApiRpc(
		IEnumerable<IRpcTransportProvider> providers,
		LuaRpcHandleRegistry registry,
		IWorkingDirectoryAccessService workingDirectory) : LuaApiBase
	{
		private const int defaultStderrLimit = LuaRpcStandardError.DefaultLimit;

		/// <inheritdoc />
		public override string? Namespace => "rpc";

		/// <inheritdoc />
		public override string? Manuals => """
			--- rpc - JSON-RPC 2.0 over a long-lived connection

			Speaks JSON-RPC to another process or service and keeps the connection open, so the peer can
			answer later and can also call YOU. That second direction is why a routing table is passed to
			connect: a server asking `workspace/configuration` or `sampling/createMessage` is normal, and a
			client that ignores such a request hangs the server.

			TRANSPORTS: see rpc.transports(); a name that is not in that list cannot be used.
			  stdio - spawns a process and talks over its standard streams
			  tcp   - connects to a listening server; the target is "host:port"
					  (an IPv6 literal goes in brackets: "[::1]:9333")

			A transport that starts nothing has no pid, no exit_code and no stderr; on tcp those
			properties read nil, handle:stderr() returns an empty buffer, and close() closes the
			socket instead of killing a process.

			FUNCTIONS:

			--- rpc.transports() -> array of strings
			  The transport names this platform can open, e.g. {"stdio"}.

			--- async rpc.connect(transport, target, routing, [options]) -> handle, error
			  Opens a connection and returns a handle. A failure returns nil plus the reason; a malformed
			  argument throws.

			  target: for stdio a command line as a string ("srv --json --flag") or an array of arguments
			  {"srv", "--json", "--flag"}; the array is the canonical form. In the string form only the
			  executable is parsed - it is the first token, and a path with spaces needs quotes - while
			  everything after it is handed to the process VERBATIM, so the quoting you write is what the
			  program parses. For tcp the target is "host:port" and the array form is rejected.

			  routing: a table mapping a method name to a handler. Keys are method names as the peer sends
			  them - use ["textDocument/publishDiagnostics"] for names that are not identifiers. One
			  positional element is allowed:
				routing[1] = function(method, params) ... end   -- every method without its own key
			  A named handler is called as function(params); the fallback as function(method, params).

			  The table is read on EVERY message, not copied: assign a key later to subscribe
			  (routing["x"] = function(params) ... end) or clear it to unsubscribe.

			  Return a value to answer a request with a result. Returning nothing answers null. Throw to
			  answer with an error: throwing a table { code = -32601, message = "...", data = {...} } sends
			  exactly that error; anything else becomes -32603 with the thrown text. A request no handler
			  covers is answered -32601 automatically, which is what keeps a peer from waiting forever.

			  options:
			    cwd               string  - working directory of the peer (default: the chat's)
			    env               table   - extra environment variables; 0 value (number) removes one
			    framing           string  - "auto" (default), "content_length" or "lines"
			    stderr                    - nil, a file path, a table or a function (see below)
			    stderr_limit      number  - lines kept by a buffer or table sink (default 1000, 0 = unlimited)
			    invoke_timeout_ms number  - default timeout of a request; absent means none

			  FRAMING: `content_length` is the LSP dialect (a Content-Length header block), `lines` is one
			  JSON object per line (the MCP dialect). `auto` detects the dialect from the first message the
			  peer sends - so it is exact for a peer that speaks first, and a client that sends first is
			  assumed to speak content_length until the peer says otherwise. An LSP server ignores anything
			  else, so if the server never speaks first, state the framing explicitly.

			  STDERR is always drained, because a peer whose diagnostic pipe fills up stops answering. Where
			  the lines go:
			    nil        -> a ring buffer on the handle; read it with handle:stderr()
			    "path"     -> appended to a file (relative paths resolve against the chat's working directory)
			    { }        -> appended to the given table as array items, up to stderr_limit lines
			    function   -> called with each line, may be async
			  The first three are free of side effects on the protocol; the callback runs on the drain task,
			  not inside the read loop, so a slow callback delays stderr and not the conversation.

			HANDLE (an rpc_handle userdata):

			  is_alive   boolean       - false once the peer ended or close() ran
			  exit_code  number or nil - the peer's process exit code, nil while it runs
			  pid        number or nil - the peer's process id, nil for a transport without a process
			  framing    string        - "content_length" or "lines", the dialect actually in use
			  invoke     rpc_invoker   - the request proxy: h.invoke:method(x)
			  notify     rpc_notifier  - the notification proxy: h.notify:method(x)
			  :stderr() -> string, dropped | nil, reason
			  async :close() -> boolean  ends the connection and kills the peer's process tree;
			                             true when it closed it, false when it was already closed

			invoke, notify and close all wait for something, so all of them need await. An unawaited
			notify may also be written after the request that follows it, because the write order is only
			guaranteed within a task. stderr() is synchronous: it reads a buffer.

			REQUESTS AND NOTIFICATIONS ('rpc_invoker' / 'rpc_notifier'):

			  Both proxies take a method name the same three ways, and both drop the proxy from the
			  arguments before the handler sees them:

			  local r = await h.invoke:mymethod({ a = 1 })     -- asks, then waits for the answer
			  local r = await h.invoke("some.method", { })     -- the same, for names that are not identifiers

			  await h.notify:mymethod({ a = 1 })               -- sends, then waits only for the write
			  await h.notify("some.method", { })

			  A result is decoded JSON: a table, a string, a number, a boolean or nil for JSON null.

			  An error is a Lua error whose VALUE is a table - use try/catch or pcall:
				try
				  local r = await h.invoke:mymethod()
				catch e
				  print(e.code, e.message)   -- e.data holds the payload, if the peer sent any
				end

			  A timeout is a plain error message, not a table. The connection dying is -32000.

			LIFETIME:

			  A handle owns its connection: nothing is shared between handles, so close() always ends the
			  peer. The chat-scoped registry closes whatever a script forgot when the chat dies, but it
			  cannot reopen it, so a handle kept in store.session across a script reload is still the same
			  live connection - do NOT close it in the script's cleanup block.

			  There is no auto-reconnect. When the peer dies, is_alive turns false, pending requests fail
			  with -32000 and close() reports false; connecting again is a new rpc.connect.

			  Script loading is synchronous, so `await rpc.connect(...)` at file scope is impossible.
			  Connect inside a function that is invoked from a tool or a command, or make the first use
			  connect lazily and cache the handle in store.session.

			EXAMPLES:

			  -- A calculator server, with an unrouted-method fallback
			  local h, err = await rpc.connect("stdio", { "calc-server", "--json" }, {
			    add = function(p) return p.a + p.b end,
			    ["system.ping"] = function(p) return "pong" end,
			    function(method, p)
			      print("unrouted:", method)
			      error({ code = -32601, message = "no such method: " .. method })
			    end
			  })
			  if not h then print("connect failed:", err) return end

			  print(await h.invoke:add({ a = 2, b = 3 }))
			  await h.notify:client_ready({ version = 1 })
			  print("closed:", await h:close())

			  -- Long-lived, survives a script reload
			  local lsp = store.session.get("lsp")
			  if not lsp then
			    lsp = await rpc.connect("stdio", { "typescript-language-server", "--stdio" }, {
			      ["textDocument/publishDiagnostics"] = function(p)
			        store.session.set("diagnostics", p.diagnostics)
			      end,
			      ["workspace/configuration"] = function(p)
			        return {}                                   -- one empty object per requested item
			      end
			    }, { framing = "content_length" })
			    store.session.set("lsp", lsp)
			  end

			  -- stderr to a file next to the chat
			  local h2 = await rpc.connect("stdio", "myserver --verbose", {}, { stderr = "rpc-server.log" })
			  print(h2:stderr())   -- nil, "stderr is redirected"
			  await h2:close()
			""";

		/// <inheritdoc />
		public override Action? Populate(LuaTable globals, LuaTable ns, LuaService luaService)
		{
			ns["transports"] = new LuaCallbackFunction(Transports);
			ns["connect"] = new LuaCallbackFunction(ConnectAsync);
			return null;
		}

		private LuaTuple Transports(LuaCallingContext ctx, LuaValue[] args)
		{
			var result = new LuaTable();
			var index = 1;

			foreach (var provider in providers)
				result[index++] = new LuaString(provider.Name);

			return new LuaTuple(result);
		}

		private async Task<LuaTuple> ConnectAsync(LuaCallingContext ctx, LuaValue[] args)
		{
			if (args.Length < 3)
			{
				throw new LuaRuntimeException(
					"rpc.connect(transport, target, routing, [options]): at least 3 arguments expected.");
			}

			if (args[0] is not LuaString transportName)
				throw new LuaRuntimeException("rpc.connect(): the first argument must be a string (transport name).");

			var provider = providers.FirstOrDefault(p =>
				string.Equals(p.Name, transportName.Value, StringComparison.OrdinalIgnoreCase));

			if (provider is null)
			{
				var available = string.Join(", ", providers.Select(p => p.Name));
				throw new LuaRuntimeException(
					$"rpc.connect('{transportName.Value}'): no such transport. Available: {(available.Length == 0 ? "none" : available)}.");
			}

			var (target, arguments, commandLine) = ParseTarget(args[1]);

			if (args[2] is not LuaTable routing)
				throw new LuaRuntimeException("rpc.connect(): the third argument must be a table (method routing).");

			var options = args.Length > 3 && args[3] is LuaTable optionTable ? optionTable : null;

			var directory = ReadDirectory(options);
			var environment = ReadEnvironment(options);
			var framing = ReadFraming(options);
			var stderrLimit = ReadStderrLimit(options);
			var timeout = ReadTimeout(options);

			// Built before connecting: a sink that cannot be created must not leave a process behind.
			var standardError = LuaRpcStandardError.Create(options?.Get("stderr"), stderrLimit, workingDirectory);

			var request = new RpcTransportRequest
			{
				Target = target,
				Arguments = arguments,
				CommandLine = commandLine,
				WorkingDirectory = directory,
				Environment = environment
			};

			if (Log.IsEnabled(Serilog.Events.LogEventLevel.Information))
			{
				Log.Information(
					"Lua rpc.connect: {Transport} '{File}' {Arguments} in {Directory}",
					provider.Name,
					target,
					commandLine ?? string.Join(' ', arguments),
					directory);
			}

			try
			{
				var transport = await provider.ConnectAsync(request, ctx.CancellationToken).ConfigureAwait(false);

				try
				{
					var router = new LuaRpcRouter(routing, ctx);
					var handle = new LuaRpcHandle(transport, router, registry, standardError, framing, timeout, ctx);
					registry.Register(handle);

					return new LuaTuple(LuaRpcHandle.Wrap(handle));
				}
				catch
				{
					// The handle owns the transport; without a handle, nothing else will close it.
					await transport.DisposeAsync().ConfigureAwait(false);
					throw;
				}
			}
			catch (OperationCanceledException)
			{
				throw;
			}
			catch (LuaRuntimeException)
			{
				throw;
			}
			catch (Exception ex)
			{
				return new LuaTuple(LuaNil.Instance, new LuaString(ex.Message));
			}
		}

		/// <summary>
		/// Reads the target: an executable plus its verbatim command line, or an argument array.
		/// </summary>
		/// <param name="target">The second argument of <c>rpc.connect</c>.</param>
		/// <returns>The executable, the argument array and the raw command line of the string form.</returns>
		private static (string Target, IReadOnlyList<string> Arguments, string? CommandLine) ParseTarget(LuaValue target)
		{
			if (target is LuaString commandLine)
			{
				string executable;
				string arguments;

				try
				{
					(executable, arguments) = RpcCommandLine.SplitExecutable(commandLine.Value);
				}
				catch (FormatException ex)
				{
					throw new LuaRuntimeException($"rpc.connect(): {ex.Message}");
				}

				if (executable.Length == 0)
					throw new LuaRuntimeException("rpc.connect(): the target command line is empty.");

				// Only the executable is parsed: the rest goes to the peer as the caller wrote it.
				return (executable, [], string.IsNullOrWhiteSpace(arguments) ? null : arguments);
			}

			if (target is LuaTable table)
			{
				var parts = new List<string>((int)table.Length);

				for (var i = 1; i <= table.Length; i++)
				{
					if (table.Get(i) is not LuaString part)
					{
						throw new LuaRuntimeException(
							$"rpc.connect(): target[{i}] must be a string; use the array form only for arguments.");
					}

					parts.Add(part.Value);
				}

				if (parts.Count == 0)
					throw new LuaRuntimeException("rpc.connect(): the target array is empty.");

				return (parts[0], parts.Skip(1).ToArray(), null);
			}

			throw new LuaRuntimeException(
				"rpc.connect(): the second argument must be a command line string or an array of arguments.");
		}

		private string ReadDirectory(LuaTable? options)
		{
			return options?.Get("cwd") is LuaString cwd
				? workingDirectory.GetFullPath(cwd.Value)
				: workingDirectory.GetWorkingDirectory();
		}

		private static Dictionary<string, string?> ReadEnvironment(LuaTable? options)
		{
			var result = new Dictionary<string, string?>(StringComparer.Ordinal);

			if (options?.Get("env") is not LuaTable environment)
				return result;

			foreach (var entry in environment.Entries)
			{
				if (entry.Key is not LuaString name || name.Value.Length == 0)
					continue;

				result[name.Value] = entry.Value switch
				{
					LuaString value => value.Value,
					LuaNumber number when (number.Value == 0) => null,
					LuaNumber number => number.Value.ToString(System.Globalization.CultureInfo.InvariantCulture),
					LuaBoolean flag => flag.Value ? "true" : "false",
					_ => throw new LuaRuntimeException(
						$"rpc.connect(): options.env['{name.Value}'] must be a string, a number, a boolean or nil.")
				};
			}

			return result;
		}

		private static RpcFraming ReadFraming(LuaTable? options)
		{
			if (options?.Get("framing") is not LuaString framing)
				return RpcFraming.Auto;

			return framing.Value.ToLowerInvariant() switch
			{
				"auto" => RpcFraming.Auto,
				"content_length" or "content-length" => RpcFraming.ContentLength,
				"lines" or "line" or "jsonl" => RpcFraming.Lines,
				_ => throw new LuaRuntimeException(
					$"rpc.connect(): options.framing must be 'auto', 'content_length' or 'lines', not '{framing.Value}'.")
			};
		}

		private static int ReadStderrLimit(LuaTable? options)
		{
			if (options?.Get("stderr_limit") is not LuaNumber limit)
				return defaultStderrLimit;

			return limit.Value <= 0 ? 0 : (int)Math.Min(limit.Value, int.MaxValue);
		}

		private static TimeSpan? ReadTimeout(LuaTable? options)
		{
			if (options?.Get("invoke_timeout_ms") is not LuaNumber timeout)
				return null;

			if (timeout.Value <= 0)
			{
				throw new LuaRuntimeException(
					"rpc.connect(): options.invoke_timeout_ms must be positive; omit it for no timeout.");
			}

			return TimeSpan.FromMilliseconds(timeout.Value);
		}
	}
}
