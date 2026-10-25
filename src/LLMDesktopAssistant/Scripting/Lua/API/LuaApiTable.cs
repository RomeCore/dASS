using AsyncLua;
using AsyncLua.Values;

namespace LLMDesktopAssistant.Scripting.Lua.API
{
	/// <summary>
	/// Lua API with extended table utilities: <c>table.*</c>.
	/// Supplements the built-in table library with commonly needed functions.
	/// </summary>
	[LuaApi(chatScoped: false)]
	public class LuaApiTable : LuaApiBase
	{
		public override string? Namespace => "table";

		public override string? Manuals => """
			--- table - extended table utilities

			Supplements the built-in Lua table library.

			FUNCTIONS:

			--- table.contains(t, value)
			  Checks if a value exists in the table (shallow comparison).
			  Parameters:
			    - t: table - table to search
			    - value: any - value to find
			  Returns: boolean

			--- table.find(t, value)
			  Returns the first key/index where the value is found, or nil.
			  Parameters:
			    - t: table - table to search
			    - value: any - value to find
			  Returns: key or nil

			--- table.map(t, func)
			  Returns a new table where each element is transformed by func(value, key).
			  Parameters:
			    - t: table - source table
			    - func: function(value, key) -> new_value
			  Returns: table

			--- table.filter(t, func)
			  Returns a new table with elements where func(value, key) returns true.
			  Parameters:
			    - t: table - source table
			    - func: function(value, key) -> boolean
			  Returns: table

			--- table.reduce(t, func, initial)
			  Reduces the table to a single value using a binary function.
			  Parameters:
			    - t: table - source table
			    - func: function(accumulator, value, key) -> new_accumulator
			    - initial: any - initial accumulator value
			  Returns: any - final accumulator

			--- table.slice(t, start, [end])
			  Returns a slice of the array (1-based inclusive).
			  Parameters:
			    - t: table - array table
			    - start: number - start index (1-based)
			    - end: number (optional) - end index (inclusive), default: #t
			  Returns: table - new array

			--- table.keys(t)
			  Returns an array of all keys in the table.
			  Parameters:
			    - t: table - source table
			  Returns: table - array of keys

			--- table.values(t)
			  Returns an array of all values in the table.
			  Parameters:
			    - t: table - source table
			  Returns: table - array of values

			--- table.merge(t1, t2)
			  Merges t2 into t1. For arrays, appends elements.
			  For objects, overwrites/replaces keys from t2.
			  Parameters:
			    - t1: table - target table
			    - t2: table - source table
			  Returns: new merged table

			--- table.clone(t)
			  Returns a shallow copy of the table.
			  Parameters:
			    - t: table - source table
			  Returns: table - copy
			
			--- table.deep_clone(t)
			  Returns a deep copy of the table.
			  Parameters:
			    - t: table - source table
			  Returns: table - copy
			
			--- table.is_empty(t)
			  Returns true if the table has no elements (array part and hash part).
			  Parameters:
			    - t: table - source table
			  Returns: boolean

			--- table.size(t)
			  Returns the total number of elements in the table (all keys).
			  Parameters:
			    - t: table - source table
			  Returns: number

			--- table.first(t)
			--- table.last(t)
			  Returns the first/last element of an array table, or nil if empty.
			  Parameters:
			    - t: table - array table
			  Returns: element or nil

			--- table.each(t, func)
			  Iterates over all elements, calling func(value, key) for each.
			  Parameters:
			    - t: table - source table
			    - func: function(value, key)
			  Returns: nil
			
			--- table.times(element, count)
			  Creates a new array table by repeating the given element `count` times.
			  Parameters:
			    - element: any - the value to repeat
			    - count: number - number of repetitions (must be >= 0)
			  Returns: table - array with `count` copies of `element`
			  Example: table.times("hello", 3) -> {"hello", "hello", "hello"}
			
			EXAMPLES:

			  local t = {1, 2, 3, 4, 5}
			  print(table.contains(t, 3)) -- true
			  print(table.find(t, 4))     -- 4
			  local doubled = table.map(t, function(v) return v * 2 end)
			  local evens = table.filter(t, function(v) return v % 2 == 0 end)
			  local sum = table.reduce(t, function(a, v) return a + v end, 0)
			  local head = table.slice(t, 1, 3) -- {1, 2, 3}
			  print(table.first(t)) -- 1
			  print(table.last(t))  -- 5
			""";

