using AsyncLua;
using AsyncLua.Values;
using LLMDesktopAssistant.Scripting.Lua.Scheduling;

namespace LLMDesktopAssistant.Scripting.Lua.API
{
	/// <summary>
	/// Lua API for the chat scheduler: <c>dass.schedule.*</c>.
	/// </summary>
	/// <remarks>
	/// The scheduler is chat-scoped on purpose: a job belongs to the chat, so a pinned chat that boots the
	/// script on application start is the one way to get a job that is always armed.
	/// </remarks>
	[LuaApi(chatScoped: true)]
	public class LuaApiSchedule(ChatScheduler scheduler) : LuaApiBase
	{
		private const int maxJobs = 256;
		private const int minPeriodMs = 50;

		public override string? Namespace => "dass.schedule";

		public override string? Manuals => """
			--- dass.schedule - chat scheduler

			Runs a function later, on a grid or on the calendar. A job belongs to the chat: it survives the tool
			call, the turn and Stop, and dies when the chat closes, when dass.schedule.clear() runs, or when its
			handle is cancelled. Reloading addons rebuilds the Lua state and drops every job too.

			CALLBACK ARGUMENTS:

			  fn(missed, last_shot)
			    missed    - how many firings were skipped. Its meaning depends on the trigger:
			                every: always a number. How many grid steps were skipped (from in the past, or a
			                       firing that outlived its period). 0 in the normal case.
			                cron:  nil when catch_slot is not set, -1 on the first firing of a slot,
			                       0 on a normal firing, n>0 when n occurrences passed between the stored
			                       slot stamp and the registration - that firing happens immediately.
			                at:    nil, a one-shot has nothing to skip.
			    last_shot - Unix timestamp of the previous firing, or nil. Delivered even without a slot.
			                Convert it with datetime.from_timestamp(last_shot) when details are needed.

			FUNCTIONS:

			--- dass.schedule.every(fn, options) -> schedule
			  Repeats on a millisecond grid.
			  options (required):
			    - period: number - period in milliseconds, at least 50 (required)
			    - delay: number - milliseconds to wait from `from` before the first firing (default 0)
			    - from: number - Unix timestamp the count starts at (default: now)
			    - grid: boolean (default true) - true: firings land on from + delay + period * n and a firing
			      that runs too long skips grid steps, which the next firing sees in `missed`; false: the next
			      firing is `period` after the previous one finishes and `missed` is always 0.
			  A `from` in the past is not an error: the grid is kept and the skipped steps are counted.

			--- dass.schedule.cron(fn, options) -> schedule
			  Follows a calendar rule in local time.
			  options (required):
			    - expression: string (required) - UNIX cron cron expression with five fields: minute hour day-of-month month day-of-week.
			      Supports * , - / and the names JAN..DEC and MON..SUN; 0 and 7 both mean Sunday.
			    - catch_slot: string (optional) - the slot that remembers the last firing across restarts.

			  Without catch_slot the job waits silently for the next occurrence. With it, the registration
			  itself may fire: -1 when the slot has no stamp yet, or the number of occurrences that passed
			  since the stamp. The stamp is rewritten on every firing.

			--- dass.schedule.at(fn, timestamp) -> schedule
			  Fires once at a Unix timestamp. A timestamp in the past is an error.

			--- dass.schedule.list() -> table
			  Every job of the chat as a schedule handle, disabled ones included.

			--- dass.schedule.clear() -> number
			  Cancels every job of the chat and returns how many were cancelled.

			--- dass.schedule.on_failure(fn) -> callback_registration
			  Subscribes one function to the failures of EVERY job of the chat.
			  fn(handle, error, count) - handle is the job that failed, error is the exception message,
			  count is how many failures in a row it has now.
			  Cancel the subscription with registration:cancel().

			--- dass.schedule.on_failure_disable(fn) -> callback_registration
			  Subscribes one function to the auto-disable of EVERY job of the chat.
			  fn(handle, error) - fires right after the third failure in a row disabled the job.

			SCHEDULE HANDLE:

			  Properties:
			    - id: number - unique inside the chat
			    - kind: string - "every", "cron" or "at"
			    - spec: string - the rule as text
			    - enabled: boolean - read and write; false stops the job but keeps it listed
			    - running: boolean - a firing is executing right now
			    - next_run: number|nil - Unix timestamp of the next firing
			    - run_count: number - firings attempted, failures included
			    - fail_count: number - failures in a row; a success resets it
			    - skipped: number - cron only: occurrences dropped because the previous firing was still running
			    - last_run: number|nil - Unix timestamp of the last firing
			    - last_error: string|nil - message of the last failure

			  Methods:
				- h:cancel() -> boolean - removes the job from the chat
				- h:on_failure(fn) -> callback_registration - same subscription, but only for this job;
				  fn(handle, error, count)
				- h:on_failure_disable(fn) -> callback_registration - only for this job; fn(handle, error)

			NOTES:
			  - Three failures in a row disable the job; nothing is delivered to the agents and nothing is
				written to the chat. Subscribe with on_failure / on_failure_disable to act on it, and set
				h.enabled = true to bring a disabled job back.
			  - A firing has no tool result and no print channel: its output goes to the application log.
			    To be heard, use dass.chat.send or dass.notification.
			  - Inside a firing dass.tool.result and the command/tool ambient context are absent.
			  - A chat holds at most 256 jobs; registering one more is an error.

			EXAMPLES:

			  -- Every 10 minutes, aligned to the clock
			  dass.schedule.every(function(missed, last)
			    if missed > 0 then print("skipped " .. missed .. " ticks") end
			    -- ...
			  end, { period = 600000, from = time.now() })

			  -- Every weekday at 09:00, and on startup tell me what was missed
			  dass.schedule.cron(function(missed)
			    if missed == -1 then print("first run") end
			    if missed and missed > 0 then print(missed .. " days were missed") end
			    -- ...
			  end, { expression = "0 9 * * 1-5", catch_slot = "morning" })

			  -- Once, in an hour
			  dass.schedule.at(function() print("late") end, time.now() + 3600)

			  -- Cancel everything on close
			  dass.chat.on_close(function() dass.schedule.clear() end)

			  -- React to failures of every job
			  dass.schedule.on_failure(function(handle, err, count)
				print("job " .. handle.id .. " failed " .. count .. "x: " .. err)
			  end)
			""";

