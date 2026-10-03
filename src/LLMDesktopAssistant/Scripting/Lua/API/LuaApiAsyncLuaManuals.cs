using AsyncLua;
using AsyncLua.Values;

namespace LLMDesktopAssistant.Scripting.Lua.API;

/// <summary>
/// Provides manual/documentation for AsyncLua-specific syntax features
/// that extend standard Lua 5.5.
/// </summary>
[LuaApi(chatScoped: true)]
public class LuaApiAsyncLuaManuals : LuaApiBase
{
	public override string? Namespace => "asynclua";

	public override string? Manuals => """
		--- AsyncLua — Extended Lua Syntax Manuals
		
		AsyncLua 5.5+$ASYNCLUA_VER is an extended Lua 5.5 interpreter with built-in async/await,
		concurrency primitives, augmented assignments, and exception handling.
		
		Below are the supported extended features with examples.
		
		───────────────────────────────────────────────────────────────
		ASYNC / AWAIT
		───────────────────────────────────────────────────────────────
		
		Declare an async function with `async function` (or `local async function`):
		
		  async function fetchData()
		      await task.delay(100)
		      return 'data received'
		  end
		  
		  local result = await fetchData()
		  print(result)  -- "data received"
		
		Use `await` to wait for a task (async function call) to complete.
		The `await` keyword can also be used in expressions:
		
		  local task1 = someAsyncFunc()
		  -- ... do other work ...
		  local result = await task1
		
		`await` can receive multiple return values:
		
		  local a, b, c = await getMultipleResults()

		`await` can be used as a statement to await multiple tasks concurrently:

		  await task1, task2, task3

		But when you need to get multiple results from multiple tasks, use `await`
		on every task separately:

		  local result1, result2 = await task1, await task2

		Also functions can be NOT defined as `async` but have `await` statements inside,
		these functions will be WAITED by the interpreter, so this:
		
		  local async function fetchData()
		      await task.delay(100)
			  return 'data received'
		  end
		  local result = await fetchData()
		
		Will work as same as this:
		
		  -- No async keyword
		  local function fetchDataSync()
		      await task.delay(100)
			  return 'data received'
		  end
		  local result = fetchDataSync() -- Will be awaited by interpreter, the code will sleep at this moment

		And if we deep dive to know how interpreter works, the code will run like:

		  await task.delay(100)
		  local result = 'data received'

		───────────────────────────────────────────────────────────────
		DELAY
		───────────────────────────────────────────────────────────────
		
		`task.delay(ms)` — returns a task that completes after the specified milliseconds:

		  await task.delay(500)  -- waits 500ms
		  await task.delay(0.5) -- waits 500us

		───────────────────────────────────────────────────────────────
		RUN (fire-and-forget)
		───────────────────────────────────────────────────────────────
		
		`task.run(asyncFunc, args...)` — launches an sync/async function in another thread.
		Can be useful on synchronous functions that take a long time to complete
		(long-running cycles and calculations without async features).
		The function runs concurrently in the background:
		
		  local task1 = task.run(doSomeHeavyWork, arg1, arg2, ...)  -- note: pass function, NOT call it
		  print('Main continues...')
		  local result = await task1
		  print('Result: ' .. result)
		
		───────────────────────────────────────────────────────────────
		PARARUN (Parallel Run)
		───────────────────────────────────────────────────────────────
		
		`task.pararun(table, transformer, [concurrency_level])` — iterates over a table
		and applies the transformer function to each element concurrently:
		
		  local result = await task.pararun({1, 2, 3, 4}, function(item) return item * 2 end, 2)  -- note: pass function, NOT call it
		  print(result[1], result[2], result[3], result[4])  -- prints: 2, 4, 6, 8
		
		───────────────────────────────────────────────────────────────
		CREATE (TaskCompletionSource-like handle)
		───────────────────────────────────────────────────────────────

		`task.create()` — creates a pending task that behaves like a TaskCompletionSource:
		it can be completed later with result values or with an error, and it is awaitable.
		There is no cancellation.

		Complete it with results:

		  local t = task.create()
		  t:set_result(42, 'done')
		  local number, text = await t
		  print(number, text)  -- 42	done

		Complete it with an error (awaiting will throw):

		  local t = task.create()
		  t:set_error('something went wrong')
		  try
			  await t
		  catch e do
			  print(e)  -- "something went wrong"
		  end

		The handle is a normal awaitable task, so it can also be stored and completed
		elsewhere (e.g. from a UI callback), then awaited:

		  local t = task.create()
		  _G.pending = t          -- hand the handle to some async producer
		  local value = await t   -- suspends until set_result / set_error is called

		METHODS:
		  - t:set_result(...)      — completes the task with the given values (multiple results allowed)
		  - t:set_error([message]) — completes the task with an error (default message: "Task failed.")

		───────────────────────────────────────────────────────────────
		IS_ASYNC
		───────────────────────────────────────────────────────────────
		
		`is_async(func)` — returns true if the given value is an async function:
		
		  print(is_async(syncFunc))   -- false
		  print(is_async(asyncFunc))  -- true
		  print(is_async(42))         -- nil, not a function
		
		───────────────────────────────────────────────────────────────
		PCALL_ASYNC / XPCALL_ASYNC
		───────────────────────────────────────────────────────────────
		
		`pcall_async(asyncFunc, ...)` — protected call for async functions.
		Returns (ok, result...) like standard pcall, but works with async:
		
		  local ok, err = await pcall_async(dangerousAsyncFunc)
		  if not ok then
		      print('Error:', err)
		  end
		
		`xpcall_async(asyncFunc, errHandler)` — async version of xpcall
		with a custom error handler:
		
		  local function handler(err)
		      return 'Handled: ' .. err
		  end
		  local ok, result = await xpcall_async(dangerousFunc, handler)
		
		───────────────────────────────────────────────────────────────
		TRY / CATCH / THROW
		───────────────────────────────────────────────────────────────
		
		Structured exception handling. Unlike standard Lua's pcall/error,
		AsyncLua provides native try-catch syntax:
		
		  try
		      -- Protected block
		      throw 'something went wrong'
		      result = 0  -- never reached
		  catch e do
		      -- e contains the error message as a string
		      result = 'caught: ' .. e
		  end
		
		`catch` without an error variable is also valid:
		
		  try
		      throw 'err'
		  catch
		      result = 42
		  end
		
		`throw` can raise a string as exception message (works same as error() in Lua):
		
		  throw 'error message'
		
		Throws from inside `catch` propagate to outer handlers (not
		re-caught by the same catch block):
		
		  try
		      try
		          throw 'inner'
		      catch e do
		          throw 'from catch: ' .. e  -- propagates to outer
		      end
		  catch e do
		      print('Outer caught:', e)  -- "from catch: inner"
		  end
		
		───────────────────────────────────────────────────────────────
		LOCK / MUTEX
		───────────────────────────────────────────────────────────────
		
		Critical sections using `lock mutex do ... end`.
		`mutex` is any Lua table used as a synchronization primitive:
		
		  local mutex = {}
		  
		  async function doWork()
		      lock mutex do
		          -- Critical section: only one coroutine enters at a time
		          local temp = shared_counter
		          await task.delay(10)  -- lock is held during await!
		          shared_counter = temp + 1
		      end
		  end
		
		Key behaviours:
		- Lock is automatically released when the block exits (or on throw)
		- Nested locks on different mutexes are allowed (released in reverse order)
		- Lock can be applied to any value (table, string, number)
		- `lock` is reentrant: same mutex can be locked multiple times by the same coroutine
		
		───────────────────────────────────────────────────────────────
		AUGMENTED ASSIGNMENTS
		───────────────────────────────────────────────────────────────
		
		Compound assignment operators that modify a variable in place.
		Supported operators:
		
		  x += 5    -- addition
		  x -= 3    -- subtraction
		  x *= 2    -- multiplication
		  x /= 4    -- division
		  x //= 2   -- integer division (floor)
		  x %= 3    -- modulo
		  x ^= 2    -- power
		  s ..= " world" -- string concatenation
		  x &= 0x0F -- bitwise AND
		  x |= 0xF0 -- bitwise OR
		  x ~= 0xFF -- bitwise XOR
		  x <<= 2   -- bitwise shift left
		  x >>= 1   -- bitwise shift right
		
		Works with local variables, global variables, and table indices:
		
		  local x = 10
		  x += 5
		  
		  t = {}  -- global table
		  t.value = 10
		  t.value *= 2
		
		───────────────────────────────────────────────────────────────
		CONTINUE
		───────────────────────────────────────────────────────────────
		
		`continue` skips the current loop iteration and proceeds to the next.
		Works inside `for`, `while`, and `repeat` loops.
		
		  for i = 1, 10 do
		      if i == 3 then
		          continue
		      end
		      print(i)
		  end
		
		───────────────────────────────────────────────────────────────
		ADDITIONAL OPERATORS
		───────────────────────────────────────────────────────────────
		
		Standard Lua operators — all work as expected:
		
		  ^   Power:          2 ^ 3 = 8
		  //  Floor division: 7 // 3 = 2
		  %   Modulo:         10 % 3 = 1
		  ..  Concatenation:  "hello" .. " world"
		  #   Length:         #table, #string
		  not Logic NOT:      not true = false
		  -   Unary minus:    -42
		  &   Bitwise AND:    0xFF & 0x0F = 0x0F
		  |   Bitwise OR:     0xF0 | 0x0F = 0xFF
		  ~   Bitwise XOR:    0xFF ~ 0x0F = 0xF0
		  <<  Left shift:     1 << 4 = 16
		  >>  Right shift:    16 >> 2 = 4
		
		───────────────────────────────────────────────────────────────
		GOTO / LABELS
		───────────────────────────────────────────────────────────────
		
		Standard Lua goto with named labels:
		
		  goto my_label
		  print('skipped')
		  ::my_label::
		  print('here')
		
		───────────────────────────────────────────────────────────────
		EXAMPLES — FULL PATTERNS
		───────────────────────────────────────────────────────────────
		
		-- Concurrent execution with lock
		local mutex = {}
		local counter = 0
		
		async function increment(amount, name)
		    lock mutex do
		        local temp = counter
		        await task.delay(50)
		        counter = temp + amount
		        print(name .. " set counter to " .. counter)
		    end
		end
		
		-- Launch all tasks concurrently
		local t1 = increment(10, "A")
		local t2 = increment(20, "B")
		local t3 = increment(30, "C")
		
		-- Wait for all
		await t1
		await t2
		await t3
		print("Final counter:", counter)  -- 60
		
		-- Safe async call with pcall_async and try-catch
		
		async function riskyOperation()
		    if math.random() > 0.5 then
		        throw "random failure"
		    end
		    return "success"
		end
		
		-- Alternative 1 (try-catch)
		try
		    local result = await riskyOperation()
		    print("Result:", result)
		catch e do
		    print("Operation failed:", e)
		end
		
		-- Alternative 2 (pcall_async)
		local ok, result = await pcall_async(riskyOperation)
		if ok then
		    print("Result:", result)
		else
		    print("Operation failed:", result)
		end
		
		-- Alternative 3
		-- Remember that sync functions (pcall in this example) is waited automatically by the interpreter,
		-- and pcall can can take both sync and async functions.
		local ok, result = pcall(riskyOperation)
		if ok then
		    print("Result:", result)
		else
		    print("Operation failed:", result)
		end
		""".Replace("$ASYNCLUA_VER", typeof(LuaState).Assembly.GetName().Version?.ToString() ?? "");

	public override Action? Populate(LuaTable globals, LuaTable ns, LuaService luaService)
	{
		// This class only provides manuals, no runtime API.
		return null;
	}
}
