using System.Collections.Immutable;
using System.Globalization;
using RCParsing;
using RCParsing.TokenPatterns;

namespace LLMDesktopAssistant.Utils.Dice
{
	/// <summary>
	/// Parses standard dice notation into a <see cref="DiceExpression"/>. The grammar is deliberately
	/// the notation players already know, so the model can pass <c>2d20kh1+5</c> straight through
	/// instead of translating it into a parameter set.
	/// </summary>
	/// <remarks>
	/// Supported forms: <c>2d6</c>, <c>d20</c>, <c>d%</c>, <c>1d20+5</c>, <c>1d8+2d6+3</c>,
	/// <c>4d6kh3</c>, <c>2d20kh1</c>, <c>4d6dl1</c>, <c>3d6!</c>, <c>2d10!9</c>, <c>4d6r1</c>,
	/// <c>4d6rr1</c>. Whitespace and case are ignored.
	/// </remarks>
	public static class DiceExpressionParser
	{
		private const string syntaxHint =
			"1d20+5, 2d6, d%, 4d6kh3, 2d20kh1+3, 3d6!, 4d6r1";

		private static readonly Parser _parser;

		static DiceExpressionParser()
		{
			var builder = new ParserBuilder();

			builder.Settings.SkipWhitespaces();

			builder.CreateRule("integer")
				.Number<int>(NumberFlags.UnsignedInteger)
				.Transform(v => v.GetIntermediateValue<int>());

			// 'd%' is the percentile die, a d100 under an older name.
			builder.CreateRule("sides")
				.Choice(
					b => b.Rule("integer"),
					b => b.Literal('%').Transform(_ => 100));

			builder.CreateRule("keep")
				.LiteralChoiceIgnoreCase("kh", "kl", "dh", "dl")
				.Optional(b => b.Rule("integer"))

				.Transform(v =>
				{
					var selection = v[0].Text.ToLowerInvariant() switch
					{
						"kh" => DiceSelection.KeepHighest,
						"kl" => DiceSelection.KeepLowest,
						"dh" => DiceSelection.DropHighest,
						_ => DiceSelection.DropLowest
					};

					return new DiceSelectionModifier
					{
						Selection = selection,
						Count = v.TryGetNullableValue<int>(1) ?? 1
					};
				});

			// A bare '!' explodes on the maximum face, '!N' on every face of N or higher.
			builder.CreateRule("explode")
				.Literal('!')
				.Optional(b => b.Rule("integer"))

				.Transform(v => new DiceExplodeModifier
				{
					Threshold = v.TryGetNullableValue<int>(1)
				});

			// 'rr' has to be tried before 'r', otherwise the second r never gets a chance.
			builder.CreateRule("reroll")
				.Choice(
					b => b.LiteralIgnoreCase("rr"),
					b => b.LiteralIgnoreCase("r"))
				.Optional(b => b.Rule("integer"))

				.Transform(v => new DiceRerollModifier
				{
					Value = v.TryGetNullableValue<int>(1) ?? 1,
					Repeats = v[0].Text.Equals("rr", StringComparison.OrdinalIgnoreCase)
				});

			builder.CreateRule("modifier")
				.Choice(
					b => b.Rule("keep"),
					b => b.Rule("explode"),
					b => b.Rule("reroll"))

				.Transform(v => v.GetValue(0));

			builder.CreateRule("dice")
				.Optional(b => b.Rule("integer"))
				.LiteralIgnoreCase("d")
				.Rule("sides")
				.ZeroOrMore(b => b.Rule("modifier"))

				.Transform(v =>
				{
					var count = v.TryGetNullableValue<int>(0) ?? 1;
					var sides = v.GetValue<int>(2);
					var modifiers = v[3].Children
						.Select(child => child.GetValue<DiceModifier>())
						.ToImmutableList();

					ValidateGroup(count, sides, modifiers);

					return new DiceGroup
					{
						Count = count,
						Sides = sides,
						Modifiers = modifiers
					};
				});

			builder.CreateRule("term")
				.Choice(
					b => b.Rule("dice"),
					b => b.Rule("integer"))

				.Transform(v =>
				{
					DiceTerm term = v.GetValue(0) switch
					{
						DiceTerm dice => dice,
						int constant => new DiceConstant { Value = constant },
						var other => throw new DiceExpressionException(
							$"Unexpected term '{other}'.")
					};

					return ImmutableList.Create(term);
				});

			builder.CreateRule("expr")
				.OneOrMoreSeparated(
					b => b.Rule("term"),
					b => b.LiteralChoice("+", "-"),
					includeSeparatorsInResult: true)

				.TransformFoldLeft<ImmutableList<DiceTerm>, string, ImmutableList<DiceTerm>>(
					(terms, op, next) => terms.Add(next[0].WithSign(op == "-" ? -1 : 1)));

			builder.CreateMainRule("expression")
				.Rule("expr")
				.EOF()
				.TransformSelect(0);

			_parser = builder.Build();
		}