		public override Action? Populate(LuaTable globals, LuaTable ns, LuaService luaService)
		{
			ns["every"] = new LuaCallbackFunction((ctx, args) => Every(ctx, args, luaService));
			ns["cron"] = new LuaCallbackFunction((ctx, args) => Cron(ctx, args, luaService));
			ns["at"] = new LuaCallbackFunction((ctx, args) => At(ctx, args, luaService));
			ns["list"] = new LuaCallbackFunction(List);
			ns["clear"] = new LuaCallbackFunction(Clear);
			ns["on_failure"] = new LuaCallbackFunction(OnFailure);
			ns["on_failure_disable"] = new LuaCallbackFunction(OnFailureDisable);

			scheduler.Lua = luaService;

			return scheduler.Reset;
		}

		/// <summary>
		/// Registers a grid job.
		/// </summary>
		private LuaTuple Every(LuaCallingContext ctx, LuaValue[] args, LuaService luaService)
		{
			const string call = "dass.schedule.every(fn, options)";
			var function = ReadFunction(args, call, out var options);

			if (options.Get("period") is not LuaNumber period)
				throw new LuaRuntimeException($"{call}: 'period' is required and must be a number of milliseconds.");

			if (period.Value < minPeriodMs)
				throw new LuaRuntimeException($"{call}: 'period' must be at least {minPeriodMs} milliseconds.");

			var delay = ReadNumber(options, "delay") ?? 0;
			var from = ReadNumber(options, "from") ?? DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() / 1000.0;
			var grid = ReadBoolean(options, "grid", true);

			EnsureRoom(call);

			var handle = LuaScheduleHandle.ForEvery(scheduler, scheduler.TakeId(), function, luaService,
				(long)Math.Round(period.Value),
				(long)Math.Round(delay),
				(long)Math.Round(from * 1000),
				grid);

			scheduler.Register(handle);
			return new LuaTuple(LuaScheduleHandle.Wrap(handle));
		}

