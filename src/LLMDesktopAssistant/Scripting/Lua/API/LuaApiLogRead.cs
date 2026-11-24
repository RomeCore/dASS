using AsyncLua;
using AsyncLua.Values;
using LLMDesktopAssistant.Scripting.Lua.Logs;
using LLMDesktopAssistant.Utils;

namespace LLMDesktopAssistant.Scripting.Lua.API
{
	/// <summary>
	/// Lua API for reading the log buffer and subscribing to its events: <c>dass.log.*</c>.
	/// </summary>
	/// <remarks>
	/// The buffer is application-wide: every chat sees the same events, its own script output included.
	/// </remarks>
	[LuaApi]
	public class LuaApiLogRead(LuaLogFeed feed) : LuaApiBase
	{
		public override string? Namespace => "dass.log";

		public override string? Manuals => """
			--- dass.log - reading API

			Reads the application log buffer (the last 5000 events of every chat and subsystem)
			and subscribes to new ones. Logging is in the same namespace, see the writing section.

			FUNCTIONS:

			--- dass.log.count() -> integer
			  Total count of events ever logged, including the ones already evicted from the
			  buffer. Equal to the sequence of the newest event, so missing = count - last_seq.

			--- dass.log.query([filter]) -> entry[]
			  The matching buffered events, oldest first. Without a filter it returns the whole
			  buffer.

			  filter (optional table):
			    - min_level: string - lowest level that passes: verbose|debug|info|warning|error|fatal
			    - levels: table - exact set of levels, e.g. { "error", "fatal" }
			    - text: string - case-insensitive substring of the rendered message or the template
			    - since: integer - return only events with seq > since
			    - last: integer - keep the last N matching events (applied after the other fields)

			--- dass.log.subscribe([filter], callback) -> callback_registration
			  Calls callback(entry) for every new event passing the filter. The filter is the
			  same table query takes; since skips everything at or below that sequence.
			  The callback runs on the thread that logged, right after the event is stored.
			  Events logged from inside the callback are not delivered back to it.

			  subscribe(filter, callback) and subscribe(callback) are both accepted.
			  The handle has .cancel() and .registered.

			ENTRY:

			  entry = {
			    seq = 12345,                  -- integer, strictly increasing
			    timestamp = 1764012345.678,   -- Unix seconds, see datetime.from_timestamp
			    level = "info",               -- verbose|debug|info|warning|error|fatal
			    message = "User John logged in",   -- rendered message
			    template = "User {Name} logged in",
			    properties = { Name = "John" },    -- string -> string
			    exception = nil,              -- full exception text with the stack, or nil
			  }

			EXAMPLES:

			  -- everything that went wrong in this session
			  local errors = dass.log.query({ min_level = "error" })
			  print(#errors .. " errors")

			  -- the last 20 warnings and worse, newest last
			  for _, entry in ipairs(dass.log.query({ min_level = "warning", last = 20 })) do
			    print(entry.timestamp, entry.level, entry.message)
			  end

			  -- live tail with no gap: snapshot first, then subscribe from the last seen seq
			  local seen = dass.log.query({ last = 50 })
			  local lastSeq = seen[#seen] and seen[#seen].seq or 0
			  local sub = dass.log.subscribe({ since = lastSeq }, function(entry)
			    print("[" .. entry.level .. "] " .. entry.message)
			  end)

			  -- later
			  sub:cancel()
			""";

		public override Action? Populate(LuaTable globals, LuaTable ns, LuaService luaService)
		{
			// The state is rebuilt on every refresh: the registrations of the old one are dead.
			feed.Reset();

			ns["count"] = new LuaCallbackFunction(Count);
			ns["query"] = new LuaCallbackFunction(Query);
			ns["subscribe"] = new LuaCallbackFunction(Subscribe);

			return null;
		}

		private LuaTuple Count(LuaCallingContext ctx, LuaValue[] args)
			=> new(new LuaNumber(LogBuffer.Count));

		private LuaTuple Query(LuaCallingContext ctx, LuaValue[] args)
		{
			const string call = "dass.log.query([filter])";

			var filter = LuaLogFilter.Parse(args.Length > 0 ? args[0] : null, call);

			var matches = LogBuffer.GetAfter(filter.Since);
			matches.RemoveAll(entry => !filter.Matches(entry));

			// `last` is the tail window: the newest matches, still reported oldest first.
			if (matches.Count > filter.Last)
				matches.RemoveRange(0, matches.Count - filter.Last);

			var table = new LuaTable();
			foreach (var entry in matches)
				table.Append(entry.ToLua());

			return new LuaTuple(table);
		}

		private LuaTuple Subscribe(LuaCallingContext ctx, LuaValue[] args)
		{
			const string call = "dass.log.subscribe([filter], callback)";

			if (args.Length == 0)
				throw new LuaRuntimeException($"{call}: a callback is expected.");

			LuaValue? filterValue = null;
			var function = args[0] as LuaFunction;

			if (function is null)
			{
				filterValue = args[0];
				function = args.Length > 1 ? args[1] as LuaFunction : null;
			}

			if (function is null)
				throw new LuaRuntimeException($"{call}: a callback function is expected.");

			var filter = LuaLogFilter.Parse(filterValue, call);

			return new LuaTuple(LuaCallbackRegistration.Wrap(feed.Subscribe(function, filter, ctx)));
		}
	}
}