		/// <summary>
		/// Parses a dice expression.
		/// </summary>
		/// <param name="input">The expression text, for example <c>2d6+3</c>.</param>
		/// <returns>The parsed expression.</returns>
		/// <exception cref="DiceExpressionException">The text is not a valid dice expression.</exception>
		public static DiceExpression Parse(string input)
		{
			if (string.IsNullOrWhiteSpace(input))
				throw new DiceExpressionException("The dice expression is empty.");

			var text = input.Trim();

			ImmutableList<DiceTerm> terms;
			try
			{
				terms = _parser.Parse(text).GetValue<ImmutableList<DiceTerm>>();
			}
			catch (ParsingException exception)
			{
				var position = Math.Max(exception.LastPosition, 0) + 1;
				throw new DiceExpressionException(
					$"Invalid dice expression '{text}': cannot parse it at position {position}. " +
					$"Expected one of: {syntaxHint}.", exception);
			}

			if (terms.Count > DiceLimits.MaxTerms)
			{
				throw new DiceExpressionException(
					$"The dice expression '{text}' has {terms.Count} terms, " +
					$"at most {DiceLimits.MaxTerms} are supported.");
			}

			return new DiceExpression { Text = text, Terms = terms };
		}

		/// <summary>
		/// Checks the semantic limits of one group: a group of zero dice or zero faces is meaningless,
		/// and a keep/drop modifier that selects more dice than exist is a typo rather than a wish.
		/// </summary>
		/// <param name="count">The number of dice in the group.</param>
		/// <param name="sides">The number of faces per die.</param>
		/// <param name="modifiers">The modifiers written after the group.</param>
		private static void ValidateGroup(int count, int sides, ImmutableList<DiceModifier> modifiers)
		{
			if (count < 1 || count > DiceLimits.MaxDicePerGroup)
			{
				throw new DiceExpressionException(
					$"A dice group must roll between 1 and {DiceLimits.MaxDicePerGroup} dice, got {count}.");
			}

			if (sides < 1 || sides > DiceLimits.MaxSides)
			{
				throw new DiceExpressionException(
					$"A die must have between 1 and {DiceLimits.MaxSides} faces, got {sides}.");
			}

			foreach (var modifier in modifiers)
			{
				switch (modifier)
				{
					case DiceSelectionModifier selection
						when selection.Count < 1 || selection.Count > count:
						throw new DiceExpressionException(
							$"'{selection.ToNotation()}' selects {selection.Count} dice " +
							$"out of a group of {count}.");

					case DiceExplodeModifier explode
						when explode.Threshold is < 2 || explode.Threshold > sides:
						throw new DiceExpressionException(
							$"Explosion threshold must be between 2 and {sides}, " +
							$"got {explode.Threshold}.");

					case DiceRerollModifier reroll
						when reroll.Value < 1 || reroll.Value >= sides:
						throw new DiceExpressionException(
							$"Reroll face must be between 1 and {sides - 1}, got {reroll.Value}.");
				}
			}
		}
	}
}
