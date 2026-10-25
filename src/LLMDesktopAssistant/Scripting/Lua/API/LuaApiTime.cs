using System.Diagnostics;
using AsyncLua;
using AsyncLua.Values;

namespace LLMDesktopAssistant.Scripting.Lua.API
{
	/// <summary>
	/// Lua API for time operations: <c>time.*</c>.
	/// Provides stopwatch, duration formatting, unit conversion,
	/// high-resolution timestamps, delays, timers, and execution measurement.
	/// </summary>
	[LuaApi(chatScoped: false)]
	public class LuaApiTime : LuaApiBase
	{
		public override string? Namespace => "time";

		public override string? Manuals => """
			--- time - time measurement and formatting API

			Provides stopwatch, duration formatting, unit conversion,
			high-resolution timestamps, delays, timers, and execution measurement.

			FUNCTIONS:

			--- time.now()
			  Returns the current Unix timestamp in seconds (with millisecond precision as float).
			  Returns: number - e.g. 1712345678.123

			--- time.now_ms()
			  Returns the current Unix timestamp in milliseconds.
			  Returns: number - integer, e.g. 1712345678123

			--- time.now_ns()
			  Returns a high-resolution monotonic timestamp in nanoseconds.
			  Does NOT represent wall-clock time; only suitable for computing differences.
			  Returns: number - nanoseconds since an undefined epoch

			--- time.sleep(ms)
			  Pauses execution for the specified number of milliseconds.
			  Parameters:
			    - ms: number - duration in milliseconds (can be fractional)
			  Returns: nil

			--- time.sleep_until(timestamp)
			  Pauses execution until the specified Unix timestamp (seconds).
			  Parameters:
			    - timestamp: number - Unix timestamp in seconds
			  Returns: nil

			--- time.format(ms, [style])
			  Formats a duration in milliseconds into a human-readable string.
			  Parameters:
			    - ms: number - duration in milliseconds
			    - style: string (optional) - "auto" (default), "full", "short", "clock"
			  Returns: string

			--- time.convert(value, from, to)
			  Converts a time value from one unit to another.
			  Supported units: ns, us, μs, ms, s, m, h, d, wk
			  Parameters:
			    - value: number - the value to convert
			    - from: string - source unit
			    - to: string - target unit
			  Returns: number

			--- time.measure(func)
			  Executes a function and measures its execution time.
			  Parameters:
			    - func: function - the function to execute
			  Returns: duration_ms (number), ... (function return values)

			--- time.stopwatch()
			  Creates a new stopwatch object for measuring elapsed time.
			  Returns: stopwatch userdata with :elapsed(), :elapsed_ns(),
			            :reset(), and :to_string() methods.

			--- async time.with_timeout(func, timeout_ms)
			  Executes a function with a timeout.
			  If the function exceeds the timeout, it is abandoned.
			  Parameters:
			    - func: function - the function to execute
			    - timeout_ms: number - timeout in milliseconds
			  Returns:
			    - ok: boolean - true if completed within timeout
			    - elapsed_ms: number - actual execution time
			    - result or error: any - return value or error message

			--- time.set_timeout(ms, callback)
			  Schedules a callback to run once after the specified delay.
			  The callback runs asynchronously.
			  Returns: timer userdata with :cancel() and :is_pending() methods.
			  Example:
			    local t = time.set_timeout(1000, function()
			      print("Hello 1 second later!")
			    end)
			    t:cancel()  -- cancel if needed

			--- time.set_interval(ms, callback)
			  Schedules a callback to run repeatedly every `ms` milliseconds.
			  Each invocation runs asynchronously.
			  Returns: timer userdata with :cancel() and :is_pending() methods.
			  Example:
			    local t = time.set_interval(500, function()
			      print("tick")
			    end)
			    t:cancel()  -- stop the interval
			""";

		public override Action? Populate(LuaTable globals, LuaTable ns, LuaService luaService)
		{
			ns["now"] = new LuaCallbackFunction(Now);
			ns["now_ms"] = new LuaCallbackFunction(NowMs);
			ns["now_ns"] = new LuaCallbackFunction(NowNs);
			ns["sleep"] = new LuaCallbackFunction(Sleep);
			ns["sleep_until"] = new LuaCallbackFunction(SleepUntil);
			ns["format"] = new LuaCallbackFunction(Format);
			ns["convert"] = new LuaCallbackFunction(Convert);
			ns["measure"] = new LuaCallbackFunction(Measure);
			ns["stopwatch"] = new LuaCallbackFunction(NewStopwatch);
			ns["with_timeout"] = new LuaCallbackFunction(WithTimeoutAsync);
			ns["set_timeout"] = new LuaCallbackFunction(SetTimeout);
			ns["set_interval"] = new LuaCallbackFunction(SetInterval);
			return null;
		}

