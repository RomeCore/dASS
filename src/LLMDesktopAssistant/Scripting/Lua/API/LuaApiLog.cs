using AsyncLua;
using AsyncLua.Values;
using Serilog;

namespace LLMDesktopAssistant.Scripting.Lua.API
{
	/// <summary>
	/// Lua API for structured logging via Serilog: <c>dass.log.*</c>.
	/// Supports all Serilog levels with template-based message formatting.
	/// </summary>
	[LuaApi(chatScoped: false)]
	public class LuaApiLog : LuaApiBase
	{
		private readonly ILogger _logger;

		public override string? Namespace => "dass.log";

		public override string? Manuals => """
			--- dass.log - structured logging API

			Logs messages at various levels with template-based formatting.
			Supports named placeholders like {Name}, {Count}, etc.

			FUNCTIONS:

			--- dass.log.verbose(template, ...)
			--- dass.log.debug(template, ...)
			--- dass.log.info(template, ...)
			--- dass.log.warning(template, ...)
			--- dass.log.error(template, ...)
			--- dass.log.fatal(template, ...)

			  Each function takes a message template string followed by
			  optional positional arguments to fill in placeholders.

			  Parameters:
			    - template: string - message template (e.g. "User {Name} logged in")
			    - ...: any (optional) - values for placeholders, in order

			  Returns: nil

			EXAMPLES:

			  dass.log.info("Hello {Name}, you are {Age} years old", "John", 30)
			  dass.log.warning("Disk space low: {FreeMB} MB remaining", 42)
			  dass.log.error("Failed to process file {Path}: {Error}", "/tmp/x.txt", "not found")
			  dass.log.debug("X = {X}, Y = {Y}", 10, 20)
			  dass.log.verbose("Entering function {Func}", "calculate")
			  dass.log.fatal("System halted: {Reason}", "kernel panic")
			""";

		public LuaApiLog(ILogger logger)
		{
			_logger = logger;
		}

		public override Action? Populate(LuaTable globals, LuaTable ns, LuaService luaService)
		{
			ns["verbose"] = new LuaCallbackFunction(LogVerbose);
			ns["debug"] = new LuaCallbackFunction(LogDebug);
			ns["info"] = new LuaCallbackFunction(LogInfo);
			ns["warning"] = new LuaCallbackFunction(LogWarning);
			ns["error"] = new LuaCallbackFunction(LogError);
			ns["fatal"] = new LuaCallbackFunction(LogFatal);
			return null;
		}

		private LuaTuple LogVerbose(LuaCallingContext ctx, LuaValue[] args)
		{
			LogWithLevel(args, (t, p) => _logger.Verbose(t, p));
			return new LuaTuple(LuaNil.Instance);
		}

		private LuaTuple LogDebug(LuaCallingContext ctx, LuaValue[] args)
		{
			LogWithLevel(args, (t, p) => _logger.Debug(t, p));
			return new LuaTuple(LuaNil.Instance);
		}

		private LuaTuple LogInfo(LuaCallingContext ctx, LuaValue[] args)
		{
			LogWithLevel(args, (t, p) => _logger.Information(t, p));
			return new LuaTuple(LuaNil.Instance);
		}

		private LuaTuple LogWarning(LuaCallingContext ctx, LuaValue[] args)
		{
			LogWithLevel(args, (t, p) => _logger.Warning(t, p));
			return new LuaTuple(LuaNil.Instance);
		}

		private LuaTuple LogError(LuaCallingContext ctx, LuaValue[] args)
		{
			LogWithLevel(args, (t, p) => _logger.Error(t, p));
			return new LuaTuple(LuaNil.Instance);
		}

		private LuaTuple LogFatal(LuaCallingContext ctx, LuaValue[] args)
		{
			LogWithLevel(args, (t, p) => _logger.Fatal(t, p));
			return new LuaTuple(LuaNil.Instance);
		}

		private static void LogWithLevel(LuaValue[] args, System.Action<string, object[]> logAction)
		{
			if (args.Length < 1)
				throw new LuaRuntimeException("dass.log.LEVEL(template, ...): at least 1 argument expected (template).");

			if (args[0] is not LuaString templateVal)
				throw new LuaRuntimeException("dass.log.LEVEL(): first argument must be a string (template).");

			if (args.Length == 1)
			{
				logAction(templateVal.Value, []);
				return;
			}

			var propertyValues = new object[args.Length - 1];
			for (int i = 1; i < args.Length; i++)
			{
				propertyValues[i - 1] = LuaValueToObject(args[i]);
			}

			logAction(templateVal.Value, propertyValues);
		}

		private static object LuaValueToObject(LuaValue val)
		{
			if (val is LuaNil)
				return null!;
			if (val is LuaBoolean boolean)
				return boolean.Value;
			if (val is LuaNumber number)
				return number.Value;
			if (val is LuaString str)
				return str.Value;
			if (val is LuaTable t)
			{
				// Convert to simple dict/array for readability
				var dict = new Dictionary<string, object?>();
				foreach (var kv in t.Entries)
				{
					dict.Add(kv.Key.ToString() ?? "?", LuaValueToObject(kv.Value));
				}
				return dict;
			}
			return val.ToString() ?? "?";
		}
	}
}
