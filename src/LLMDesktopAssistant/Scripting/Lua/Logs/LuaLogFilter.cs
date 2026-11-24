using AsyncLua;
using AsyncLua.Values;
using LLMDesktopAssistant.Utils;
using Serilog.Events;

namespace LLMDesktopAssistant.Scripting.Lua.Logs
{
	/// <summary>
	/// The filter shared by <c>dass.log.query</c> and <c>dass.log.subscribe</c>.
	/// </summary>
	/// <remarks>
	/// Every field narrows the set: an entry must be at or above <c>min_level</c>, be listed in <c>levels</c>
	/// when one is given, contain <c>text</c> and be newer than <c>since</c>. <c>last</c> is not a predicate but
	/// the size of the tail window the caller keeps, applied after everything else matched.
	/// </remarks>
	public class LuaLogFilter
	{
		/// <summary>
		/// Initialises a filter.
		/// </summary>
		/// <param name="minLevel">The lowest level that passes.</param>
		/// <param name="levels">The accepted levels, or <see langword="null"/> when any level passes.</param>
		/// <param name="text">The case-insensitive substring the rendered message or the template must contain, or <see langword="null"/>.</param>
		/// <param name="since">The last sequence the caller has already seen.</param>
		/// <param name="last">How many newest matching entries to return, or <see cref="int.MaxValue"/> for all of them.</param>
		public LuaLogFilter(LogEventLevel minLevel, ImmutableHashSet<LogEventLevel>? levels, string? text,
			long since, int last)
		{
			MinLevel = minLevel;
			Levels = levels;
			Text = text;
			Since = since;
			Last = last;
		}

		/// <summary>
		/// The lowest level that passes.
		/// </summary>
		public LogEventLevel MinLevel { get; }

		/// <summary>
		/// The accepted levels, or <see langword="null"/> when any level passes.
		/// </summary>
		public ImmutableHashSet<LogEventLevel>? Levels { get; }

		/// <summary>
		/// The case-insensitive substring the rendered message or the template must contain,
		/// or <see langword="null"/>.
		/// </summary>
		public string? Text { get; }

		/// <summary>
		/// The last sequence the caller has already seen.
		/// </summary>
		public long Since { get; }

		/// <summary>
		/// How many newest matching entries to return, or <see cref="int.MaxValue"/> for all of them.
		/// </summary>
		public int Last { get; }

		/// <summary>
		/// Parses a filter table, or returns the pass-everything filter for a missing one.
		/// </summary>
		/// <param name="value">The filter table or <see langword="null"/>.</param>
		/// <param name="call">The Lua call the filter came from, used in error messages.</param>
		/// <exception cref="LuaRuntimeException">The value is not a table or a field has the wrong shape.</exception>
		public static LuaLogFilter Parse(LuaValue? value, string call)
		{
			if (value is null or LuaNil)
				return new LuaLogFilter(LogEventLevel.Verbose, null, null, 0, int.MaxValue);

			if (value is not LuaTable table)
				throw new LuaRuntimeException($"{call}: the filter must be a table or nil.");

			var minLevel = LogEventLevel.Verbose;
			if (table.Get("min_level") is { } minLevelValue && minLevelValue is not LuaNil)
			{
				if (minLevelValue is not LuaString minLevelString)
					throw new LuaRuntimeException($"{call}: filter.min_level must be a string.");

				if (!LuaLogLevels.TryParse(minLevelString.Value, out minLevel))
					throw new LuaRuntimeException(
						$"{call}: filter.min_level must be one of: {LuaLogLevels.Names}, got '{minLevelString.Value}'.");
			}

			ImmutableHashSet<LogEventLevel>? levels = null;
			if (table.Get("levels") is { } levelsValue && levelsValue is not LuaNil)
			{
				if (levelsValue is not LuaTable levelsTable)
					throw new LuaRuntimeException($"{call}: filter.levels must be an array of level names.");

				var builder = ImmutableHashSet.CreateBuilder<LogEventLevel>();
				foreach (var entry in levelsTable.Entries)
				{
					if (entry.Value is not LuaString levelName)
						throw new LuaRuntimeException($"{call}: filter.levels must contain only level names.");

					if (!LuaLogLevels.TryParse(levelName.Value, out var level))
						throw new LuaRuntimeException(
							$"{call}: filter.levels has an unknown level '{levelName.Value}'. Known: {LuaLogLevels.Names}.");

					builder.Add(level);
				}

				levels = builder.ToImmutable();
			}

			string? text = null;
			if (table.Get("text") is { } textValue && textValue is not LuaNil)
			{
				if (textValue is not LuaString textString)
					throw new LuaRuntimeException($"{call}: filter.text must be a string.");

				text = textString.Value;
			}

			long since = 0;
			if (table.Get("since") is { } sinceValue && sinceValue is not LuaNil)
			{
				if (sinceValue is not LuaNumber sinceNumber)
					throw new LuaRuntimeException($"{call}: filter.since must be a number (a sequence).");

				since = (long)sinceNumber.Value;
			}

			var last = int.MaxValue;
			if (table.Get("last") is { } lastValue && lastValue is not LuaNil)
			{
				if (lastValue is not LuaNumber lastNumber)
					throw new LuaRuntimeException($"{call}: filter.last must be a number.");

				last = lastNumber.Value <= 0 ? 0 : (int)Math.Min(int.MaxValue, lastNumber.Value);
			}

			return new LuaLogFilter(minLevel, levels, text, since, last);
		}

		/// <summary>
		/// Checks whether one buffered event passes this filter.
		/// </summary>
		public bool Matches(RenderedLogEvent entry)
		{
			if (entry.Sequence <= Since)
				return false;

			if (entry.Level < MinLevel)
				return false;

			if (Levels is not null && !Levels.Contains(entry.Level))
				return false;

			if (Text is not null
				&& !entry.Rendered.Contains(Text, StringComparison.OrdinalIgnoreCase)
				&& !entry.Template.Contains(Text, StringComparison.OrdinalIgnoreCase))
				return false;

			return true;
		}
	}
}
