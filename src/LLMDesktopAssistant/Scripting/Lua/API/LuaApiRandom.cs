using AsyncLua;
using AsyncLua.Values;

namespace LLMDesktopAssistant.Scripting.Lua.API
{
	/// <summary>
	/// Lua API for random number generation and sampling: <c>random.*</c>.
	/// Registered in the global namespace as "random".
	/// </summary>
	[LuaApi(chatScoped: false)]
	public class LuaApiRandom : LuaApiBase
	{
		public override string? Namespace => "random";

		public override string? Manuals => """
			--- random - random number generation and sampling API

			Provides various randomisation functions backed by System.Random.

			FUNCTIONS:

			--- random.number([min], max)
			  Returns a random floating-point number.
			  If called with one argument: returns [0, max].
			  If called with two arguments: returns [min, max].
			  Parameters:
			    - min: number (optional) - lower bound (inclusive), default 0
			    - max: number - upper bound (inclusive)
			  Returns: number

			--- random.integer([min], max)
			  Returns a random integer.
			  If called with one argument: returns [1, max].
			  If called with two arguments: returns [min, max] (both inclusive).
			  Parameters:
			    - min: number (optional) - lower bound (inclusive), default 1
			    - max: number - upper bound (inclusive)
			  Returns: integer

			--- random.boolean([chance])
			  Returns true or false with the given probability.
			  Parameters:
			    - chance: number (optional, default 0.5) - probability of true, 0 to 1
			  Returns: boolean

			--- random.choice(array, [n])
			  Returns random element(s) from an array table.
			  If n is nil or 1: returns a single random element.
			  If n > 1: returns an array of n unique random elements.
			  Parameters:
			    - array: table - array to pick from (1-based, non-sparse)
			    - n: number (optional) - number of elements to pick
			  Returns: element or array of elements
			  Throws if n > #array.

			--- random.shuffle(array)
			  Shuffles the array in-place using Fisher-Yates algorithm.
			  Parameters:
			    - array: table - array to shuffle (1-based)
			  Returns: the same table (mutated)

			--- random.guid()
			  Generates a random GUID/UUID string.
			  Returns: string - e.g. "550e8400-e29b-41d4-a716-446655440000"

			--- random.bytes(n)
			  Returns an array of n random bytes (0-255).
			  Parameters:
			    - n: number - number of bytes to generate
			  Returns: table - array of integers

			--- random.seed([seed])
			  Seeds the random number generator.
			  If called without arguments, seeds with Guid.NewGuid().
			  Parameters:
			    - seed: number (optional) - explicit seed value
			  Returns: nil

			EXAMPLES:

			  -- Dice roll
			  local roll = random.integer(1, 6)

			  -- Random float
			  local pct = random.number(0, 1)

			  -- 30% chance
			  if random.boolean(0.3) then print("lucky!") end

			  -- Pick one
			  local pick = random.choice({"rock", "paper", "scissors"})

			  -- Pick many
			  local picks = random.choice({"a","b","c","d","e"}, 3)

			  -- Shuffle
			  local deck = {1,2,3,4,5,6}
			  random.shuffle(deck)

			  -- GUID
			  print(random.guid())
			""";

		private Random _rng = new();

		public override Action? Populate(LuaTable globals, LuaTable ns, LuaService luaService)
		{
			ns["number"] = new LuaCallbackFunction(Number);
			ns["integer"] = new LuaCallbackFunction(Integer);
			ns["boolean"] = new LuaCallbackFunction(Boolean);
			ns["choice"] = new LuaCallbackFunction(Choice);
			ns["shuffle"] = new LuaCallbackFunction(Shuffle);
			ns["guid"] = new LuaCallbackFunction(Guid);
			ns["bytes"] = new LuaCallbackFunction(Bytes);
			ns["seed"] = new LuaCallbackFunction(Seed);
			return null;
		}

		private LuaTuple Number(LuaCallingContext ctx, LuaValue[] args)
		{
			if (args.Length == 0)
				throw new LuaRuntimeException("random.number([min], max): at least 1 argument expected.");

			if (args.Length == 1)
			{
				if (args[0] is not LuaNumber max)
					throw new LuaRuntimeException("random.number(max): max must be a number.");
				return new LuaTuple(new LuaNumber(_rng.NextDouble() * max.Value));
			}

			if (args[0] is not LuaNumber min || args[1] is not LuaNumber max2)
				throw new LuaRuntimeException("random.number(min, max): both arguments must be numbers.");
			return new LuaTuple(new LuaNumber(min.Value + _rng.NextDouble() * (max2.Value - min.Value)));
		}

