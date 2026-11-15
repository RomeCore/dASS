using AsyncLua;
using AsyncLua.Values;
using LLMDesktopAssistant.Hooks;
using LLMDesktopAssistant.Scripting.Lua.Addons;
using LLMDesktopAssistant.StructuredValues.Converters;
using LLMDesktopAssistant.Utils;

namespace LLMDesktopAssistant.Scripting.Lua.API
{
	/// <summary>
	/// Lua API for the hooks of the chat: firing them by hand with <c>dass.hook.call</c> and registering new ones
	/// with <c>dass.hook.register</c> - <c>dass.hook.*</c>.
	/// </summary>
	[LuaApi(chatScoped: true)]
	public class LuaApiHooks(
		IHookDispatcher hooks,
		RuntimeHookAddonSource runtimeHooks
	) : LuaApiBase
	{
		public override string? Namespace => "dass.hook";

		public override string? Manuals => """
			--- dass.hook - hook dispatch and registration

			Fires the hooks of the chat by event and target, outside the pipeline that normally fires them, and
			registers new hooks for the lifetime of the chat. A hook runs exactly as it would for a real call: a
			sequential one can answer with a value or break the chain.

			FUNCTIONS:

			--- async dass.hook.call(payload, type, [target], [default_flow], [force_flow]) -> value, blocking_message
			  Runs every hook that matches the event and the target, and returns the value of the last
			  sequential hook together with its blocking message.
			  Parameters:
			    - payload: any - what the hooks receive as `payload` (usually a table; nil is allowed)
			    - type: string - event name matched by the hook's `hook-type` glob, e.g. "tool.start.chat-run"
			    - target: string or nil (optional) - the target inside the event matched by the hook's
			      `hook-target` glob, e.g. "native.fs-edit". Hooks that declare no target match any target.
			    - default_flow: string or nil (optional, default "sequential") - the flow applied to a hook
			      that declares none: "sequential", "parallel" or "fire-and-forget"
			    - force_flow: boolean (optional, default false) - when true, `default_flow` overrides every
			      hook's own declaration
			  Returns:
			    - value - the last sequential hook's value, or nil
			    - blocking_message - string when a hook broke the chain, else nil

			  Sequential and parallel hooks are awaited, fire-and-forget ones are not. Every awaited hook
			  keeps its own timeout: an overrun is a log warning and "no opinion", never an error here.

			--- dass.hook.register(options) -> addon_revoke_handle
			  Registers a hook for this chat and returns the handle that removes it. The result behaves like a
			  hook addon from a pack: the same events, targets, flows and verdicts - only the body is a function
			  here and the declaration is a table.

			  Parameters:
			    - options: table - the keys a hook's frontmatter uses, in snake_case:
			        name: string - REQUIRED. Registering the same name twice REPLACES the earlier hook, so a
			          script that registers on load does not pile up duplicates. A runtime hook outranks a
			          pack hook of the same name, so pick a prefixed name ("my-skill.safe-paths") if you do
			          not want to shadow one.
			        executor: function - REQUIRED. See THE HOOK BODY below.
			        hook_type: string - REQUIRED. The event glob, e.g. "tool.*" or "message.user.*".
			        hook_target: string or nil - the target glob, e.g. "native.fs-edit".
			        execution_flow: string or nil - "sequential", "parallel" or "fire-and-forget".
			        timeout: number or nil - milliseconds the dispatcher gives the hook.
			        description: string or nil - shown in the hook list.
			        order: number or nil - order among the hooks of the same event.
			        enabled: boolean or nil (default true).
			        fixed: boolean or nil (default false) - true makes the hook impossible to switch off
			          from the settings list.
			  Unknown keys are ignored. Keys are matched ignoring case, '-' and '_', so `hook_type`,
			  `hook-type` and `hookType` are the same key.

			  Returns: addon_revoke_handle - see below.

			--- THE HOOK BODY

			  executor(payload, previous, hook_type, hook_target) -> value, blocking_message

			  `payload` is the event document, `previous` is the value the previous sequential hook returned,
			  `hook_type` and `hook_target` are the event and target that matched - useful when the hook was
			  registered with a glob.

			  The first result is the chain value. A string as the SECOND result ends the chain and reaches the
			  caller as a blocking message - that is how a guard refuses an action:

			    executor = function(payload, previous)
			      if payload and payload.arguments and payload.arguments.path == ".env" then
			        return nil, "Editing .env is not allowed here."
			      end
			      return previous
			    end

			  Returning nothing means "no opinion".

			--- ADDON REVOKE HANDLE ('addon_revoke_handle')

			  Properties (read-only):
			    - revoked: boolean - true once the registration is gone: revoked, or replaced by another
			      registration with the same name

			  Methods:
			    - h:revoke() -> boolean - removes the registration now; false when it was already gone

			NOTES:

			  - The registration belongs to this chat and disappears with it; it is never written to disk.
			  - A script reload re-runs the script, so registering on load replaces the previous registration
			    instead of adding a second one.
			  - A registered hook runs whenever the chat fires its event - including while the registering
			    script is still running. Do not register a hook from inside itself.

			EXAMPLES:

			  -- Ask the guards a real call would ask, without running the tool
			  local verdict, blocked = await dass.hook.call(
			    { tool = "fs-edit", arguments = { path = ".env" } },
			    "tool.start.chat-run", "native.fs-edit")
			  if blocked then print("blocked: " .. blocked) end

			  -- Force a fire-and-forget round over every hook of the event: no target, no waiting
			  -- 'task' returning by this function can be abandoned
			  dass.hook.call({ tool = "fs-read" }, "tool.start.chat-run", nil, "fire-and-forget", true)

			  -- A guard that refuses env edits for this chat only
			  local guard = dass.hook.register{
			    name = "guard-env",
			    description = "Refuse edits to .env",
			    hook_type = "tool.start.*",
			    hook_target = "native.fs-edit",
			    execution_flow = "sequential",
			    executor = function(payload, previous)
			      local path = payload and payload.arguments and payload.arguments.path
			      if path == ".env" then return nil, "Editing .env is not allowed." end
			      return previous
			    end,
			  }
			  -- guard:revoke()

			CUSTOM EVENTS:

			  The event vocabulary is open. `type` is any string, and a hook matches it with an anchored,
			  case-insensitive `hook-type` glob - nothing restricts a script to the built-in `tool.*` and
			  `message.*` names. A script and its hooks can agree on their own event name, e.g. "my_hook" or
			  "myapp.before-save", and the payload, target and verdict rules are the same as above.
			  Only the hooks of the chat's effective, enabled set run.

			  -- hooks/my-counter.lua
			  --[[
			  name: my-counter
			  description: Counts the "my_hook" event.
			  hook-type: my_hook
			  hook-target: "counter.*"
			  execution-flow: sequential
			  ]]
			  previous = previous or {}
			  previous.count = (previous.count or 0) + 1
			  return previous

			  -- The caller
			  local result = await dass.hook.call({}, "my_hook", "counter.main")
			  print(result and result.count)   -- 1
			""";

		public override Action? Populate(LuaTable globals, LuaTable ns, LuaService luaService)
		{
			ns["call"] = new LuaCallbackFunction(Call);
			ns["register"] = new LuaCallbackFunction(Register);

			return null;
		}

		private async Task<LuaTuple> Call(LuaCallingContext ctx, LuaValue[] args)
		{
			if (args.Length < 2)
				throw new LuaRuntimeException("dass.hook.call(payload, type, [target], [default_flow], [force_flow]): at least 2 arguments expected.");
		
			var payload = args[0].ToConstNodeValue(tolerant: true);
			var type = GetString("dass.hook.call(payload, type, [target], [default_flow], [force_flow])", args, 1, "type");
			
			string? target = null;
			if (args.Length >= 3 && args[2] is not LuaNil)
				target = GetString("dass.hook.call(payload, type, [target], [default_flow], [force_flow])", args, 2, "target");

			HookExecutionFlow defaultFlow = HookExecutionFlow.Sequential;
			if (args.Length >= 4 && args[3] is not LuaNil)
			{
				var defaultFlowStr = GetString("dass.hook.call(payload, type, [target], [default_flow], [force_flow])", args, 3, "default_flow");
				defaultFlow = KebabEnumNames<HookExecutionFlow>.Deserialize(defaultFlowStr);
			}

			bool forceFlow = false;
			if (args.Length >= 5)
			{
				if (args[4] is not LuaBoolean forceFlowValue)
					throw new LuaRuntimeException($"dass.hook.call(payload, type, [target], [default_flow], [force_flow]): argument 5 must be a boolean (force_flow).");
				forceFlow = forceFlowValue.Value;
			}

			var result = await hooks.ExecuteHooksAsync(payload, type, target, defaultFlow,
				forceFlow, ctx.CancellationToken);

			return new LuaTuple(result.Value.ToLuaValue(), string.IsNullOrEmpty(result.BlockingMessage) ?
				LuaNil.Instance : new LuaString(result.BlockingMessage));
		}

		private LuaTuple Register(LuaCallingContext ctx, LuaValue[] args)
		{
			const string call = "dass.hook.register(options)";

			if (args.Length == 0 || args[0] is not LuaTable table)
				throw new LuaRuntimeException($"{call}: a table is expected.");

			var options = ReadOptions(table);

			if (!options.TryGetValue("executor", out var executorValue) || executorValue is not LuaFunction executor)
				throw new LuaRuntimeException($"{call}: 'executor' is required and must be a function.");

			var hook = new HookInfo
			{
				Name = RequireString(options, "name", call),
				Description = OptionalString(options, "description", call) ?? string.Empty,
				HookType = RequireString(options, "hook_type", call),
				HookTarget = OptionalString(options, "hook_target", call),
				ExecutionFlow = OptionalFlow(options, call),
				TimeoutMs = OptionalInt(options, "timeout", call),
				Order = OptionalInt(options, "order", call) ?? 0,
				IsFixed = OptionalBool(options, "fixed", call) ?? false,
				Enabled = OptionalBool(options, "enabled", call) ?? true,

				// A runtime hook outranks a pack hook of the same name: the user registered it in this chat on purpose.
				OverrideOrder = int.MaxValue,
				Executor = new LuaFunctionHookExecutor(executor, ctx)
			};

			try
			{
				hook.ValidateProperties();
			}
			catch (ArgumentException ex)
			{
				throw new LuaRuntimeException($"{call}: {ex.Message}");
			}

			var registration = runtimeHooks.Register(hook);
			return new LuaTuple(LuaAddonRevokeHandle<HookInfo>.Wrap(registration));
		}

		/// <summary>
		/// Collects the option table into a case-, dash- and underscore-insensitive dictionary, the same way the
		/// frontmatter document reads its keys. Unknown keys are simply never looked up.
		/// </summary>
		private static Dictionary<string, LuaValue> ReadOptions(LuaTable table)
		{
			var options = new Dictionary<string, LuaValue>(IdentifierIgnoreCaseComparer.Instance);
			foreach (var entry in table.Entries)
				if (entry.Key is LuaString key)
					options[key.Value] = entry.Value;
			return options;
		}

		private static string RequireString(Dictionary<string, LuaValue> options, string key, string call)
		{
			if (options.TryGetValue(key, out var value) && value is LuaString text &&
				!string.IsNullOrWhiteSpace(text.Value))
				return text.Value;

			throw new LuaRuntimeException($"{call}: '{key}' is required and must be a non-empty string.");
		}

		private static string? OptionalString(Dictionary<string, LuaValue> options, string key, string call)
		{
			if (!options.TryGetValue(key, out var value) || value is LuaNil)
				return null;

			if (value is LuaString text)
				return string.IsNullOrWhiteSpace(text.Value) ? null : text.Value;

			throw new LuaRuntimeException($"{call}: '{key}' must be a string.");
		}

		private static int? OptionalInt(Dictionary<string, LuaValue> options, string key, string call)
		{
			if (!options.TryGetValue(key, out var value) || value is LuaNil)
				return null;

			if (value is LuaNumber number)
				return (int)number.Value;

			throw new LuaRuntimeException($"{call}: '{key}' must be a number.");
		}

		private static bool? OptionalBool(Dictionary<string, LuaValue> options, string key, string call)
		{
			if (!options.TryGetValue(key, out var value) || value is LuaNil)
				return null;

			if (value is LuaBoolean flag)
				return flag.Value;

			throw new LuaRuntimeException($"{call}: '{key}' must be a boolean.");
		}

		private static HookExecutionFlow? OptionalFlow(Dictionary<string, LuaValue> options, string call)
		{
			if (!options.TryGetValue("execution_flow", out var value) || value is LuaNil)
				return null;

			if (value is not LuaString text)
				throw new LuaRuntimeException($"{call}: 'execution_flow' must be a string.");

			var normalized = text.Value.Trim().ToLowerInvariant().Replace('_', '-');
			var flow = KebabEnumNames<HookExecutionFlow>.Deserialize(text.Value);

			// Deserialize falls back to the default on an unknown name, which would silently turn a typo into
			// fire-and-forget; the round-trip is what makes an unknown name an error.
			if (KebabEnumNames<HookExecutionFlow>.Serialize(flow) == normalized)
				return flow;

			var names = string.Join(", ", KebabEnumNames<HookExecutionFlow>.Names.Values.Order());
			throw new LuaRuntimeException($"{call}: 'execution_flow' must be one of: {names}.");
		}

		private static string GetString(string function, LuaValue[] args, int index, string name)
		{
			if (args[index] is not LuaString value)
				throw new LuaRuntimeException($"{function}: argument {index + 1} must be a string ({name}).");
			return value.Value;
		}
	}
}