		private static LuaTuple Now(LuaCallingContext ctx, LuaValue[] args)
		{
			return new LuaTuple(new LuaNumber(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0));
		}

		private static LuaTuple NowMs(LuaCallingContext ctx, LuaValue[] args)
		{
			return new LuaTuple(new LuaNumber(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()));
		}

		private static LuaTuple NowNs(LuaCallingContext ctx, LuaValue[] args)
		{
			return new LuaTuple(new LuaNumber(Stopwatch.GetTimestamp() * 1_000_000_000.0 / Stopwatch.Frequency));
		}

		private static LuaTuple Sleep(LuaCallingContext ctx, LuaValue[] args)
		{
			if (args.Length < 1)
				throw new LuaRuntimeException("time.sleep(ms): at least 1 argument expected.");
			if (args[0] is not LuaNumber msVal)
				throw new LuaRuntimeException("time.sleep(): argument must be a number (milliseconds).");

			var ms = msVal.Value;
			if (ms > 0)
			{
				var delayMs = (int)Math.Round(ms);
				if (delayMs > 0)
					Thread.Sleep(delayMs);
				else
					Thread.SpinWait(1);
			}
			return new LuaTuple(LuaNil.Instance);
		}

		private static LuaTuple SleepUntil(LuaCallingContext ctx, LuaValue[] args)
		{
			if (args.Length < 1)
				throw new LuaRuntimeException("time.sleep_until(timestamp): at least 1 argument expected.");
			if (args[0] is not LuaNumber tsVal)
				throw new LuaRuntimeException("time.sleep_until(): argument must be a number (Unix timestamp).");

			var now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0;
			var diff = tsVal.Value - now;
			if (diff > 0)
			{
				var delayMs = (int)Math.Round(diff * 1000);
				if (delayMs > 0)
					Thread.Sleep(delayMs);
			}
			return new LuaTuple(LuaNil.Instance);
		}

		private static LuaTuple Convert(LuaCallingContext ctx, LuaValue[] args)
		{
			if (args.Length < 3)
				throw new LuaRuntimeException("time.convert(value, from, to): at least 3 arguments expected.");
			if (args[0] is not LuaNumber valueVal)
				throw new LuaRuntimeException("time.convert(): first argument must be a number.");
			if (args[1] is not LuaString fromVal)
				throw new LuaRuntimeException("time.convert(): second argument must be a string (source unit).");
			if (args[2] is not LuaString toVal)
				throw new LuaRuntimeException("time.convert(): third argument must be a string (target unit).");

			var result = FromNanoseconds(ToNanoseconds(valueVal.Value, fromVal.Value.ToLowerInvariant()), toVal.Value.ToLowerInvariant());
			return new LuaTuple(new LuaNumber(result));
		}

		private static LuaTuple Format(LuaCallingContext ctx, LuaValue[] args)
		{
			if (args.Length < 1)
				throw new LuaRuntimeException("time.format(ms, [style]): at least 1 argument expected.");
			if (args[0] is not LuaNumber msVal)
				throw new LuaRuntimeException("time.format(): first argument must be a number (milliseconds).");

			var style = "auto";
			if (args.Length > 1 && args[1] is not LuaNil)
			{
				if (args[1] is LuaString sVal)
					style = sVal.Value.ToLowerInvariant();
			}

			return new LuaTuple(new LuaString(FormatDuration(msVal.Value, style)));
		}

		private static LuaTuple Measure(LuaCallingContext ctx, LuaValue[] args)
		{
			if (args.Length < 1 || args[0] is not LuaFunction func)
				throw new LuaRuntimeException("time.measure(func): first argument must be a function.");

			var sw = Stopwatch.StartNew();

			LuaTuple result;
			try { result = func.Invoke(ctx); }
			catch (Exception ex)
			{
				sw.Stop();
				throw new LuaRuntimeException($"time.measure(): failed after {sw.Elapsed.TotalMilliseconds:F1}ms: {ex.Message}");
			}

			sw.Stop();
			var elapsedMs = sw.Elapsed.TotalMilliseconds;
			var elapsedValue = new LuaNumber(elapsedMs);

			// Return elapsed_ms as first value, then unpack the original function's returns
			if (result.Count > 1) // multiple return values (tuple)
			{
				var returns = new LuaValue[result.Count + 1];
				returns[0] = elapsedValue;
				for (int i = 0; i < result.Count; i++)
					returns[i + 1] = result[i];
				return new LuaTuple(returns);
			}
			if (result.Count == 1 && result[0] is not LuaNil) // single non-nil value
				return new LuaTuple(elapsedValue, result[0]);

			// nil or empty - return just elapsed time
			return new LuaTuple(elapsedValue);
		}