		private LuaTuple Integer(LuaCallingContext ctx, LuaValue[] args)
		{
			if (args.Length == 0)
				throw new LuaRuntimeException("random.integer([min], max): at least 1 argument expected.");

			if (args.Length == 1)
			{
				if (args[0] is not LuaNumber max)
					throw new LuaRuntimeException("random.integer(max): max must be a number.");
				return new LuaTuple(new LuaNumber(_rng.Next(1, (int)max.Value + 1)));
			}

			if (args[0] is not LuaNumber min || args[1] is not LuaNumber max2)
				throw new LuaRuntimeException("random.integer(min, max): both arguments must be numbers.");
			return new LuaTuple(new LuaNumber(_rng.Next((int)min.Value, (int)max2.Value + 1)));
		}

		private LuaTuple Boolean(LuaCallingContext ctx, LuaValue[] args)
		{
			double chance = 0.5;
			if (args.Length > 0)
			{
				if (args[0] is not LuaNumber c)
					throw new LuaRuntimeException("random.boolean([chance]): chance must be a number between 0 and 1.");
				chance = c.Value;
				if (chance < 0 || chance > 1)
					throw new LuaRuntimeException("random.boolean([chance]): chance must be between 0 and 1.");
			}
			return new LuaTuple(LuaBoolean.FromBoolean(_rng.NextDouble() < chance));
		}

		private LuaTuple Choice(LuaCallingContext ctx, LuaValue[] args)
		{
			if (args.Length < 1)
				throw new LuaRuntimeException("random.choice(array, [n]): at least 1 argument expected.");

			if (args[0] is not LuaTable array)
				throw new LuaRuntimeException("random.choice(): first argument must be a table (array).");

			var length = array.Length;
			if (length == 0)
				throw new LuaRuntimeException("random.choice(): array is empty.");

			// If n is nil or 1, return a single element
			if (args.Length < 2 || args[1] is LuaNil)
			{
				return new LuaTuple(array.Get(_rng.Next(1, length + 1)));
			}

			if (args[1] is not LuaNumber nVal)
				throw new LuaRuntimeException("random.choice(array, n): n must be a number.");
			int n = (int)nVal.Value;

			if (n < 1)
				throw new LuaRuntimeException("random.choice(array, n): n must be at least 1.");
			if (n > length)
				throw new LuaRuntimeException($"random.choice(array, n): n ({n}) exceeds array length ({length}).");

			// Fisher-Yates partial shuffle - pick first n elements
			var result = new LuaTable();

			// Copy indices
			var indices = new int[length];
			for (int i = 0; i < length; i++)
				indices[i] = i + 1;

			// Partial Fisher-Yates
			for (int i = 0; i < n; i++)
			{
				int j = _rng.Next(i, length);
				// swap
				int temp = indices[i];
				indices[i] = indices[j];
				indices[j] = temp;

				result[i + 1] = array.Get(indices[i]);
			}

			return new LuaTuple(result);
		}

		private LuaTuple Shuffle(LuaCallingContext ctx, LuaValue[] args)
		{
			if (args.Length < 1)
				throw new LuaRuntimeException("random.shuffle(array): at least 1 argument expected.");

			if (args[0] is not LuaTable t)
				throw new LuaRuntimeException("random.shuffle(): first argument must be a table (array).");

			int len = t.Length;
			if (len <= 1)
				return new LuaTuple(t);

			// Fisher-Yates in-place
			for (int i = len; i >= 2; i--)
			{
				int j = _rng.Next(1, i + 1);
				// swap t[i] and t[j]
				var tmp = t.Get(i);
				t.Set(i, t.Get(j));
				t.Set(j, tmp);
			}

			return new LuaTuple(t);
		}

		private static LuaTuple Guid(LuaCallingContext ctx, LuaValue[] args)
		{
			return new LuaTuple(new LuaString(System.Guid.NewGuid().ToString()));
		}

		private LuaTuple Bytes(LuaCallingContext ctx, LuaValue[] args)
		{
			if (args.Length < 1)
				throw new LuaRuntimeException("random.bytes(n): at least 1 argument expected.");

			if (args[0] is not LuaNumber nVal)
				throw new LuaRuntimeException("random.bytes(n): n must be a number.");
			int n = (int)nVal.Value;
			if (n < 0)
				throw new LuaRuntimeException("random.bytes(n): n must be non-negative.");

			var buffer = new byte[n];
			_rng.NextBytes(buffer);

			var result = new LuaTable();
			for (int i = 0; i < n; i++)
				result[i + 1] = new LuaNumber(buffer[i]);

			return new LuaTuple(result);
		}

		private LuaTuple Seed(LuaCallingContext ctx, LuaValue[] args)
		{
			if (args.Length == 0 || args[0] is LuaNil)
			{
				_rng = new();
				return new LuaTuple(LuaNil.Instance);
			}

			if (args[0] is not LuaNumber seedVal)
				throw new LuaRuntimeException("random.seed([seed]): seed must be a number.");
			_rng = new Random((int)seedVal.Value);
			return new LuaTuple(LuaNil.Instance);
		}
	}
}