		public override Action? Populate(LuaTable globals, LuaTable ns, LuaService luaService)
		{
			ns["contains"] = new LuaCallbackFunction(Contains);
			ns["find"] = new LuaCallbackFunction(Find);
			ns["map"] = new LuaCallbackFunction(Map);
			ns["filter"] = new LuaCallbackFunction(Filter);
			ns["reduce"] = new LuaCallbackFunction(Reduce);
			ns["slice"] = new LuaCallbackFunction(Slice);
			ns["keys"] = new LuaCallbackFunction(Keys);
			ns["values"] = new LuaCallbackFunction(Values);
			ns["merge"] = new LuaCallbackFunction(Merge);
			ns["clone"] = new LuaCallbackFunction(Clone);
			ns["deep_clone"] = new LuaCallbackFunction(DeepClone);
			ns["is_empty"] = new LuaCallbackFunction(IsEmpty);
			ns["size"] = new LuaCallbackFunction(Size);
			ns["first"] = new LuaCallbackFunction(First);
			ns["last"] = new LuaCallbackFunction(Last);
			ns["each"] = new LuaCallbackFunction(Each);
			ns["times"] = new LuaCallbackFunction(Times);
			return null;
		}

		private static LuaTuple Contains(LuaCallingContext ctx, LuaValue[] args)
		{
			if (args.Length < 2)
				throw new LuaRuntimeException("table.contains(t, value): at least 2 arguments expected.");
			if (args[0] is not LuaTable t)
				throw new LuaRuntimeException("table.contains(): first argument must be a table.");
			var value = args[1];

			foreach (var kv in t.Entries)
			{
				if (ValuesEqual(kv.Value, value))
					return new LuaTuple(LuaBoolean.True);
			}
			return new LuaTuple(LuaBoolean.False);
		}

		private static LuaTuple Find(LuaCallingContext ctx, LuaValue[] args)
		{
			if (args.Length < 2)
				throw new LuaRuntimeException("table.find(t, value): at least 2 arguments expected.");
			if (args[0] is not LuaTable t)
				throw new LuaRuntimeException("table.find(): first argument must be a table.");
			var value = args[1];

			foreach (var kv in t.Entries)
			{
				if (ValuesEqual(kv.Value, value))
					return new LuaTuple(kv.Key);
			}
			return new LuaTuple(LuaNil.Instance);
		}

		private static LuaTuple Map(LuaCallingContext ctx, LuaValue[] args)
		{
			if (args.Length < 2)
				throw new LuaRuntimeException("table.map(t, func): at least 2 arguments expected.");
			if (args[0] is not LuaTable t)
				throw new LuaRuntimeException("table.map(): first argument must be a table.");
			if (args[1] is not LuaFunction func)
				throw new LuaRuntimeException("table.map(): second argument must be a function.");

			var result = new LuaTable();
			foreach (var kv in t.Entries)
			{
				var newVal = func.Invoke(ctx, kv.Value, kv.Key);
				result.Set(kv.Key, newVal.Count > 0 ? newVal[0] : LuaNil.Instance);
			}
			return new LuaTuple(result);
		}

		private static LuaTuple Filter(LuaCallingContext ctx, LuaValue[] args)
		{
			if (args.Length < 2)
				throw new LuaRuntimeException("table.filter(t, func): at least 2 arguments expected.");
			if (args[0] is not LuaTable t)
				throw new LuaRuntimeException("table.filter(): first argument must be a table.");
			if (args[1] is not LuaFunction func)
				throw new LuaRuntimeException("table.filter(): second argument must be a function.");

			var isArray = IsArray(t);
			var result = new LuaTable();

			if (isArray)
			{
				int idx = 1;
				for (int i = 1; i <= t.Length; i++)
				{
					var val = t.Get(i);
					var keep = func.Invoke(ctx, val, new LuaNumber(i));
					if (keep.Count > 0 && keep[0] is LuaBoolean bKeep && bKeep.Value)
					{
						result[idx] = val;
						idx++;
					}
				}
			}
			else
			{
				foreach (var kv in t.Entries)
				{
					var keep = func.Invoke(ctx, kv.Value, kv.Key);
					if (keep.Count > 0 && keep[0] is LuaBoolean bKeep && bKeep.Value)
						result.Set(kv.Key, kv.Value);
				}
			}

			return new LuaTuple(result);
		}

