using System.Collections.Immutable;
using System.Globalization;
using System.Text;

namespace LLMDesktopAssistant.Utils.Dice
{
	/// <summary>
	/// The outcome of rolling one expression once: the pool of every group, the flat modifier and the total.
	/// </summary>
	public sealed class DiceRollResult
	{
		/// <summary>
		/// The expression this result belongs to.
		/// </summary>
		public required DiceExpression Expression { get; init; }

		/// <summary>
		/// The rolled groups, in source order.
		/// </summary>
		public required ImmutableList<DiceGroupRoll> Groups { get; init; }

		/// <summary>
		/// The signed sum of the expression's constant terms.
		/// </summary>
		public int Modifier { get; init; }

		/// <summary>
		/// The final total of the roll.
		/// </summary>
		public int Total { get; init; }

		/// <summary>
		/// Whether an explosion or reroll chain was cut by <see cref="DiceLimits.MaxDicePerPool"/>.
		/// </summary>
		public bool LimitReached { get; init; }

		/// <summary>
		/// The values that survived the keep/drop selection, across all groups.
		/// </summary>
		public IEnumerable<int> KeptValues => Groups.SelectMany(group => group.Dice).Where(die => die.Kept).Select(die => die.Value);

		/// <summary>
		/// Renders the roll as one line, for example
		/// <c>2d20kh1+5 = 21 (d20: 16, 11(dropped); mod +5)</c>.
		/// </summary>
		/// <returns>The one-line view of the roll.</returns>
		public string ToText()
		{
			var builder = new StringBuilder();
			builder.Append(Expression.ToNotation())
				.Append(" = ")
				.Append(Total.ToString(CultureInfo.InvariantCulture));

			var parts = new List<string>();
			foreach (var group in Groups)
				parts.Add($"{group.Group.ToLabel()}: {group.DescribeDice()}");

			// A constant-only expression already shows the value on the left, repeating it as a modifier is noise.
			if (Modifier != 0 && Groups.Count > 0)
				parts.Add("mod " + (Modifier > 0 ? "+" : string.Empty) + Modifier.ToString(CultureInfo.InvariantCulture));

			if (parts.Count > 0)
				builder.Append(" (").Append(string.Join("; ", parts)).Append(')');

			if (LimitReached)
				builder.Append(" [dice limit reached]");

			return builder.ToString();
		}
	}

	/// <summary>
	/// The rolled pool of one dice group and the part of the total it contributes.
	/// </summary>
	public sealed class DiceGroupRoll
	{
		/// <summary>
		/// The group that produced this pool.
		/// </summary>
		public required DiceGroup Group { get; init; }

		/// <summary>
		/// Every die of the pool, in roll order, including the ones a keep/drop modifier removed.
		/// </summary>
		public required ImmutableList<DiceDie> Dice { get; init; }

		/// <summary>
		/// The sum of the kept dice, explosion extras included.
		/// </summary>
		public int Value { get; init; }

		/// <summary>
		/// Renders the pool, marking rerolls, explosions and dropped dice. A pool past
		/// <see cref="DiceLimits.MaxListedDice"/> is cut: the total stays exact, the tail is only counted.
		/// </summary>
		/// <returns>The comma-separated dice of this group.</returns>
		public string DescribeDice()
		{
			var listed = Dice.Take(DiceLimits.MaxListedDice).Select(DescribeDie).ToList();

			if (Dice.Count > DiceLimits.MaxListedDice)
				listed.Add($"+{Dice.Count - DiceLimits.MaxListedDice} more");

			return string.Join(", ", listed);
		}

		private static string DescribeDie(DiceDie die)
		{
			var builder = new StringBuilder();

			if (die.RerolledFrom.Count > 0)
				builder.Append(string.Join("→", die.RerolledFrom)).Append('→');

			builder.Append(die.Value.ToString(CultureInfo.InvariantCulture));

			if (die.Explosions.Count > 0)
			{
				builder.Append('!').Append('(')
					.Append(string.Join(", ", die.Explosions.Select(value => value.ToString(CultureInfo.InvariantCulture))))
					.Append(')');
			}

			if (!die.Kept)
				builder.Append("(dropped)");

			return builder.ToString();
		}
	}

	/// <summary>
	/// One die of a pool: its final face, the faces it exploded into and whether it survived selection.
	/// </summary>
	public sealed class DiceDie
	{
		/// <summary>
		/// The face the die finally showed, after any rerolls.
		/// </summary>
		public int Value { get; init; }

		/// <summary>
		/// The faces this die went through before it settled, oldest first; empty when it was not rerolled.
		/// </summary>
		public ImmutableList<int> RerolledFrom { get; init; } = [];

		/// <summary>
		/// The extra faces the die exploded into, in roll order.
		/// </summary>
		public ImmutableList<int> Explosions { get; init; } = [];

		/// <summary>
		/// Whether the die still counts towards the total after the keep/drop selection.
		/// </summary>
		public bool Kept { get; set; } = true;

		/// <summary>
		/// The contribution of this die to the group value, its explosions included.
		/// </summary>
		public int Total => Value + Explosions.Sum();
	}
}
