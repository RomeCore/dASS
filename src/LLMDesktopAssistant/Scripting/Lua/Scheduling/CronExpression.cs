using System.Globalization;

namespace LLMDesktopAssistant.Scripting.Lua.Scheduling
{
	/// <summary>
	/// A parsed five-field cron expression evaluated against local wall-clock time.
	/// </summary>
	/// <remarks>
	/// The search jumps field-wise instead of walking every minute, and a wall-clock time that does
	/// not exist on the DST switch day is skipped rather than fired.
	/// </remarks>
	public sealed class CronExpression
	{
		private const int catchUpLimit = 10000;
		private const int searchHorizon = 500000;

		private static readonly string[] monthNames =
			["JAN", "FEB", "MAR", "APR", "MAY", "JUN", "JUL", "AUG", "SEP", "OCT", "NOV", "DEC"];

		private static readonly string[] dayNames =
			["SUN", "MON", "TUE", "WED", "THU", "FRI", "SAT"];

		private readonly bool[] _minutes = new bool[60];
		private readonly bool[] _hours = new bool[24];
		private readonly bool[] _daysOfMonth = new bool[32];
		private readonly bool[] _months = new bool[13];
		private readonly bool[] _daysOfWeek = new bool[8];
		private readonly bool _dayOfMonthRestricted;
		private readonly bool _dayOfWeekRestricted;

		private CronExpression(string expression)
		{
			var fields = expression.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);
			if (fields.Length != 5)
				throw new FormatException("a cron expression must have exactly 5 fields: minute hour day-of-month month day-of-week");

			Fill(_minutes, fields[0], 0, 59, null, "minute");
			Fill(_hours, fields[1], 0, 23, null, "hour");
			_dayOfMonthRestricted = Fill(_daysOfMonth, fields[2], 1, 31, null, "day-of-month");
			Fill(_months, fields[3], 1, 12, monthNames, "month");
			_dayOfWeekRestricted = Fill(_daysOfWeek, fields[4], 0, 7, dayNames, "day-of-week");

			// 7 and 0 both name Sunday.
			if (_daysOfWeek[7])
			{
				_daysOfWeek[0] = true;
				_daysOfWeek[7] = false;
			}
		}

		/// <summary>
		/// Parses a five-field cron expression.
		/// </summary>
		/// <param name="expression">The expression, e.g. <c>0 9 * * 1-5</c>.</param>
		/// <exception cref="FormatException">The expression is not a valid five-field cron rule.</exception>
		public static CronExpression Parse(string expression)
		{
			var trimmed = expression?.Trim() ?? string.Empty;
			if (trimmed.Length == 0)
				throw new FormatException("the cron expression is empty");

			return new CronExpression(trimmed);
		}

		/// <summary>
		/// Finds the first occurrence strictly after the given moment.
		/// </summary>
		/// <param name="after">The moment to search from, in local time.</param>
		/// <exception cref="InvalidOperationException">No occurrence falls inside the search horizon.</exception>
		public DateTime NextOccurrence(DateTime after)
		{
			var cursor = Truncate(after).AddMinutes(1);

			for (var i = 0; i < searchHorizon; i++)
			{
				if (!_months[cursor.Month])
				{
					cursor = new DateTime(cursor.Year, cursor.Month, 1, 0, 0, 0, cursor.Kind).AddMonths(1);
					continue;
				}

				if (!DayMatches(cursor))
				{
					cursor = cursor.Date.AddDays(1);
					continue;
				}

				if (!_hours[cursor.Hour])
				{
					cursor = new DateTime(cursor.Year, cursor.Month, cursor.Day, cursor.Hour, 0, 0, cursor.Kind).AddHours(1);
					continue;
				}

				if (!_minutes[cursor.Minute])
				{
					cursor = cursor.AddMinutes(1);
					continue;
				}

				// A wall-clock time inside the spring-forward gap never happens, so it must not fire.
				if (TimeZoneInfo.Local.IsInvalidTime(cursor))
				{
					cursor = cursor.AddMinutes(1);
					continue;
				}

				return cursor;
			}

			throw new InvalidOperationException("the cron expression has no occurrence within the search horizon");
		}

