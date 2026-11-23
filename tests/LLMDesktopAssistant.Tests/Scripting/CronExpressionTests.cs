using LLMDesktopAssistant.Scripting.Lua.Scheduling;

namespace LLMDesktopAssistant.Tests.Scripting
{
	/// <summary>
	/// Covers the five-field cron rule the scheduler evaluates: field syntax, the standard day-of-month
	/// OR day-of-week semantics, and the occurrence counting the catch-up slot relies on.
	/// </summary>
	public class CronExpressionTests
	{
		[Theory]
		[InlineData("* * * *")]
		[InlineData("* * * * * *")]
		[InlineData("60 * * * *")]
		[InlineData("* 24 * * *")]
		[InlineData("* * 0 * *")]
		[InlineData("* * 32 * *")]
		[InlineData("* * * 13 *")]
		[InlineData("* * * * 8")]
		[InlineData("*/0 * * * *")]
		[InlineData("5-1 * * * *")]
		[InlineData("MON * * * *")]
		public void Parse_RejectsInvalidExpressions(string expression)
			=> Assert.Throws<FormatException>(() => CronExpression.Parse(expression));

		[Fact]
		public void Parse_AcceptsNamesAndRanges()
		{
			var expression = CronExpression.Parse("0 9 * JAN-MAR MON-FRI");
			var next = expression.NextOccurrence(new DateTime(2026, 11, 23, 21, 17, 0));

			// November is outside JAN-MAR, so the next match is in January.
			Assert.Equal(1, next.Month);
			Assert.Equal(9, next.Hour);
			Assert.Equal(0, next.Minute);
			Assert.True(next.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday));
		}

		[Fact]
		public void NextOccurrence_SkipsToTheNextMatchingMinute()
		{
			var expression = CronExpression.Parse("*/15 * * * *");
			var next = expression.NextOccurrence(new DateTime(2026, 11, 23, 21, 17, 30));

			Assert.Equal(30, next.Minute);
			Assert.Equal(21, next.Hour);
			Assert.Equal(0, next.Second);
		}

		[Fact]
		public void NextOccurrence_IsStrictlyAfterTheGivenMoment()
		{
			var expression = CronExpression.Parse("0 9 * * 1-5");
			var moment = new DateTime(2026, 11, 23, 9, 0, 0);
			var next = expression.NextOccurrence(moment);

			Assert.True(next > moment);
			Assert.Equal(9, next.Hour);
			Assert.True(next.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday));
			Assert.True(next < moment.AddDays(8));
		}

		[Fact]
		public void NextOccurrence_ReachesTheNextLeapDay()
		{
			var expression = CronExpression.Parse("0 0 29 2 *");
			var next = expression.NextOccurrence(new DateTime(2026, 11, 23, 21, 17, 0));

			Assert.Equal(2, next.Month);
			Assert.Equal(29, next.Day);
			Assert.Equal(0, next.Hour);
		}

		[Theory]
		[InlineData(0)]
		[InlineData(7)]
		public void NextOccurrence_TreatsSevenAndZeroAsSunday(int field)
		{
			var expression = CronExpression.Parse($"0 0 * * {field}");
			var next = expression.NextOccurrence(new DateTime(2026, 11, 23, 21, 17, 0));

			Assert.Equal(DayOfWeek.Sunday, next.DayOfWeek);
			Assert.Equal(0, next.Hour);
		}

		[Fact]
		public void NextOccurrence_OrsTwoRestrictedDayFields()
		{
			var expression = CronExpression.Parse("0 0 1 * 1");
			var next = expression.NextOccurrence(new DateTime(2026, 11, 23, 21, 17, 0));

			Assert.True(next.Day == 1 || next.DayOfWeek == DayOfWeek.Monday);
		}

		[Fact]
		public void CountOccurrences_CountsTheMinutesInsideTheInterval()
		{
			var expression = CronExpression.Parse("* * * * *");
			var start = new DateTime(2026, 11, 23, 21, 0, 0);

			Assert.Equal(10, expression.CountOccurrences(start, start.AddMinutes(10)));
			Assert.Equal(0, expression.CountOccurrences(start, start));
		}

		[Fact]
		public void CountOccurrences_CountsTheWeekdaysInTheInterval()
		{
			var expression = CronExpression.Parse("0 9 * * 1-5");
			var start = new DateTime(2026, 11, 23, 21, 17, 0);

			// A whole week holds five weekdays, and the bound is inclusive.
			Assert.Equal(5, expression.CountOccurrences(start, start.AddDays(7)));
		}
	}
}
