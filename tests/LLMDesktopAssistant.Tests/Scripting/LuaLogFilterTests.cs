using System.Collections.Immutable;
using AsyncLua;
using AsyncLua.Values;
using LLMDesktopAssistant.Scripting.Lua.Logs;
using LLMDesktopAssistant.Utils;
using Serilog.Events;

namespace LLMDesktopAssistant.Tests.Scripting
{
	public class LuaLogFilterTests
	{
		private static RenderedLogEvent Entry(LogEventLevel level, string rendered,
			string template = "", long sequence = 10)
			=> new(sequence, DateTimeOffset.UnixEpoch, level, null, template,
				ImmutableDictionary<string, string>.Empty, rendered);

		private static LuaTable Table(params (string Key, LuaValue Value)[] items)
		{
			var table = new LuaTable();
			foreach (var (key, value) in items)
				table[key] = value;
			return table;
		}

		[Fact]
		public void Parse_WithoutFilter_PassesEverything()
		{
			var filter = LuaLogFilter.Parse(null, "test");

			Assert.True(filter.Matches(Entry(LogEventLevel.Verbose, "x", sequence: 1)));
			Assert.Equal(int.MaxValue, filter.Last);
		}

		[Fact]
		public void MinLevel_DropsLowerLevels()
		{
			var filter = LuaLogFilter.Parse(Table(("min_level", new LuaString("warning"))), "test");

			Assert.False(filter.Matches(Entry(LogEventLevel.Information, "x")));
			Assert.True(filter.Matches(Entry(LogEventLevel.Warning, "x")));
			Assert.True(filter.Matches(Entry(LogEventLevel.Error, "x")));
		}

		[Fact]
		public void Levels_KeepsOnlyTheListedOnes()
		{
			var levels = new LuaTable();
			levels.Append(new LuaString("error"));
			levels.Append(new LuaString("fatal"));

			var filter = LuaLogFilter.Parse(Table(("levels", levels)), "test");

			Assert.False(filter.Matches(Entry(LogEventLevel.Warning, "x")));
			Assert.True(filter.Matches(Entry(LogEventLevel.Error, "x")));
			Assert.True(filter.Matches(Entry(LogEventLevel.Fatal, "x")));
		}

		[Fact]
		public void Text_MatchesTheRenderedMessageAndTheTemplate()
		{
			var filter = LuaLogFilter.Parse(Table(("text", new LuaString("token"))), "test");

			Assert.True(filter.Matches(Entry(LogEventLevel.Information, "a token here")));
			Assert.True(filter.Matches(Entry(LogEventLevel.Information, "", "template with token")));
			Assert.False(filter.Matches(Entry(LogEventLevel.Information, "nothing")));
		}

		[Fact]
		public void Text_IgnoresCase()
		{
			var filter = LuaLogFilter.Parse(Table(("text", new LuaString("Token"))), "test");

			Assert.True(filter.Matches(Entry(LogEventLevel.Information, "token")));
			Assert.True(filter.Matches(Entry(LogEventLevel.Information, "TOKEN")));
			Assert.False(filter.Matches(Entry(LogEventLevel.Information, "to keen")));
		}

		[Fact]
		public void Since_SkipsTheSequenceItself()
		{
			var filter = LuaLogFilter.Parse(Table(("since", new LuaNumber(10))), "test");

			Assert.False(filter.Matches(Entry(LogEventLevel.Information, "x", sequence: 10)));
			Assert.True(filter.Matches(Entry(LogEventLevel.Information, "x", sequence: 11)));
		}

		[Fact]
		public void Last_IsParsedAndRejectsNegatives()
		{
			Assert.Equal(50, LuaLogFilter.Parse(Table(("last", new LuaNumber(50))), "test").Last);
			Assert.Equal(0, LuaLogFilter.Parse(Table(("last", new LuaNumber(-5))), "test").Last);
		}

		[Fact]
		public void Parse_UnknownLevel_ThrowsListingTheKnownOnes()
		{
			var exception = Assert.Throws<LuaRuntimeException>(() =>
				LuaLogFilter.Parse(Table(("min_level", new LuaString("loud"))), "test"));

			Assert.Contains("loud", exception.Message);
			Assert.Contains("warning", exception.Message);
		}

		[Theory]
		[InlineData("verbose", LogEventLevel.Verbose)]
		[InlineData("debug", LogEventLevel.Debug)]
		[InlineData("info", LogEventLevel.Information)]
		[InlineData("information", LogEventLevel.Information)]
		[InlineData("warning", LogEventLevel.Warning)]
		[InlineData("warn", LogEventLevel.Warning)]
		[InlineData("error", LogEventLevel.Error)]
		[InlineData("fatal", LogEventLevel.Fatal)]
		public void LevelNames_ResolveToTheirLevel(string name, LogEventLevel expected)
		{
			Assert.True(LuaLogLevels.TryParse(name, out var level));
			Assert.Equal(expected, level);
			Assert.Equal(name.StartsWith("warn") ? "warning" : name == "information" ? "info" : name,
				LuaLogLevels.Name(level));
		}

		[Fact]
		public void Entry_IsRenderedAsTheDocumentedTable()
		{
			var entry = new RenderedLogEvent(42, DateTimeOffset.UnixEpoch.AddSeconds(1.5), LogEventLevel.Warning,
				new InvalidOperationException("boom"), "User {Name}",
				ImmutableDictionary<string, string>.Empty.Add("Name", "John"), "User John");

			var table = entry.ToLua();

			Assert.Equal(42, Assert.IsType<LuaNumber>(table.Get("seq")).Value);
			Assert.Equal(1.5, Assert.IsType<LuaNumber>(table.Get("timestamp")).Value);
			Assert.Equal("warning", Assert.IsType<LuaString>(table.Get("level")).Value);
			Assert.Equal("User John", Assert.IsType<LuaString>(table.Get("message")).Value);
			Assert.Equal("User {Name}", Assert.IsType<LuaString>(table.Get("template")).Value);
			Assert.Equal("John", Assert.IsType<LuaString>(Assert.IsType<LuaTable>(table.Get("properties")).Get("Name")).Value);
			Assert.Contains("boom", Assert.IsType<LuaString>(table.Get("exception")).Value);
		}

		[Fact]
		public void Entry_WithoutException_ReportsNil()
		{
			var table = Entry(LogEventLevel.Information, "x").ToLua();

			Assert.IsType<LuaNil>(table.Get("exception"));
		}
	}
}
