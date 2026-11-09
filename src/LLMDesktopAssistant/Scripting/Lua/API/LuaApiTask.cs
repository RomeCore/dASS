using AsyncLua;
using AsyncLua.Values;

namespace LLMDesktopAssistant.Scripting.Lua.API
{
	[LuaApi(chatScoped: true)]
	public class LuaApiTask : LuaApiBase
	{
		public override string? Namespace => "task";

		public override string? Manuals => """
			--- task - extended task utilities

			Supplements the AsyncLua `task` library, which keeps its own scheduling functions:
			task.delay, task.run, task.pararun and task.create (see the `asynclua` manuals). This part
			adds cancellation - a handle you control from Lua - and the functions that decide WHICH
			token a piece of Lua code runs under, so a call can be detached from the script's own
			cancellation or bound to a handle of your own.

			Every script runs under a token owned by the host: Stop, a closed chat or a cancelled tool
			fires it. That token is what the functions here swap.

			FUNCTIONS:

			--- task.create_cancellation([...]) -> cancellation
			  Creates a cancellation handle: a token you control from Lua.

			  Parameters (any number, all optional):
				- "linked": string - the literal "linked" links the handle to the token of the call
				  that creates it, so the handle fires when the host cancels this script.
				- a `cancellation` handle - links to it, so this handle fires when the other one does.

			  With no arguments the handle is independent: only its own cancel()/cancel_after() fires it.

			  Returns: cancellation - the handle (see CANCELLATION HANDLE below).

			  Throws if an argument is neither the string "linked" nor a cancellation handle.

			--- async task.without_cancellation(func, [args...]) -> ...
			  Calls func under a token that is never cancelled: the host's Stop no longer reaches this
			  call, neither between instructions nor inside an await.

			  Parameters:
				- func: function - the function to call.
				- args...: any (optional) - its arguments, passed positionally.

			  Returns: a task holding whatever func returns - always await it. The API itself is
			  asynchronous, so how func is declared changes nothing: a missing await hands you a task
			  where a value was expected.

			  Use it for work that must finish once started: a tool call, an upload, a commit.

			--- async task.with_cancellation(func, cancellation, [args...]) -> ...
			  Calls func under the token of the given handle.

			  Parameters:
				- func: function - the function to call.
				- cancellation: cancellation - the handle whose token the call runs under.
				- args...: any (optional) - its arguments, passed positionally.

			  Returns: a task holding whatever func returns, like without_cancellation.

			  The token REPLACES the caller's one, it does not link: the host's Stop is invisible
			  inside this call. Pass task.create_cancellation("linked") when both must stay in force.

			--- async task.with_context(func, context, [args...]) -> ...
			  Calls func under a copy of the current context, with the parts the context table names
			  overridden. A key left out (or nil) keeps the caller's behaviour.

			  Parameters:
				- func: function - the function to call.
				- context: table - what to override:
					print: function(input) - the sink behind the global print() of this call. A plain
					  function; it runs in the caller's context, so a print inside it does not call
					  itself.
					warn: function(input) - the same for warn(). When only print is given, warn()
					  falls back to it, exactly as the host does.
					globals: table - this table becomes the environment of the call and its _G, so
					  every global the body reads resolves here. The environment is FULLY isolated:
					  builtins (print, table, pairs...), dass.* and every other API are absent unless
					  the table carries them, and assignments land in the table. Only Lua code is
					  affected - a native function that brought an environment of its own keeps it.
					cancellation: cancellation - the token the call runs under. 0 erases cancellation
					  for the call, which is a different request from nil: nil means "do not override"
					  and keeps the caller's token. Any other value throws.
				- args...: any (optional) - its arguments, passed positionally.

			  Returns: a task holding whatever func returns, like without_cancellation.

			  The overrides live for this call only: they are copied into a clone of the context, and
			  every call the body makes inherits them.

			CANCELLATION HANDLE ('cancellation'):

			  Properties (read-only):
				- cancellable: boolean - true while the handle has not fired
				- cancellation_requested: boolean - true once it fired

			  Methods:
				- c:cancel() -> boolean - fires it now; false when it had already fired or was closed
				- c:cancel_after(ms) - fires it after the given milliseconds
				- c:close() - fires it and releases it; later cancel calls return false

			  Handles are chat-scoped: the chat closes whatever a script left open when it ends.

			WHAT A FIRE DOES:

			  - The token is checked before every Lua instruction, so a cancel interrupts a tight
				compute loop as well as an await.
			  - A pending await is raced against the token, so a long call is abandoned mid-flight.
			  - It is a cancellation, NOT a Lua error: `try/catch` and `pcall` never see it - the script
				simply stops and the host reports the cancellation. So a cancel_after deadline is a
				guard, not a handleable timeout - time.with_timeout is the one that reports it as a value.

			NOTES:
			  - A handle does nothing on its own: only code that runs INSIDE a call made with its token
				checks it, so a handle nobody bound to a call is just a flag.
			  - without_cancellation hides the host token from that call only; the rest of the script
				stays cancellable, and a nested async call inherits the detached token of its caller.
			  - A call cannot be entered with a token that already fired: the body never starts, the
				script stops at the call site.
			  - A handle linked with "linked" can never be observed by the script it belongs to: the
				host fires the token and aborts that script at the same moment. Read the flag on a
				handle you cancel yourself, or on one handed to a different call.

			EXAMPLES:

			  -- A tool call that must finish once started: Stop no longer reaches it
			  local r = await task.without_cancellation(dass.tool.call, "fs-write_file",
				{ path = "report.md", content = report })
			  if not r.success then print(r.content) end

			  -- A call under a token of your own, with its own deadline; the script stops together
			  -- with the deadline, so it guards the work instead of reporting a failure
			  local session = task.create_cancellation("linked")
			  session:cancel_after(30000)
			  local rows = await task.with_cancellation(collect_rows, session)

			  -- Capture print() and declare the environment the body may assume
			  local lines = {}
			  task.with_context(render, {
				print = function(line) table.insert(lines, line) end,
				globals = { table = table, string = string, pairs = pairs }
			  })

			  -- A call that can no longer be stopped, spelled with the context table
			  task.with_context(job, { cancellation = 0 })
			""";

