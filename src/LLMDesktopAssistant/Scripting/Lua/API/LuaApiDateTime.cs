using System.Globalization;
using AsyncLua;
using AsyncLua.Values;

namespace LLMDesktopAssistant.Scripting.Lua.API
{
	/// <summary>
	/// Lua API for date and time operations: <c>datetime.*</c>.
	/// </summary>
	[LuaApi(chatScoped: false)]
	public class LuaApiDateTime : LuaApiBase
	{
		public override string? Namespace => "datetime";

		public override string? Manuals => """
			--- datetime - date and time API

			All functions work with datetime tables:
			  { year, month, day, hour, min, sec, ms, is_dst, utc_offset (minutes), timestamp (unix epoch seconds) }

			FUNCTIONS:

			--- datetime.now()
			  Returns the current UTC time as a datetime table.
			  Returns: table

			--- datetime.now_local()
			  Returns the current local time as a datetime table.
			  Returns: table

			--- datetime.parse(str, [format])
			  Parses a date/time string into a datetime table.
			  Parameters:
			    - str: string - date/time string
			    - format: string (optional) - .NET date format string. 
			      If omitted, ISO 8601 and common formats are auto-detected.
			  Returns: table or nil on failure

			--- datetime.format(dt, [format])
			  Formats a datetime table into a string.
			  Parameters:
			    - dt: table - datetime table
			    - format: string (optional) - .NET date format string.
			      Default: "o" (ISO 8601: "2026-05-25T02:54:00.0000000Z")
			  Returns: string

			--- datetime.diff(dt1, dt2)
			  Returns the difference between two datetime tables in seconds.
			  Parameters:
			    - dt1: table - first datetime
			    - dt2: table - second datetime
			  Returns: number - dt1 - dt2 in seconds

			--- datetime.add(dt, interval)
			  Adds a time interval to a datetime table.
			  Parameters:
			    - dt: table - datetime table
			    - interval: table - interval with fields:
			      - years: number (optional)
			      - months: number (optional)
			      - days: number (optional)
			      - hours: number (optional)
			      - minutes: number (optional)
			      - seconds: number (optional)
			      - ms: number (optional, milliseconds)
			  Returns: table - new datetime (original unchanged)

			--- datetime.utc(dt)
			  Converts a datetime table to UTC.
			  Parameters:
			    - dt: table - datetime table
			  Returns: table

			--- datetime.timestamp(dt)
			  Returns the Unix epoch seconds from a datetime table.
			  Parameters:
			    - dt: table - datetime table
			  Returns: number

			--- datetime.from_timestamp(ts)
			  Creates a datetime table from Unix epoch seconds.
			  Parameters:
			    - ts: number - Unix epoch seconds
			  Returns: table

			EXAMPLES:

			  local now = datetime.now()
			  print(now.year, now.month, now.day)

			  -- Format
			  print(datetime.format(now, "yyyy-MM-dd HH:mm:ss"))

			  -- Parse
			  local dt = datetime.parse("2026-05-25T03:00:00Z")

			  -- Difference
			  local d1 = datetime.now()
			  local d2 = datetime.add(d1, {hours = 1})
			  print(datetime.diff(d2, d1)) -- 3600

			  -- Add
			  local tomorrow = datetime.add(now, {days = 1})

			  -- Timestamp
			  print(datetime.timestamp(now))
			""";

		public override Action? Populate(LuaTable globals, LuaTable ns, LuaService luaService)
		{
			ns["now"] = new LuaCallbackFunction(Now);
			ns["now_local"] = new LuaCallbackFunction(Local);
			ns["parse"] = new LuaCallbackFunction(Parse);
			ns["format"] = new LuaCallbackFunction(Format);
			ns["diff"] = new LuaCallbackFunction(Diff);
			ns["add"] = new LuaCallbackFunction(Add);
			ns["utc"] = new LuaCallbackFunction(Utc);
			ns["timestamp"] = new LuaCallbackFunction(Timestamp);
			ns["from_timestamp"] = new LuaCallbackFunction(FromTimestamp);
			return null;
		}

