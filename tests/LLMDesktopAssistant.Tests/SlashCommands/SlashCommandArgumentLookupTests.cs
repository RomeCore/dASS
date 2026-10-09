using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using LLMDesktopAssistant.Localization;
using LLMDesktopAssistant.SlashCommands.Arguments;
using LLMDesktopAssistant.SlashCommands.Input;

namespace LLMDesktopAssistant.Tests.SlashCommands
{
	/// <summary>
	/// The pure argument lookup: which argument the caret sits in, its value span and the typed prefix.
	/// </summary>
	public class SlashCommandArgumentLookupTests
	{
		private static readonly SlashCommandArgument Positional = new() { Name = Locale.GetKey("test.p") };
		private static readonly SlashCommandArgument Keyed = new() { Name = Locale.GetKey("test.k") };

		private static SlashCommandArgumentSchema Schema() => new()
		{
			Positionals = [Positional],
			Keyed = new Dictionary<string, SlashCommandArgument> { ["key"] = Keyed }.ToImmutableDictionary()
		};

		// "hello key=value": the positional is [0,5), the keyed argument is [6,15) with its value at [10,15).
		private static SlashCommandArgumentTarget? Find(int caretOffset)
			=> SlashCommandArgumentLookup.Find(
				SlashCommandArgumentParser.Parse(Schema(), "hello key=value"), "hello key=value", caretOffset);

		[Fact]
		public void Find_InsideAPositional_ReturnsItsSlot_AndThePrefix()
		{
			var target = Find(2);

			Assert.NotNull(target);
			Assert.Same(Positional, target!.Value.Slot);
			Assert.Equal(0, target.Value.ValueStart);
			Assert.Equal(5, target.Value.ValueLength);
			Assert.Equal("he", target.Value.Prefix);
		}

		[Fact]
		public void Find_InsideAKeyedValue_ReturnsItsSlot_AndThePrefix()
		{
			var target = Find(15);

			Assert.NotNull(target);
			Assert.Same(Keyed, target!.Value.Slot);
			Assert.Equal(10, target.Value.ValueStart);
			Assert.Equal(5, target.Value.ValueLength);
			Assert.Equal("value", target.Value.Prefix);
		}

		[Fact]
		public void Find_OnAKeyedArgumentsKey_ReturnsNull()
		{
			// The caret is inside "key", before the '=' — v1 does not complete keys.
			Assert.Null(Find(7));
		}

		[Fact]
		public void Find_EmptyArgumentText_ReturnsNull()
		{
			var parsed = SlashCommandArgumentParser.Parse(Schema(), string.Empty);

			Assert.Null(SlashCommandArgumentLookup.Find(parsed, string.Empty, 0));
		}
	}
}