		private static bool IsArray(LuaTable t)
		{
			int len = t.Length;
			if (len == 0)
			{
				foreach (var _ in t.Entries)
					return false;
				return true;
			}
			for (int i = 1; i <= len; i++)
			{
				if (t.Get(i) is LuaNil)
					return false;
			}
			foreach (var kv in t.Entries)
			{
				if (kv.Key is not LuaNumber)
					return false;
			}
			return true;
		}

		private static LuaTuple Reduce(LuaCallingContext ctx, LuaValue[] args)
		{
			if (args.Length < 3)
				throw new LuaRuntimeException("table.reduce(t, func, initial): at least 3 arguments expected.");
			if (args[0] is not LuaTable t)
				throw new LuaRuntimeException("table.reduce(): first argument must be a table.");
			if (args[1] is not LuaFunction func)
				throw new LuaRuntimeException("table.reduce(): second argument must be a function.");
			var acc = args[2];

			foreach (var kv in t.Entries)
			{
				var result = func.Invoke(ctx, acc, kv.Value, kv.Key);
				acc = result.Count > 0 ? result[0] : LuaNil.Instance;
			}
			return new LuaTuple(acc);
		}

		private static LuaTuple Slice(LuaCallingContext ctx, LuaValue[] args)
		{
			if (args.Length < 2)
				throw new LuaRuntimeException("table.slice(t, start, [end]): at least 2 arguments expected.");
			if (args[0] is not LuaTable t)
				throw new LuaRuntimeException("table.slice(): first argument must be a table.");
			if (args[1] is not LuaNumber startVal)
				throw new LuaRuntimeException("table.slice(): start must be a number.");
			int start = (int)startVal.Value;

			int len = t.Length;
			int end = len;
			if (args.Length > 2 && args[2] is not LuaNil)
			{
				if (args[2] is not LuaNumber endVal)
					throw new LuaRuntimeException("table.slice(): end must be a number.");
				end = (int)endVal.Value;
			}

			if (start < 1) start = 1;
			if (end > len) end = len;

			var result = new LuaTable();
			if (start > end)
				return new LuaTuple(result);

			int idx = 1;
			for (int i = start; i <= end; i++)
			{
				result[idx] = t.Get(i);
				idx++;
			}
			return new LuaTuple(result);
		}

		private static LuaTuple Keys(LuaCallingContext ctx, LuaValue[] args)
		{
			if (args.Length < 1)
				throw new LuaRuntimeException("table.keys(t): at least 1 argument expected.");
			if (args[0] is not LuaTable t)
				throw new LuaRuntimeException("table.keys(): first argument must be a table.");

			var result = new LuaTable();
			int idx = 1;
			foreach (var kv in t.Entries)
			{
				result[idx] = kv.Key;
				idx++;
			}
			return new LuaTuple(result);
		}

		private static LuaTuple Values(LuaCallingContext ctx, LuaValue[] args)
		{
			if (args.Length < 1)
				throw new LuaRuntimeException("table.values(t): at least 1 argument expected.");
			if (args[0] is not LuaTable t)
				throw new LuaRuntimeException("table.values(): first argument must be a table.");

			var result = new LuaTable();
			int idx = 1;
			foreach (var kv in t.Entries)
			{
				result[idx] = kv.Value;
				idx++;
			}
			return new LuaTuple(result);
		}

		private static LuaTuple Merge(LuaCallingContext ctx, LuaValue[] args)
		{
			if (args.Length < 2)
				throw new LuaRuntimeException("table.merge(t1, t2): at least 2 arguments expected.");
			if (args[0] is not LuaTable t1 || args[1] is not LuaTable t2)
				throw new LuaRuntimeException("table.merge(): both arguments must be tables.");

			return new LuaTuple(t1.DeepMergeWith(t2));
		}

		private static LuaTuple Clone(LuaCallingContext ctx, LuaValue[] args)
		{
			if (args.Length < 1)
				throw new LuaRuntimeException("table.clone(t): at least 1 argument expected.");
			if (args[0] is not LuaTable t)
				throw new LuaRuntimeException("table.clone(): first argument must be a table.");

			return new LuaTuple(t.ShallowClone());
		}