		private static LuaTuple Now(LuaCallingContext ctx, LuaValue[] args)
		{
			return new LuaTuple(DateTimeToTable(DateTimeOffset.UtcNow));
		}

		private static LuaTuple Local(LuaCallingContext ctx, LuaValue[] args)
		{
			return new LuaTuple(DateTimeToTable(DateTimeOffset.Now));
		}

		private static LuaTuple Parse(LuaCallingContext ctx, LuaValue[] args)
		{
			if (args.Length < 1)
				throw new LuaRuntimeException("datetime.parse(str, [format]): at least 1 argument expected.");

			if (!args[0].TryToString(out var str))
				throw new LuaRuntimeException("datetime.parse(): first argument must be a string.");

			try
			{
				if (args.Length > 1 && args[1] is not LuaNil)
				{
					if (!args[1].TryToString(out var format))
						throw new LuaRuntimeException("datetime.parse(): second argument must be a string (format).");
					var dt = DateTimeOffset.ParseExact(str, format, CultureInfo.InvariantCulture);
					return new LuaTuple(DateTimeToTable(dt));
				}

				var parsed = DateTimeOffset.Parse(str, CultureInfo.InvariantCulture);
				return new LuaTuple(DateTimeToTable(parsed));
			}
			catch (Exception ex)
			{
				throw new LuaRuntimeException($"datetime.parse() error: {ex.Message}");
			}
		}

		private static LuaTuple Format(LuaCallingContext ctx, LuaValue[] args)
		{
			if (args.Length < 1)
				throw new LuaRuntimeException("datetime.format(dt, [format]): at least 1 argument expected.");

			var dt = TableToDateTime(args[0] as LuaTable);
			if (dt == null)
				throw new LuaRuntimeException("datetime.format(): first argument must be a datetime table.");

			var format = "o"; // ISO 8601 default
			if (args.Length > 1 && args[1] is not LuaNil)
			{
				if (!args[1].TryToString(out var fmt))
					throw new LuaRuntimeException("datetime.format(): second argument must be a string (format).");
				format = fmt;
			}

			try
			{
				return new LuaTuple(new LuaString(dt.Value.ToUniversalTime().ToString(format, CultureInfo.InvariantCulture)));
			}
			catch (Exception ex)
			{
				throw new LuaRuntimeException($"datetime.format() error: {ex.Message}");
			}
		}

		private static LuaTuple Diff(LuaCallingContext ctx, LuaValue[] args)
		{
			if (args.Length < 2)
				throw new LuaRuntimeException("datetime.diff(dt1, dt2): at least 2 arguments expected.");

			var dt1 = TableToDateTime(args[0] as LuaTable);
			var dt2 = TableToDateTime(args[1] as LuaTable);
			if (dt1 == null || dt2 == null)
				throw new LuaRuntimeException("datetime.diff(): both arguments must be datetime tables.");

			return new LuaTuple(new LuaNumber((dt1.Value - dt2.Value).TotalSeconds));
		}

		private static LuaTuple Add(LuaCallingContext ctx, LuaValue[] args)
		{
			if (args.Length < 2)
				throw new LuaRuntimeException("datetime.add(dt, interval): at least 2 arguments expected.");

			if (args[0] is not LuaTable t || TableToDateTime(t) is not DateTimeOffset dt)
				throw new LuaRuntimeException("datetime.add(): first argument must be a datetime table.");

			var interval = args[1];
			if (interval is not LuaTable it)
				throw new LuaRuntimeException("datetime.add(): second argument must be a table (interval).");

			var years = GetIntField(it, "years", 0);
			var months = GetIntField(it, "months", 0);
			var days = GetIntField(it, "days", 0);
			var hours = GetIntField(it, "hours", 0);
			var minutes = GetIntField(it, "minutes", 0);
			var seconds = GetIntField(it, "seconds", 0);
			var ms = GetIntField(it, "ms", 0);

			var result = dt.ToUniversalTime();
			if (years != 0 || months != 0)
			{
				// Add years/months via calendar (preserving time)
				var utc = result.DateTime;
				var newDt = utc.AddYears(years).AddMonths(months);
				result = new DateTimeOffset(newDt, TimeSpan.Zero);
			}
			result = result.AddDays(days).AddHours(hours).AddMinutes(minutes).AddSeconds(seconds).AddMilliseconds(ms);

			return new LuaTuple(DateTimeToTable(result));
		}