		/// <summary>
		/// Counts the occurrences that fall inside the given interval, capped at the catch-up limit.
		/// </summary>
		/// <param name="afterExclusive">The lower bound, exclusive.</param>
		/// <param name="toInclusive">The upper bound, inclusive.</param>
		public int CountOccurrences(DateTime afterExclusive, DateTime toInclusive)
		{
			var count = 0;
			var cursor = afterExclusive;

			while (count < catchUpLimit)
			{
				DateTime next;
				try
				{
					next = NextOccurrence(cursor);
				}
				catch (InvalidOperationException)
				{
					break;
				}

				if (next > toInclusive)
					break;

				count++;
				cursor = next;
			}

			return count;
		}

		/// <summary>
		/// Matches the day the standard cron way: two restricted day fields are OR-ed together.
		/// </summary>
		private bool DayMatches(DateTime moment)
		{
			if (!_dayOfMonthRestricted && !_dayOfWeekRestricted)
				return true;

			var dayOfMonth = _daysOfMonth[moment.Day];
			var dayOfWeek = _daysOfWeek[(int)moment.DayOfWeek];

			if (_dayOfMonthRestricted && _dayOfWeekRestricted)
				return dayOfMonth || dayOfWeek;

			return _dayOfMonthRestricted ? dayOfMonth : dayOfWeek;
		}

		/// <summary>
		/// Fills one field and reports whether it restricts the schedule (<c>*</c> does not).
		/// </summary>
		private static bool Fill(bool[] set, string field, int min, int max, string[]? names, string name)
		{
			foreach (var element in field.Split(','))
			{
				if (element.Length == 0)
					throw new FormatException($"cron field '{name}' has an empty element");

				var parts = element.Split('/');
				if (parts.Length > 2)
					throw new FormatException($"cron field '{name}' has an invalid element '{element}'");

				var step = 1;
				if (parts.Length == 2
					&& (!int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out step) || step <= 0))
					throw new FormatException($"cron field '{name}' has an invalid step '{parts[1]}'");

				var range = parts[0];
				int from;
				int to;

				if (range == "*")
				{
					from = min;
					to = max;
				}
				else if (range.Contains('-'))
				{
					var bounds = range.Split('-');
					if (bounds.Length != 2)
						throw new FormatException($"cron field '{name}' has an invalid range '{range}'");

					from = ParseValue(bounds[0], names, name, isMonth: name == "month");
					to = ParseValue(bounds[1], names, name, isMonth: name == "month");
				}
				else
				{
					from = ParseValue(range, names, name, isMonth: name == "month");
					to = parts.Length == 2 ? max : from;
				}

				if (from < min || from > max || to < min || to > max || from > to)
					throw new FormatException($"cron field '{name}' value is out of range in '{element}' (allowed {min}..{max})");

				for (var value = from; value <= to; value += step)
					set[value] = true;
			}

			return field != "*";
		}

		/// <summary>
		/// Reads one value, accepting a three-letter month or weekday name.
		/// </summary>
		private static int ParseValue(string text, string[]? names, string name, bool isMonth)
		{
			if (names is not null)
			{
				var index = Array.FindIndex(names, n => string.Equals(n, text, StringComparison.OrdinalIgnoreCase));
				if (index >= 0)
					return isMonth ? index + 1 : index;
			}

			if (!int.TryParse(text, NumberStyles.None, CultureInfo.InvariantCulture, out var value))
				throw new FormatException($"cron field '{name}' has an invalid value '{text}'");

			return value;
		}

		private static DateTime Truncate(DateTime value)
			=> new(value.Year, value.Month, value.Day, value.Hour, value.Minute, 0, value.Kind);
	}
}