		private static LuaTuple DeepClone(LuaCallingContext ctx, LuaValue[] args)
		{
			if (args.Length < 1)
				throw new LuaRuntimeException("table.deep_clone(t): at least 1 argument expected.");
			if (args[0] is not LuaTable t)
				throw new LuaRuntimeException("table.deep_clone(): first argument must be a table.");

			return new LuaTuple(t.DeepClone());
		}

		private static LuaTuple IsEmpty(LuaCallingContext ctx, LuaValue[] args)
		{
			if (args.Length < 1)
				throw new LuaRuntimeException("table.is_empty(t): at least 1 argument expected.");
			if (args[0] is not LuaTable t)
				throw new LuaRuntimeException("table.is_empty(): first argument must be a table.");

			foreach (var _ in t.Entries)
				return new LuaTuple(LuaBoolean.False);
			return new LuaTuple(LuaBoolean.True);
		}

		private static LuaTuple Size(LuaCallingContext ctx, LuaValue[] args)
		{
			if (args.Length < 1)
				throw new LuaRuntimeException("table.size(t): at least 1 argument expected.");
			if (args[0] is not LuaTable t)
				throw new LuaRuntimeException("table.size(): first argument must be a table.");

			int count = 0;
			foreach (var _ in t.Entries)
				count++;
			return new LuaTuple(new LuaNumber(count));
		}

		private static LuaTuple First(LuaCallingContext ctx, LuaValue[] args)
		{
			if (args.Length < 1)
				throw new LuaRuntimeException("table.first(t): at least 1 argument expected.");
			if (args[0] is not LuaTable t)
				throw new LuaRuntimeException("table.first(): first argument must be a table.");
			if (t.Length == 0)
				return new LuaTuple(LuaNil.Instance);
			return new LuaTuple(t.Get(1));
		}

		private static LuaTuple Last(LuaCallingContext ctx, LuaValue[] args)
		{
			if (args.Length < 1)
				throw new LuaRuntimeException("table.last(t): at least 1 argument expected.");
			if (args[0] is not LuaTable t)
				throw new LuaRuntimeException("table.last(): first argument must be a table.");
			int len = t.Length;
			if (len == 0)
				return new LuaTuple(LuaNil.Instance);
			return new LuaTuple(t.Get(len));
		}

		private static LuaTuple Each(LuaCallingContext ctx, LuaValue[] args)
		{
			if (args.Length < 2)
				throw new LuaRuntimeException("table.each(t, func): at least 2 arguments expected.");
			if (args[0] is not LuaTable t)
				throw new LuaRuntimeException("table.each(): first argument must be a table.");
			if (args[1] is not LuaFunction func)
				throw new LuaRuntimeException("table.each(): second argument must be a function.");

			foreach (var kv in t.Entries)
				func.Invoke(ctx, kv.Value, kv.Key);
			return new LuaTuple(LuaNil.Instance);
		}

		private static LuaTuple Times(LuaCallingContext ctx, LuaValue[] args)
		{
			if (args.Length < 2)
				throw new LuaRuntimeException("table.repeat(element, count): at least 2 arguments expected.");
			
			if (args[1] is not LuaNumber countVal)
				throw new LuaRuntimeException("table.repeat(): count must be a number.");
			
			int count = (int)countVal.Value;
			if (count < 0)
				throw new LuaRuntimeException("table.repeat(): count must be >= 0.");

			var element = args[0];
			var result = new LuaTable();
			for (int i = 1; i <= count; i++)
				result[i] = element;
			return new LuaTuple(result);
		}

		// --- Helper ---

		private static bool ValuesEqual(LuaValue a, LuaValue b)
		{
			if (a is LuaNil)
				return b is LuaNil;
			if (a is LuaBoolean boolA && b is LuaBoolean boolB)
				return boolA.Value == boolB.Value;
			if (a is LuaNumber numA && b is LuaNumber numB)
				return numA.Value == numB.Value;
			if (a is LuaString strA && b is LuaString strB)
				return strA.Value == strB.Value;
			return a.Equals(b);
		}
	}
}