		private static LuaTuple Utc(LuaCallingContext ctx, LuaValue[] args)
		{
			if (args.Length < 1)
				throw new LuaRuntimeException("datetime.utc(dt): at least 1 argument expected.");

			if (TableToDateTime(args[0] as LuaTable) is not DateTimeOffset dt)
				throw new LuaRuntimeException("datetime.utc(): first argument must be a datetime table.");

			return new LuaTuple(DateTimeToTable(dt.ToUniversalTime()));
		}

		private static LuaTuple Timestamp(LuaCallingContext ctx, LuaValue[] args)
		{
			if (args.Length < 1)
				throw new LuaRuntimeException("datetime.timestamp(dt): at least 1 argument expected.");

			if (TableToDateTime(args[0] as LuaTable) is not DateTimeOffset dt)
				throw new LuaRuntimeException("datetime.timestamp(): first argument must be a datetime table.");

			return new LuaTuple(new LuaNumber(dt.ToUnixTimeSeconds()));
		}

		private static LuaTuple FromTimestamp(LuaCallingContext ctx, LuaValue[] args)
		{
			if (args.Length < 1)
				throw new LuaRuntimeException("datetime.from_timestamp(ts): at least 1 argument expected.");

			if (!args[0].TryToNumber(out var ts))
				throw new LuaRuntimeException("datetime.from_timestamp(): first argument must be a number.");

			var dt = DateTimeOffset.FromUnixTimeSeconds((long)ts);
			return new LuaTuple(DateTimeToTable(dt));
		}

		// --- Helpers ---

		private static LuaTable DateTimeToTable(DateTimeOffset dt)
		{
			var t = new LuaTable();
			t["year"] = new LuaNumber(dt.Year);
			t["month"] = new LuaNumber(dt.Month);
			t["day"] = new LuaNumber(dt.Day);
			t["hour"] = new LuaNumber(dt.Hour);
			t["min"] = new LuaNumber(dt.Minute);
			t["sec"] = new LuaNumber(dt.Second);
			t["ms"] = new LuaNumber(dt.Millisecond);
			t["is_dst"] = LuaBoolean.FromBoolean(dt.DateTime.IsDaylightSavingTime());
			t["utc_offset"] = new LuaNumber((int)dt.Offset.TotalMinutes);
			t["timestamp"] = new LuaNumber(dt.ToUnixTimeSeconds());
			t["day_of_week"] = new LuaNumber((int)dt.DayOfWeek); // 0=Sunday, 6=Saturday
			t["day_of_year"] = new LuaNumber(dt.DayOfYear);
			return t;
		}

		private static DateTimeOffset? TableToDateTime(LuaTable? t)
		{
			if (t is null)
				return null;

			var year = GetIntField(t, "year", 1);
			var month = GetIntField(t, "month", 1);
			var day = GetIntField(t, "day", 1);
			var hour = GetIntField(t, "hour", 0);
			var min = GetIntField(t, "min", 0);
			var sec = GetIntField(t, "sec", 0);
			var ms = GetIntField(t, "ms", 0);

			// If timestamp is present, prefer it
			var tsField = t.Get("timestamp");
			if (tsField is LuaNumber number)
			{
				return DateTimeOffset.FromUnixTimeSeconds((long)number.Value);
			}

			int offsetMinutes = GetIntField(t, "utc_offset", 0);
			var offset = TimeSpan.FromMinutes(offsetMinutes);

			try
			{
				var dt = new DateTimeOffset(year, month, day, hour, min, sec, ms, offset);
				return dt;
			}
			catch
			{
				return null;
			}
		}

		private static int GetIntField(LuaTable t, string key, int defaultValue)
		{
			var val = t.Get(key);
			if (val is LuaNumber number)
				return (int)number.Value;
			return defaultValue;
		}
	}
}
