using LLMDesktopAssistant.Utils.Dice;

namespace LLMDesktopAssistant.Tests.Utils.Dice
{
	/// <summary>
	/// Covers the dice notation parser and the roller that consumes it: the forms players write,
	/// the limits that keep a typo from becoming a long loop, and the selection semantics.
	/// </summary>
	public class DiceExpressionTests
	{
		[Theory]
		[InlineData("2d6", 2, 6)]
		[InlineData("d20", 1, 20)]
		[InlineData("D20", 1, 20)]
		[InlineData(" 1d8 ", 1, 8)]
		[InlineData("d%", 1, 100)]
		[InlineData("10d6", 10, 6)]
		public void Parse_ReadsTheDiceGroup(string input, int count, int sides)
		{
			var expression = DiceExpressionParser.Parse(input);

			var group = Assert.Single(expression.Groups);
			Assert.Equal(count, group.Count);
			Assert.Equal(sides, group.Sides);
		}

		[Fact]
		public void Parse_KeepsTheSignsAndOrderOfTheTerms()
		{
			var expression = DiceExpressionParser.Parse("1d8+2d6-3");

			Assert.Equal(3, expression.Terms.Count);
			Assert.Equal([1, 1, -1], expression.Terms.Select(term => term.Sign));
			Assert.Equal(-3, expression.Modifier);
			Assert.Equal("1d8+2d6-3", expression.ToNotation());
		}

		[Fact]
		public void ToNotation_NormalizesCaseAndWhitespace()
		{
			var expression = DiceExpressionParser.Parse(" 2D6  +  3 ");

			Assert.Equal("2d6+3", expression.ToNotation());
		}

		[Theory]
		[InlineData("4d6kh3", DiceSelection.KeepHighest, 3)]
		[InlineData("4d6kh", DiceSelection.KeepHighest, 1)]
		[InlineData("4d6kl2", DiceSelection.KeepLowest, 2)]
		[InlineData("4d6dh2", DiceSelection.DropHighest, 2)]
		[InlineData("4d6dl1", DiceSelection.DropLowest, 1)]
		public void Parse_ReadsKeepAndDropModifiers(string input, DiceSelection selection, int count)
		{
			var expression = DiceExpressionParser.Parse(input);

			var modifier = Assert.IsType<DiceSelectionModifier>(Assert.Single(expression.Groups.Single().Modifiers));
			Assert.Equal(selection, modifier.Selection);
			Assert.Equal(count, modifier.Count);
		}

		[Fact]
		public void Parse_ReadsExplodingAndRerollModifiers()
		{
			var exploding = Assert.IsType<DiceExplodeModifier>(Assert.Single(DiceExpressionParser.Parse("3d6!").Groups.Single().Modifiers));
			Assert.Null(exploding.Threshold);

			var threshold = Assert.IsType<DiceExplodeModifier>(Assert.Single(DiceExpressionParser.Parse("2d10!9").Groups.Single().Modifiers));
			Assert.Equal(9, threshold.Threshold);

			var reroll = Assert.IsType<DiceRerollModifier>(Assert.Single(DiceExpressionParser.Parse("4d6r1").Groups.Single().Modifiers));
			Assert.Equal(1, reroll.Value);
			Assert.False(reroll.Repeats);

			var repeated = Assert.IsType<DiceRerollModifier>(Assert.Single(DiceExpressionParser.Parse("4d6rr2").Groups.Single().Modifiers));
			Assert.Equal(2, repeated.Value);
			Assert.True(repeated.Repeats);
		}

		[Fact]
		public void Parse_ReadsSeveralModifiersInWrittenOrder()
		{
			var group = DiceExpressionParser.Parse("4d6kh3!").Groups.Single();

			Assert.Collection(group.Modifiers,
				modifier => Assert.IsType<DiceSelectionModifier>(modifier),
				modifier => Assert.IsType<DiceExplodeModifier>(modifier));
		}

		[Theory]
		[InlineData("")]
		[InlineData("   ")]
		[InlineData("abc")]
		[InlineData("2d")]
		[InlineData("d")]
		[InlineData("2d6+")]
		[InlineData("2*3")]
		[InlineData("0d6")]
		[InlineData("2d0")]
		[InlineData("4d6kh5")]
		[InlineData("d6!1")]
		[InlineData("d6r6")]
		public void Parse_RejectsWhatCannotBeRolled(string input)
		{
			var exception = Assert.Throws<DiceExpressionException>(() => DiceExpressionParser.Parse(input));

			Assert.False(string.IsNullOrWhiteSpace(exception.Message));
		}