		private static LuaTuple NewStopwatch(LuaCallingContext ctx, LuaValue[] args)
		{
			return new LuaTuple(LuaValueConverter.ToLuaValue(new LuaStopwatch()));
		}

		// --- Helpers ---

		private static double ToNanoseconds(double value, string unit) => unit switch
		{
			"ns" => value,
			"us" or "μs" => value * 1000,
			"ms" => value * 1_000_000,
			"s" or "sec" or "seconds" => value * 1_000_000_000,
			"m" or "min" or "minutes" => value * 60_000_000_000,
			"h" or "hours" => value * 3_600_000_000_000,
			"d" or "days" => value * 86_400_000_000_000,
			"wk" or "weeks" => value * 604_800_000_000_000,
			_ => throw new LuaRuntimeException($"time.convert(): unknown unit '{unit}'")
		};

		private static double FromNanoseconds(double ns, string unit) => unit switch
		{
			"ns" => ns,
			"us" or "μs" => ns / 1000,
			"ms" => ns / 1_000_000,
			"s" or "sec" or "seconds" => ns / 1_000_000_000,
			"m" or "min" or "minutes" => ns / 60_000_000_000,
			"h" or "hours" => ns / 3_600_000_000_000,
			"d" or "days" => ns / 86_400_000_000_000,
			"wk" or "weeks" => ns / 604_800_000_000_000,
			_ => throw new LuaRuntimeException($"time.convert(): unknown unit '{unit}'")
		};

		private static string FormatDuration(double ms, string style)
		{
			if (ms < 0) ms = 0;
			return style switch
			{
				"auto" => FormatAuto(ms),
				"full" => FormatFull(ms),
				"short" => FormatShort(ms),
				"clock" or "timestamp" => FormatClock(ms),
				_ => FormatAuto(ms)
			};
		}

		private static string FormatAuto(double ms)
		{
			if (ms < 0.001) return $"{(ms * 1_000_000):F0}ns";
			if (ms < 1) return $"{(ms * 1000):F0}μs";
			if (ms < 1000) return $"{ms:F1}ms";
			if (ms < 60_000) return $"{ms / 1000:F2}s";

			double seconds = ms / 1000;
			double minutes = seconds / 60;
			double hours = minutes / 60;
			double days = hours / 24;

			if (days >= 1)
			{
				var wholeDays = (int)days;
				var remainingHours = hours - wholeDays * 24;
				return remainingHours >= 1 ? $"{wholeDays}d {(int)remainingHours}h" : $"{wholeDays}d";
			}
			if (hours >= 1)
			{
				var wholeHours = (int)hours;
				var remainingMinutes = minutes - wholeHours * 60;
				return remainingMinutes >= 1 ? $"{wholeHours}h {(int)remainingMinutes}m" : $"{wholeHours}h";
			}
			var wholeMinutes = (int)minutes;
			var remainingSeconds = seconds - wholeMinutes * 60;
			return remainingSeconds >= 1 ? $"{wholeMinutes}m {(int)remainingSeconds}s" : $"{wholeMinutes}m";
		}

		private static string FormatFull(double ms)
		{
			if (ms < 0.001) return $"{(ms * 1_000_000):F0} nanoseconds";
			if (ms < 1) return $"{(ms * 1000):F2} microseconds";

			var parts = new List<string>();
			var totalSeconds = ms / 1000;

			var weeks = (int)(totalSeconds / (7 * 86400));
			totalSeconds -= weeks * 7 * 86400;
			if (weeks > 0) parts.Add($"{weeks} week{(weeks != 1 ? "s" : "")}");

			var days = (int)(totalSeconds / 86400);
			totalSeconds -= days * 86400;
			if (days > 0) parts.Add($"{days} day{(days != 1 ? "s" : "")}");

			var hours = (int)(totalSeconds / 3600);
			totalSeconds -= hours * 3600;
			if (hours > 0) parts.Add($"{hours} hour{(hours != 1 ? "s" : "")}");

			var minutes = (int)(totalSeconds / 60);
			totalSeconds -= minutes * 60;
			if (minutes > 0) parts.Add($"{minutes} minute{(minutes != 1 ? "s" : "")}");

			if (totalSeconds >= 1 || parts.Count == 0)
				parts.Add($"{totalSeconds:F3} seconds");
			else
			{
				var remainingMs = totalSeconds * 1000;
				if (remainingMs >= 1)
					parts.Add($"{remainingMs:F0} ms");
				else if (remainingMs > 0)
					parts.Add($"{(remainingMs * 1000):F0} μs");
			}

			return string.Join(", ", parts);
		}

