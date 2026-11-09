using AsyncLua;
using AsyncLua.Values;
using LLMDesktopAssistant.Hooks;
using LLMDesktopAssistant.StructuredValues.Converters;
using LLMDesktopAssistant.Utils;

namespace LLMDesktopAssistant.Scripting.Lua.API
{
	/// <summary>
	/// Lua API for firing the hooks of the chat by hand: <c>dass.hook.*</c>.
	/// </summary>
	[LuaApi(chatScoped: true)]
	public class LuaApiHooks(
		IHookDispatcher hooks
	) : LuaApiBase
	{
		public override string? Namespace => "dass.hook";

		public override string? Manuals => """
			--- dass.hook - hook dispatch API

			Fires the hooks of the chat by event and target, outside the pipeline that normally fires
			them. A hook runs exactly as it would for a real tool call: a sequential one can answer with a
			value or break the chain.

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
			    - blocking_message - string when a hook broke the chain (a blocking exit code), else nil

			  Sequential and parallel hooks are awaited, fire-and-forget ones are not. Every awaited hook
			  keeps its own timeout: an overrun is a log warning and "no opinion", never an error here.

			EXAMPLES:

			  -- Ask the guards a real call would ask, without running the tool
			  local verdict, blocked = await dass.hook.call(
			    { tool = "fs-edit", arguments = { path = ".env" } },
			    "tool.start.chat-run", "native.fs-edit")
			  if blocked then print("blocked: " .. blocked) end

			  -- Force a fire-and-forget round over every hook of the event: no target, no waiting
			  -- 'task' returning by this function can be abandoned
			  dass.hook.call({ tool = "fs-read" }, "tool.start.chat-run", nil, "fire-and-forget", true)

			CUSTOM EVENTS:

			  The event vocabulary is open. `type` is any string, and a hook matches it with an anchored,
			  case-insensitive `hook-type` glob - nothing restricts a script to the built-in `tool.*`
			  names. A script and its hooks can agree on their own event name, e.g. "my_hook" or
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

		private static string GetString(string function, LuaValue[] args, int index, string name)
		{
			if (args[index] is not LuaString value)
				throw new LuaRuntimeException($"{function}: argument {index + 1} must be a string ({name}).");
			return value.Value;
		}
	}
}