		[Fact]
		public void Roll_KeepsEveryDieInsideItsFaces()
		{
			var expression = DiceExpressionParser.Parse("25d6");
			var result = new DiceRoller(new Random(1234)).Roll(expression);

			var group = Assert.Single(result.Groups);
			Assert.All(group.Dice, die => Assert.InRange(die.Value, 1, 6));
			Assert.Equal(group.Dice.Sum(die => die.Value), result.Total);
		}

		[Fact]
		public void Roll_KeepHighestLeavesTheSelectedDiceInTheTotal()
		{
			var result = new DiceRoller(new Random(2024)).Roll(DiceExpressionParser.Parse("4d6kh3"));

			var group = Assert.Single(result.Groups);
			var kept = group.Dice.Where(die => die.Kept).ToList();
			var dropped = group.Dice.Where(die => !die.Kept).ToList();

			Assert.Equal(3, kept.Count);
			Assert.Single(dropped);
			Assert.True(kept.Min(die => die.Value) >= dropped[0].Value);
			Assert.Equal(kept.Sum(die => die.Value), result.Total);
		}

		[Fact]
		public void Roll_DropLowestRemovesTheLowestDie()
		{
			var result = new DiceRoller(new Random(31337)).Roll(DiceExpressionParser.Parse("4d6dl1"));

			var group = Assert.Single(result.Groups);
			var dropped = Assert.Single(group.Dice, die => !die.Kept);

			Assert.Equal(group.Dice.Min(die => die.Value), dropped.Value);
			Assert.Equal(3, group.Dice.Count(die => die.Kept));
		}

		[Fact]
		public void Roll_AppliesTheFlatModifierToTheTotal()
		{
			var result = new DiceRoller(new Random(7)).Roll(DiceExpressionParser.Parse("2d20kh1+5"));

			var group = Assert.Single(result.Groups);
			Assert.Equal(5, result.Modifier);
			Assert.Equal(group.Value + 5, result.Total);
			Assert.Single(group.Dice, die => die.Kept);
		}

		[Fact]
		public void Roll_ExplodesOnTheMaximumFace()
		{
			var expression = DiceExpressionParser.Parse("100d6!");
			var result = new DiceRoller(new Random(99)).Roll(expression);

			var exploders = Assert.Single(result.Groups).Dice.Where(die => die.Explosions.Count > 0).ToList();

			Assert.NotEmpty(exploders);
			Assert.All(exploders, die =>
			{
				Assert.Equal(6, die.Value);
				Assert.All(die.Explosions, extra => Assert.InRange(extra, 1, 6));
			});
			Assert.Equal(Assert.Single(result.Groups).Dice.Sum(die => die.Value + die.Explosions.Sum()), result.Total);
		}

		[Fact]
		public void Roll_RerollsUntilTheFaceChanges()
		{
			var result = new DiceRoller(new Random(5)).Roll(DiceExpressionParser.Parse("50d2rr1"));

			var group = Assert.Single(result.Groups);

			Assert.All(group.Dice, die => Assert.Equal(2, die.Value));
			Assert.All(group.Dice, die =>
			{
				if (die.RerolledFrom.Count == 0)
					return;

				Assert.All(die.RerolledFrom, face => Assert.Equal(1, face));
			});
			Assert.Contains(group.Dice, die => die.RerolledFrom.Count > 0);
		}

		[Fact]
		public void Roll_StopsExplodingAtThePoolLimit()
		{
			var expression = DiceExpressionParser.Parse("1000d2!");
			var result = new DiceRoller(new Random(11)).Roll(expression);

			Assert.True(result.LimitReached);
			Assert.Contains("dice limit reached", result.ToText());
		}

		[Fact]
		public void ToText_ShowsTheBreakdown()
		{
			var result = new DiceRoller(new Random(42)).Roll(DiceExpressionParser.Parse("2d20kh1+5"));
			var text = result.ToText();

			Assert.StartsWith("2d20kh1+5 = ", text);
			Assert.Contains("(2d20: ", text);
			Assert.Contains("(dropped)", text);
			Assert.Contains("mod +5", text);
		}

		[Fact]
		public void Roll_AcceptsAConstantOnlyExpression()
		{
			var result = new DiceRoller(new Random(1)).Roll(DiceExpressionParser.Parse("5"));

			Assert.Empty(result.Groups);
			Assert.Equal(5, result.Total);
			Assert.Equal("5 = 5", result.ToText());
		}
	}
}