		private static string FormatShort(double ms)
		{
			if (ms < 0.001) return $"{(ms * 1_000_000):F0}ns";
			if (ms < 1) return $"{(ms * 1000):F0}μs";

			var totalSeconds = ms / 1000;
			var parts = new List<string>();

			var days = (int)(totalSeconds / 86400);
			totalSeconds -= days * 86400;
			if (days > 0) parts.Add($"{days}d");

			var hours = (int)(totalSeconds / 3600);
			totalSeconds -= hours * 3600;
			if (hours > 0) parts.Add($"{hours}h");

			var minutes = (int)(totalSeconds / 60);
			totalSeconds -= minutes * 60;
			if (minutes > 0) parts.Add($"{minutes}m");

			if (totalSeconds >= 1 || parts.Count == 0)
				parts.Add($"{totalSeconds:F3}s");
			else
			{
				var remainingMs = totalSeconds * 1000;
				parts.Add(remainingMs >= 1 ? $"{remainingMs:F0}ms" : $"{(remainingMs * 1000):F0}μs");
			}

			return string.Join(" ", parts);
		}

		private static string FormatClock(double ms)
		{
			if (ms < 0) ms = 0;
			var totalSeconds = (long)(ms / 1000);
			var remainingMs = (int)(ms % 1000);
			return $"{totalSeconds / 3600:D2}:{(totalSeconds % 3600) / 60:D2}:{totalSeconds % 60:D2}.{remainingMs:D3}";
		}

		private static async Task<LuaTuple> WithTimeoutAsync(LuaCallingContext ctx, LuaValue[] args)
		{
			if (args.Length < 2)
				throw new LuaRuntimeException("time.with_timeout(func, timeout_ms): at least 2 arguments expected.");
			if (args[0] is not LuaFunction func)
				throw new LuaRuntimeException("time.with_timeout(): first argument must be a function.");
			if (args[1] is not LuaNumber timeoutVal)
				throw new LuaRuntimeException("time.with_timeout(): second argument must be a positive number (timeout in ms).");

			var timeoutMs = (int)timeoutVal.Value;
			var sw = Stopwatch.StartNew();

			var invokeTask = func.InvokeAsync(ctx);
			var delayTask = Task.Delay(timeoutMs);
			var completed = await Task.WhenAny(invokeTask, delayTask);
			sw.Stop();
			var elapsedMs = sw.Elapsed.TotalMilliseconds;

			if (completed == invokeTask)
			{
				var result = await invokeTask;

				if (result.Count > 1)
				{
					var returns = new LuaValue[result.Count + 2];
					returns[0] = LuaBoolean.True;
					returns[1] = new LuaNumber(elapsedMs);
					for (int i = 0; i < result.Count; i++)
						returns[i + 2] = result[i];
					return new LuaTuple(returns);
				}
				if (result.Count == 1 && result[0] is not LuaNil)
					return new LuaTuple(LuaBoolean.True, new LuaNumber(elapsedMs), result[0]);

				return new LuaTuple(LuaBoolean.True, new LuaNumber(elapsedMs));
			}
			else
			{
				return new LuaTuple(
					LuaBoolean.False,
					new LuaNumber(elapsedMs),
					new LuaString($"Timeout exceeded (limit was {timeoutMs}ms)"));
			}
		}

		private static LuaTuple SetTimeout(LuaCallingContext ctx, LuaValue[] args)
		{
			if (args.Length < 2)
				throw new LuaRuntimeException("time.set_timeout(ms, callback): at least 2 arguments expected.");
			if (args[0] is not LuaNumber msVal)
				throw new LuaRuntimeException("time.set_timeout(): first argument must be a number (milliseconds).");
			if (args[1] is not LuaFunction callback)
				throw new LuaRuntimeException("time.set_timeout(): second argument must be a function (callback).");

			var timer = new LuaTimer((int)msVal.Value, callback, ctx, isRepeating: false);
			return new LuaTuple(LuaValueConverter.ToLuaValue(timer));
		}

		private static LuaTuple SetInterval(LuaCallingContext ctx, LuaValue[] args)
		{
			if (args.Length < 2)
				throw new LuaRuntimeException("time.set_interval(ms, callback): at least 2 arguments expected.");
			if (args[0] is not LuaNumber msVal)
				throw new LuaRuntimeException("time.set_interval(): first argument must be a number (milliseconds).");
			if (args[1] is not LuaFunction callback)
				throw new LuaRuntimeException("time.set_interval(): second argument must be a function (callback).");

			var timer = new LuaTimer((int)msVal.Value, callback, ctx, isRepeating: true);
			return new LuaTuple(LuaValueConverter.ToLuaValue(timer));
		}
	}
}
