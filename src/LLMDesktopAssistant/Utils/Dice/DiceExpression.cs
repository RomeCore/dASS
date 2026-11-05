using System.Collections.Immutable;
using System.Globalization;
using System.Text;

namespace LLMDesktopAssistant.Utils.Dice
{
	/// <summary>
	/// A parsed dice expression: a list of signed terms summed together, in source order.
	/// </summary>
	public sealed class DiceExpression
	{
		/// <summary>
		/// The expression as the caller wrote it, trimmed. It is kept for error messages only; the
		/// normalized form the result is reported against comes from <see cref="ToNotation"/>.
		/// </summary>
		public required string Text { get; init; }

		/// <summary>
		/// The signed terms of the expression, in source order.
		/// </summary>
		public required ImmutableList<DiceTerm> Terms { get; init; }

		/// <summary>
		/// The dice groups of the expression, in source order.
		/// </summary>
		public IEnumerable<DiceGroup> Groups => Terms.OfType<DiceGroup>();

		/// <summary>
		/// The signed sum of the constant terms.
		/// </summary>
		public int Modifier => Terms.OfType<DiceConstant>().Sum(term => term.Sign * term.Value);

		/// <summary>
		/// Renders the expression back into normalized dice notation, the way the result line shows it.
		/// </summary>
		/// <returns>The normalized expression text, for example <c>2d6+3</c>.</returns>
		public string ToNotation()
		{
			var builder = new StringBuilder();

			for (var index = 0; index < Terms.Count; index++)
			{
				var term = Terms[index];
				if (index == 0)
				{
					if (term.Sign < 0)
						builder.Append('-');
				}
				else
				{
					builder.Append(term.Sign < 0 ? '-' : '+');
				}

				builder.Append(term.ToNotation());
			}

			return builder.ToString();
		}
	}

	/// <summary>
	/// One signed term of a dice expression: either a dice group or a flat constant.
	/// </summary>
	public abstract class DiceTerm
	{
		/// <summary>
		/// The sign the term contributes with: <c>1</c> for a plus, <c>-1</c> for a minus.
		/// </summary>
		public int Sign { get; init; } = 1;

		/// <summary>
		/// Returns a copy of the term carrying the given sign.
		/// </summary>
		/// <param name="sign">The sign to apply.</param>
		/// <returns>A copy of this term with the new sign.</returns>
		public abstract DiceTerm WithSign(int sign);

		/// <summary>
		/// Renders the term as dice notation, without its sign.
		/// </summary>
		/// <returns>The notation of this term, for example <c>2d6kh1</c>.</returns>
		public abstract string ToNotation();
	}

	/// <summary>
	/// A flat modifier applied to the total, like the <c>3</c> in <c>2d6+3</c>.
	/// </summary>
	public sealed class DiceConstant : DiceTerm
	{
		/// <summary>
		/// The value of the constant, always non-negative; the sign lives on the term.
		/// </summary>
		public required int Value { get; init; }

		/// <inheritdoc/>
		public override DiceTerm WithSign(int sign) => new DiceConstant { Value = Value, Sign = sign };

		/// <inheritdoc/>
		public override string ToNotation() => Value.ToString(CultureInfo.InvariantCulture);
	}

	/// <summary>
	/// One group of identical dice, like <c>4d6kh3</c>, together with the modifiers written after it.
	/// </summary>
	public sealed class DiceGroup : DiceTerm
	{
		/// <summary>
		/// The number of dice rolled for this group.
		/// </summary>
		public required int Count { get; init; }

		/// <summary>
		/// The number of faces per die.
		/// </summary>
		public required int Sides { get; init; }

		/// <summary>
		/// The modifiers applied after the dice are rolled, in the order they were written.
		/// </summary>
		public ImmutableList<DiceModifier> Modifiers { get; init; } = [];

		/// <inheritdoc/>
		public override DiceTerm WithSign(int sign) => new DiceGroup
		{
			Count = Count,
			Sides = Sides,
			Modifiers = Modifiers,
			Sign = sign
		};

		/// <inheritdoc/>
		public override string ToNotation() =>
			ToLabel() + string.Concat(Modifiers.Select(modifier => modifier.ToNotation()));

		/// <summary>
		/// Renders the dice of this group without the modifiers, the way the result breakdown labels it.
		/// </summary>
		/// <returns>The label, for example <c>4d6</c> or <c>1d20</c>.</returns>
		public string ToLabel() =>
			Count.ToString(CultureInfo.InvariantCulture) +
			'd' +
			Sides.ToString(CultureInfo.InvariantCulture);
	}

	/// <summary>
	/// Which dice a keep/drop modifier selects out of the group's pool.
	/// </summary>
	public enum DiceSelection
	{
		/// <summary>
		/// Selects the highest dice, they stay in the total.
		/// </summary>
		KeepHighest,

		/// <summary>
		/// Selects the lowest dice, they stay in the total.
		/// </summary>
		KeepLowest,

		/// <summary>
		/// Selects the highest dice, they leave the total.
		/// </summary>
		DropHighest,

		/// <summary>
		/// Selects the lowest dice, they leave the total.
		/// </summary>
		DropLowest
	}

	/// <summary>
	/// A modifier written after a dice group, applied after the dice are rolled.
	/// </summary>
	public abstract class DiceModifier
	{
		/// <summary>
		/// Renders the modifier back into dice notation.
		/// </summary>
		/// <returns>The modifier notation, for example <c>kh3</c>.</returns>
		public abstract string ToNotation();
	}

	/// <summary>
	/// Keeps or drops a number of highest or lowest dice before the group is summed.
	/// </summary>
	public sealed class DiceSelectionModifier : DiceModifier
	{
		/// <summary>
		/// Which dice the modifier selects.
		/// </summary>
		public required DiceSelection Selection { get; init; }

		/// <summary>
		/// How many dice the modifier selects.
		/// </summary>
		public int Count { get; init; } = 1;

		/// <inheritdoc/>
		public override string ToNotation()
		{
			var token = Selection switch
			{
				DiceSelection.KeepHighest => "kh",
				DiceSelection.KeepLowest => "kl",
				DiceSelection.DropHighest => "dh",
				_ => "dl"
			};

			return token + Count.ToString(CultureInfo.InvariantCulture);
		}
	}

	/// <summary>
	/// Makes a die roll again and add the extra faces to its own total.
	/// </summary>
	public sealed class DiceExplodeModifier : DiceModifier
	{
		/// <summary>
		/// The lowest face that explodes, or <see langword="null"/> when only the maximum face does.
		/// </summary>
		public int? Threshold { get; init; }

		/// <inheritdoc/>
		public override string ToNotation() =>
			Threshold is null ? "!" : "!" + Threshold.Value.ToString(CultureInfo.InvariantCulture);
	}

	/// <summary>
	/// Rerolls a die that showed the given face, either once or until it shows something else.
	/// </summary>
	public sealed class DiceRerollModifier : DiceModifier
	{
		/// <summary>
		/// The face that triggers the reroll.
		/// </summary>
		public required int Value { get; init; }

		/// <summary>
		/// Whether the die keeps rerolling while it shows that face, or only once.
		/// </summary>
		public bool Repeats { get; init; }

		/// <inheritdoc/>
		public override string ToNotation() =>
			(Repeats ? "rr" : "r") + Value.ToString(CultureInfo.InvariantCulture);
	}
}