		private readonly List<LuaCancellation> _cancellations = [];
		private readonly Lock _cancellationsLock = new();

		protected override void Dispose(bool disposing)
		{
			lock (_cancellationsLock)
				foreach (var cancellation in _cancellations)
					cancellation.Dispose();
		}

		public override Action? Populate(LuaTable globals, LuaTable ns, LuaService luaService)
		{
			ns["create_cancellation"] = new LuaCallbackFunction(CreateCancellation);
			ns["without_cancellation"] = new LuaCallbackFunction(WithoutCancellation);
			ns["with_cancellation"] = new LuaCallbackFunction(WithCancellation);
			ns["with_context"] = new LuaCallbackFunction(WithContext);

			return null;
		}

		private LuaTuple CreateCancellation(LuaCallingContext ctx, LuaValue[] args)
		{
			List<CancellationToken> tokens = [];
			foreach (var arg in args)
			{
				if (arg is LuaString str && str.Value == "linked")
					tokens.Add(ctx.CancellationToken);
				else if (arg is LuaUserData userData && userData.Target is LuaCancellation linkedCancellation)
					tokens.Add(linkedCancellation.Cts.Token);
				else
					throw new LuaRuntimeException("task.create_cancellation([cancellations...]): expected a 'linked' string or 'cancellation' handle.");
			}

			// CreateLinkedTokenSource rejects an empty set, so an independent handle gets a plain source.
			var cancellation = new LuaCancellation(tokens.Count == 0
				? new CancellationTokenSource()
				: CancellationTokenSource.CreateLinkedTokenSource(tokens.ToArray()));
			_cancellations.Add(cancellation);
			return new LuaTuple(LuaCancellation.Wrap(cancellation));
		}

		private static Task<LuaTuple> WithoutCancellation(LuaCallingContext ctx, LuaValue[] args)
		{
			if (args.Length < 1)
				throw new LuaRuntimeException("task.without_cancellation(func, [args...]): at least 1 argument expected.");

			if (args[0] is not LuaFunction func)
				throw new LuaRuntimeException("task.without_cancellation(func, [args...]): function expected.");

			var newCtx = ctx.WithoutCancellation();
			return func.InvokeAsync(newCtx, args[1..]);
		}

		private static Task<LuaTuple> WithCancellation(LuaCallingContext ctx, LuaValue[] args)
		{
			if (args.Length < 2)
				throw new LuaRuntimeException("task.with_cancellation(func, cancellation, [args...]): at least 2 argument expected.");

			if (args[0] is not LuaFunction func)
				throw new LuaRuntimeException("task.with_cancellation(func, cancellation, [args...]): argument 1: function expected.");

			if (args[1] is not LuaUserData cancellationUserData || cancellationUserData.Target is not LuaCancellation cancellation)
				throw new LuaRuntimeException("task.with_cancellation(func, cancellation, [args...]): argument 2: cancellation expected.");

			var newCtx = ctx.WithCancellation(cancellation.Cts.Token);
			return func.InvokeAsync(newCtx, args[2..]);
		}

		private static Task<LuaTuple> WithContext(LuaCallingContext ctx, LuaValue[] args)
		{
			if (args.Length < 2)
				throw new LuaRuntimeException("task.with_context(func, context, [args...]): at least 2 arguments expected.");

			if (args[0] is not LuaFunction func)
				throw new LuaRuntimeException("task.with_context(func, context, [args...]): argument 1: function expected.");

			if (args[1] is not LuaTable context)
				throw new LuaRuntimeException("task.with_context(func, context, [args...]): argument 2: table expected.");

			var newCtx = ctx.Clone();

			// The sinks run in the caller's context, so a print inside a sink does not call itself.
			if (context.Get("print") is LuaFunction printSink)
				newCtx.Print = input => printSink.Invoke(ctx, new LuaString(input));

			if (context.Get("warn") is LuaFunction warnSink)
				newCtx.Warn = input => warnSink.Invoke(ctx, new LuaString(input));

			if (context.Get("globals") is LuaTable globals)
			{
				// _G inside the isolated table points at itself, so `_G.x = 1` still works.
				globals.Set(LuaVariables.GlobalTable, globals);
				newCtx.Globals = globals;
			}

			var cancellation = context.Get("cancellation");
			if (cancellation is LuaNumber number)
			{
				if (number.Value != 0)
					throw new LuaRuntimeException("task.with_context(func, context, [args...]): 'cancellation' must be a cancellation handle, or 0 to erase cancellation.");
				newCtx.CancellationToken = default;
			}
			else if (cancellation is LuaUserData { Target: LuaCancellation cancellationHandle })
			{
				newCtx.CancellationToken = cancellationHandle.Cts.Token;
			}
			else if (cancellation is not LuaNil)
			{
				throw new LuaRuntimeException("task.with_context(func, context, [args...]): 'cancellation' must be a cancellation handle, or 0 to erase cancellation.");
			}

			return func.InvokeAsync(newCtx, args[2..]);
		}
	}
}
