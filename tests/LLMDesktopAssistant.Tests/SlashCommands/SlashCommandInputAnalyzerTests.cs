using LLMDesktopAssistant.InputCompletion;
using LLMDesktopAssistant.SlashCommands;
using LLMDesktopAssistant.SlashCommands.Input;

namespace LLMDesktopAssistant.Tests.SlashCommands
{
	/// <summary>
	/// The pure input analyzer: command detection, token/argument spans, resolution state and the caret flags.
	/// </summary>
	public class SlashCommandInputAnalyzerTests
	{
		private static SlashCommandInfo Command(string name, params string[] namespaces) =>
			new() { Name = name, Namespaces = [.. namespaces] };

		private static readonly SlashCommandInfo Grilling = Command("grilling", "skill");
		private static readonly SlashCommandInfo[] Commands = [Grilling];

		[Fact]
		public void Analyze_PlainText_IsNotACommand()
		{
			var analysis = SlashCommandInputAnalyzer.Analyze("hello there", 5, Commands);

			Assert.False(analysis.IsCommand);
			Assert.Equal(SlashCommandInputResolutionState.None, analysis.ResolutionState);
			Assert.False(analysis.IsCaretInToken);
			Assert.False(analysis.IsCaretInArguments);
		}

		[Theory]
		[InlineData("")]
		[InlineData("   ")]
		[InlineData("\n\t")]
		public void Analyze_EmptyOrWhitespace_IsNotACommand(string text)
		{
			var analysis = SlashCommandInputAnalyzer.Analyze(text, 0, Commands);

			Assert.False(analysis.IsCommand);
			Assert.Equal(SlashCommandInputResolutionState.None, analysis.ResolutionState);
		}

		[Fact]
		public void Analyze_TheEscapedSlash_IsNotACommand()
		{
			var analysis = SlashCommandInputAnalyzer.Analyze("//grilling", 3, Commands);

			Assert.False(analysis.IsCommand);
			Assert.Equal(SlashCommandInputResolutionState.None, analysis.ResolutionState);
		}

		[Fact]
		public void Analyze_AKnownToken_Resolves_WithItsSpans()
		{
			var analysis = SlashCommandInputAnalyzer.Analyze("/grilling", 9, Commands);

			Assert.True(analysis.IsCommand);
			Assert.Equal("grilling", analysis.Token);
			Assert.Equal(new InputCompletionSpan(0, 9), analysis.TokenSpan);
			Assert.Equal(new InputCompletionSpan(9, 0), analysis.ArgumentSpan);
			Assert.Equal(SlashCommandInputResolutionState.Known, analysis.ResolutionState);
		}

		[Fact]
		public void Analyze_AKnownTokenWithArguments_MarksTheCaretInArguments()
		{
			var analysis = SlashCommandInputAnalyzer.Analyze("/grilling do it", 15, Commands);

			Assert.Equal("grilling", analysis.Token);
			Assert.Equal(new InputCompletionSpan(0, 9), analysis.TokenSpan);
			Assert.Equal(new InputCompletionSpan(10, 5), analysis.ArgumentSpan);
			Assert.True(analysis.IsCaretInArguments);
			Assert.False(analysis.IsCaretInToken);
			Assert.Equal(SlashCommandInputResolutionState.Known, analysis.ResolutionState);
		}

		[Fact]
		public void Analyze_ACaretInTheMiddleOfTheToken_StaysInTheToken()
		{
			// ResolutionState is caret-independent; the caret only drives the flags.
			var analysis = SlashCommandInputAnalyzer.Analyze("/grilling", 3, Commands);

			Assert.True(analysis.IsCaretInToken);
			Assert.False(analysis.IsCaretInArguments);
			Assert.Equal(SlashCommandInputResolutionState.Known, analysis.ResolutionState);
		}

		[Fact]
		public void Analyze_AnUnknownToken_IsUnknown()
		{
			var analysis = SlashCommandInputAnalyzer.Analyze("/zzz", 4, Commands);

			Assert.Equal("zzz", analysis.Token);
			Assert.Equal(SlashCommandInputResolutionState.Unknown, analysis.ResolutionState);
		}

		[Fact]
		public void Analyze_APartialToken_IsPartial()
		{
			var analysis = SlashCommandInputAnalyzer.Analyze("/gr", 3, Commands);

			Assert.Equal(SlashCommandInputResolutionState.Partial, analysis.ResolutionState);
		}

		[Fact]
		public void Analyze_ABareSlash_IsPartial_WithAnEmptyArgumentSpan()
		{
			var analysis = SlashCommandInputAnalyzer.Analyze("/", 1, Commands);

			Assert.True(analysis.IsCommand);
			Assert.Equal(string.Empty, analysis.Token);
			Assert.Equal(new InputCompletionSpan(0, 1), analysis.TokenSpan);
			Assert.Equal(new InputCompletionSpan(1, 0), analysis.ArgumentSpan);
			Assert.Equal(SlashCommandInputResolutionState.Partial, analysis.ResolutionState);
		}

		[Fact]
		public void Analyze_WhenABareTokenWinsOverOthers_IsWonOthers()
		{
			SlashCommandInfo[] commands = [Command("grilling", "skill"), Command("grilling", "agent")];

			var analysis = SlashCommandInputAnalyzer.Analyze("/grilling", 9, commands);

			Assert.Equal(SlashCommandInputResolutionState.WonOthers, analysis.ResolutionState);
		}

		[Fact]
		public void Analyze_AQualifiedToken_DisambiguatesToKnown()
		{
			SlashCommandInfo[] commands = [Command("grilling", "skill"), Command("grilling", "agent")];

			var analysis = SlashCommandInputAnalyzer.Analyze("/skill:grilling", 15, commands);

			Assert.Equal(SlashCommandInputResolutionState.Known, analysis.ResolutionState);
		}

		[Fact]
		public void Analyze_MultilineInput_OnlyTheLeadingTokenIsACommand()
		{
			const string text = "/grilling\nsecond /nope";
			var analysis = SlashCommandInputAnalyzer.Analyze(text, text.Length, Commands);

			Assert.True(analysis.IsCommand);
			Assert.Equal("grilling", analysis.Token);
			Assert.Equal(new InputCompletionSpan(0, 9), analysis.TokenSpan);
			Assert.Equal(SlashCommandInputResolutionState.Known, analysis.ResolutionState);
		}

		[Fact]
		public void Analyze_LeadingWhitespace_ShiftsTheTokenSpan()
		{
			var analysis = SlashCommandInputAnalyzer.Analyze("  /grilling", 11, Commands);

			Assert.True(analysis.IsCommand);
			Assert.Equal(new InputCompletionSpan(2, 9), analysis.TokenSpan);
			Assert.Equal(SlashCommandInputResolutionState.Known, analysis.ResolutionState);
		}
	}
}