		/// <summary>
		/// Registers a calendar job.
		/// </summary>
		private LuaTuple Cron(LuaCallingContext ctx, LuaValue[] args, LuaService luaService)
		{
			const string call = "dass.schedule.cron(fn, options)";
			var function = ReadFunction(args, call, out var options);

			if (options.Get("expression") is not LuaString expression || string.IsNullOrWhiteSpace(expression.Value))
				throw new LuaRuntimeException($"{call}: 'expression' is required and must be a non-empty string.");

			CronExpression cron;
			try
			{
				cron = CronExpression.Parse(expression.Value);
			}
			catch (FormatException ex)
			{
				throw new LuaRuntimeException($"{call}: {ex.Message}.");
			}

			string? catchSlot = null;
			var slot = options.Get("catch_slot");
			if (slot is not LuaNil)
			{
				if (slot is not LuaString slotName || string.IsNullOrWhiteSpace(slotName.Value))
					throw new LuaRuntimeException($"{call}: 'catch_slot' must be a non-empty string.");

				catchSlot = slotName.Value;
			}

			EnsureRoom(call);

			var handle = LuaScheduleHandle.ForCron(scheduler, scheduler.TakeId(), function, luaService,
				cron, catchSlot);

			scheduler.Register(handle);
			return new LuaTuple(LuaScheduleHandle.Wrap(handle));
		}

		/// <summary>
		/// Registers a one-shot job.
		/// </summary>
		private LuaTuple At(LuaCallingContext ctx, LuaValue[] args, LuaService luaService)
		{
			const string call = "dass.schedule.at(fn, timestamp)";

			if (args.Length == 0 || args[0] is not LuaFunction function)
				throw new LuaRuntimeException($"{call}: a function is expected as the first argument.");

			if (args.Length < 2 || args[1] is not LuaNumber timestamp)
				throw new LuaRuntimeException($"{call}: a Unix timestamp is expected as the second argument.");

			var atMs = (long)Math.Round(timestamp.Value * 1000);
			if (atMs <= DateTimeOffset.UtcNow.ToUnixTimeMilliseconds())
				throw new LuaRuntimeException($"{call}: the timestamp is in the past.");

			EnsureRoom(call);

			var handle = LuaScheduleHandle.ForAt(scheduler, scheduler.TakeId(), function, luaService, atMs);

			scheduler.Register(handle);
			return new LuaTuple(LuaScheduleHandle.Wrap(handle));
		}

		/// <summary>
		/// Lists the jobs of the chat as handles.
		/// </summary>
		private LuaTuple List(LuaCallingContext ctx, LuaValue[] args)
		{
			var table = new LuaTable();
			var index = 1;

			foreach (var job in scheduler.Snapshot())
				table.Set(index++, LuaScheduleHandle.Wrap(job));

			return new LuaTuple(table);
		}

		/// <summary>
		/// Cancels every job of the chat.
		/// </summary>
		private LuaTuple Clear(LuaCallingContext ctx, LuaValue[] args)
			=> new(new LuaNumber(scheduler.Clear()));

		/// <summary>
		/// Subscribes a function to the failures of every job of the chat.
		/// </summary>
		private LuaTuple OnFailure(LuaCallingContext ctx, LuaValue[] args)
		{
			if (args.Length == 0 || args[0] is not LuaFunction function)
				throw new LuaRuntimeException("dass.schedule.on_failure(fn): a function is expected.");

			return new LuaTuple(LuaCallbackRegistration.Wrap(scheduler.RegisterFailure(function, ctx)));
		}

		/// <summary>
		/// Subscribes a function to the auto-disable of every job of the chat.
		/// </summary>
		private LuaTuple OnFailureDisable(LuaCallingContext ctx, LuaValue[] args)
		{
			if (args.Length == 0 || args[0] is not LuaFunction function)
				throw new LuaRuntimeException("dass.schedule.on_failure_disable(fn): a function is expected.");

			return new LuaTuple(LuaCallbackRegistration.Wrap(scheduler.RegisterDisabled(function, ctx)));
		}

		/// <summary>
		/// Reads the callback and the mandatory options table.
		/// </summary>
		private static LuaFunction ReadFunction(LuaValue[] args, string call, out LuaTable options)
		{
			if (args.Length == 0 || args[0] is not LuaFunction function)
				throw new LuaRuntimeException($"{call}: a function is expected as the first argument.");

			if (args.Length < 2 || args[1] is not LuaTable table)
				throw new LuaRuntimeException($"{call}: an options table is required as the second argument.");

			options = table;
			return function;
		}

		private static double? ReadNumber(LuaTable options, string key)
			=> options.Get(key) is LuaNumber number ? number.Value : null;

		private static bool ReadBoolean(LuaTable options, string key, bool fallback)
		{
			var value = options.Get(key);
			return value is LuaNil ? fallback : value.ToBoolean();
		}

		private void EnsureRoom(string call)
		{
			if (scheduler.Count >= maxJobs)
				throw new LuaRuntimeException($"{call}: the chat already holds {maxJobs} scheduled jobs; cancel one first.");
		}
	}
}
