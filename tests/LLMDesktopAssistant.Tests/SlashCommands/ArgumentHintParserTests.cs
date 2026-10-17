using LLMDesktopAssistant.SlashCommands.Arguments;

namespace LLMDesktopAssistant.Tests.SlashCommands
{
	/// <summary>
	/// The <c>argument-hint</c> parser: the bracket grammar (required/optional, rest, choices, description and
	/// escaping) and the fallbacks (the free-form rest slot and the invalid-hint report).
	/// </summary>
	public class ArgumentHintParserTests
	{
		[Theory]
		[InlineData(null)]
		[InlineData("")]
		[InlineData("   ")]
		public void AnEmptyHint_YieldsTheDefaultRestSlot(string? hint)
		{
			var schema = ArgumentHintParser.Build(hint, out var invalid);

			Assert.False(invalid);
			Assert.Empty(schema.Positionals);

			var rest = Assert.IsType<SlashCommandArgument>(schema.RestPositional);
			Assert.Equal("command.argument.arguments", rest.Name.Key);
			Assert.Equal("command.argument.arguments.description", rest.Description!.Key);
			Assert.False(rest.Required);
			Assert.Null(rest.Format);
		}

		[Fact]
		public void AFreeFormHint_YieldsARestSlotCarryingTheRawHint()
		{
			var schema = ArgumentHintParser.Build("commit-name, files", out var invalid);

			Assert.False(invalid);
			Assert.Empty(schema.Positionals);
			Assert.Equal("commit-name, files", schema.RestPositional!.Description!.Key);
			Assert.Equal("command.argument.arguments", schema.RestPositional.Name.Key);
		}

		[Fact]
		public void ARequiredArgument_IsARequiredPositional()
		{
			var schema = ArgumentHintParser.Build("<pr-number>", out var invalid);

			Assert.False(invalid);
			Assert.Null(schema.RestPositional);

			var positional = Assert.Single(schema.Positionals);
			Assert.Equal("pr-number", positional.Name.Key);
			Assert.True(positional.Required);
			Assert.Null(positional.Description);
			Assert.Null(positional.Format);
		}

		[Fact]
		public void AnOptionalArgument_IsAnOptionalPositional()
		{
			var schema = ArgumentHintParser.Build("[issue-number]", out var invalid);

			Assert.False(invalid);
			var positional = Assert.Single(schema.Positionals);
			Assert.Equal("issue-number", positional.Name.Key);
			Assert.False(positional.Required);
		}

		[Theory]
		[InlineData("[a] [b]", 2)]
		[InlineData("[a][b]", 2)]
		[InlineData("  [a]   [b]  ", 2)]
		[InlineData("<a>\t[b]\n<c>", 3)]
		public void MultipleArguments_ParseInOrder(string hint, int count)
		{
			var schema = ArgumentHintParser.Build(hint, out var invalid);

			Assert.False(invalid);
			Assert.Equal(count, schema.Positionals.Count);
			Assert.All(schema.Positionals, positional => Assert.Null(positional.Format));
		}

		[Fact]
		public void ChoicesAndDescription_AreParsed()
		{
			var schema = ArgumentHintParser.Build("[mode:$low|medium|ultra:%Mode for something]", out var invalid);

			Assert.False(invalid);
			var positional = Assert.Single(schema.Positionals);
			Assert.Equal("mode", positional.Name.Key);
			Assert.Equal("Mode for something", positional.Description!.Key);

			var format = Assert.IsType<ChoiceArgumentFormatProvider>(positional.Format);
			Assert.Equal(["low", "medium", "ultra"], format.Choices);
			Assert.True(format.TryValidate("medium", out _));
			Assert.False(format.TryValidate("nope", out _));
		}

		[Fact]
		public void DescriptionWithoutChoices_IsParsed()
		{
			var schema = ArgumentHintParser.Build("[name:%Just a description]", out var invalid);

			Assert.False(invalid);
			var positional = Assert.Single(schema.Positionals);
			Assert.Equal("Just a description", positional.Description!.Key);
			Assert.Null(positional.Format);
		}

		[Fact]
		public void AnEllipsis_MakesTheArgumentTheRestSlot()
		{
			var schema = ArgumentHintParser.Build("[files...]", out var invalid);

			Assert.False(invalid);
			Assert.Empty(schema.Positionals);

			var rest = Assert.IsType<SlashCommandArgument>(schema.RestPositional);
			Assert.Equal("files", rest.Name.Key);
			Assert.False(rest.Required);
		}

		[Fact]
		public void ARestSlot_CombinesWithPositionalsAndChoices()
		{
			var schema = ArgumentHintParser.Build("[commit-name] [files...:$a|b:%The files]", out var invalid);

			Assert.False(invalid);
			Assert.Equal("commit-name", Assert.Single(schema.Positionals).Name.Key);

			var rest = Assert.IsType<SlashCommandArgument>(schema.RestPositional);
			Assert.Equal("files", rest.Name.Key);
			Assert.Equal("The files", rest.Description!.Key);
			Assert.IsType<ChoiceArgumentFormatProvider>(rest.Format);
		}

		[Fact]
		public void AnEscapedBracket_StaysInsideTheDescription()
		{
			var schema = ArgumentHintParser.Build(@"[name:%Has \] a bracket]", out var invalid);

			Assert.False(invalid);
			Assert.Equal("Has ] a bracket", Assert.Single(schema.Positionals).Description!.Key);
		}

		[Fact]
		public void AnEscapedPipe_StaysInsideAChoice()
		{
			var schema = ArgumentHintParser.Build(@"[x:$a\|b|c]", out var invalid);

			Assert.False(invalid);
			var format = Assert.IsType<ChoiceArgumentFormatProvider>(Assert.Single(schema.Positionals).Format);
			Assert.Equal(["a|b", "c"], format.Choices);
		}

		[Theory]
		[InlineData("<a...>")] // a required rest positional is not a thing
		[InlineData("[a...] [b...]")] // only one rest slot
		[InlineData("[a] [b")] // an unclosed bracket
		[InlineData("[a] junk")] // trailing garbage
		[InlineData("[]")] // no name
		[InlineData("<a]")] // mismatched brackets
		public void AnUnparseableHint_FallsBackAndReportsInvalid(string hint)
		{
			var schema = ArgumentHintParser.Build(hint, out var invalid);

			Assert.True(invalid);
			Assert.Empty(schema.Positionals);

			var rest = Assert.IsType<SlashCommandArgument>(schema.RestPositional);
			Assert.Equal("command.argument.arguments", rest.Name.Key);
			Assert.Equal(hint, rest.Description!.Key);
		}
	}
}
