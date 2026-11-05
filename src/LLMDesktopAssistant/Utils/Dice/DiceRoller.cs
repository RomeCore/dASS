using System.Collections.Immutable;

namespace LLMDesktopAssistant.Utils.Dice
{
	/// <summary>
	/// Rolls a parsed dice expression.
	/// </summary>
	/// <param name="random">The generator the dice are drawn from.</param>
	public sealed class DiceRoller(Random random)
	{
		/// <summary>
		/// Rolls the expression once.
		/// </summary>
		/// <param name="expression">The expression to roll.</param>
		/// <returns>Every group's pool, the flat modifier and the total.</returns>
		public DiceRollResult Roll(DiceExpression expression)
		{
			var groups = new List<DiceGroupRoll>();
			var limitReached = false;
			var total = 0;

			foreach (var group in expression.Groups)
			{
				var roll = RollGroup(group, ref limitReached);
				groups.Add(roll);
				total += group.Sign * roll.Value;
			}

			var modifier = expression.Modifier;

			return new DiceRollResult
			{
				Expression = expression,
				Groups = groups.ToImmutableList(),
				Modifier = modifier,
				Total = total + modifier,
				LimitReached = limitReached
			};
		}

		/// <summary>
		/// Rolls one group and applies its keep/drop modifiers.
		/// </summary>
		/// <param name="group">The group to roll.</param>
		/// <param name="limitReached">Set when an explosion chain ran into the pool limit.</param>
		/// <returns>The rolled pool and its value.</returns>
		private DiceGroupRoll RollGroup(DiceGroup group, ref bool limitReached)
		{
			// Explosions and rerolls are extra dice the same pool budget pays for; the group itself
			// has already been counted against it.
			var extrasLeft = DiceLimits.MaxDicePerPool - group.Count;
			var dice = new List<DiceDie>(group.Count);

			for (var index = 0; index < group.Count; index++)
				dice.Add(RollDie(group, ref extrasLeft, ref limitReached));

			ApplySelections(group, dice);

			return new DiceGroupRoll
			{
				Group = group,
				Dice = dice.ToImmutableList(),
				Value = dice.Where(die => die.Kept).Sum(die => die.Total)
			};
		}

		/// <summary>
		/// Rolls a single die: the face, then the rerolls, then the explosions it triggers.
		/// </summary>
		/// <param name="group">The group the die belongs to.</param>
		/// <param name="extrasLeft">The remaining budget for explosion dice.</param>
		/// <param name="limitReached">Set when a chain was cut by the budget.</param>
		/// <returns>The rolled die.</returns>
		private DiceDie RollDie(DiceGroup group, ref int extrasLeft, ref bool limitReached)
		{
			var value = NextFace(group.Sides);
			var rerolledFrom = ImmutableList.CreateBuilder<int>();
			var explosions = ImmutableList.CreateBuilder<int>();

			foreach (var reroll in group.Modifiers.OfType<DiceRerollModifier>())
			{
				if (value != reroll.Value)
					continue;

				rerolledFrom.Add(value);
				value = NextFace(group.Sides);

				if (!reroll.Repeats)
					continue;

				var guard = 1;
				while (value == reroll.Value)
				{
					rerolledFrom.Add(value);
					value = NextFace(group.Sides);

					if (++guard >= DiceLimits.MaxDicePerPool)
					{
						limitReached = true;
						break;
					}
				}
			}

			foreach (var explode in group.Modifiers.OfType<DiceExplodeModifier>())
			{
				var threshold = explode.Threshold ?? group.Sides;
				var face = value;

				while (face >= threshold)
				{
					if (extrasLeft <= 0)
					{
						limitReached = true;
						break;
					}

					extrasLeft--;
					face = NextFace(group.Sides);
					explosions.Add(face);
				}
			}

			return new DiceDie
			{
				Value = value,
				RerolledFrom = rerolledFrom.ToImmutable(),
				Explosions = explosions.ToImmutable()
			};
		}

		/// <summary>
		/// Applies the keep/drop modifiers in the order they were written, each one choosing among the
		/// dice the previous ones left in the pool.
		/// </summary>
		/// <param name="group">The group whose modifiers are applied.</param>
		/// <param name="dice">The rolled pool, mutated in place.</param>
		private static void ApplySelections(DiceGroup group, List<DiceDie> dice)
		{
			foreach (var selection in group.Modifiers.OfType<DiceSelectionModifier>())
			{
				var candidates = dice.Where(die => die.Kept).ToList();
				if (candidates.Count == 0)
					return;

				var take = Math.Min(selection.Count, candidates.Count);
				var ordered = selection.Selection is DiceSelection.KeepHighest or DiceSelection.DropHighest
					? candidates.OrderByDescending(die => die.Total).ToList()
					: candidates.OrderBy(die => die.Total).ToList();

				var affected = ordered.Take(take).ToHashSet();
				var keeps = selection.Selection is DiceSelection.KeepHighest or DiceSelection.KeepLowest;

				foreach (var die in candidates)
					die.Kept = keeps == affected.Contains(die);
			}
		}

		/// <summary>
		/// Rolls one face of the group's die.
		/// </summary>
		/// <param name="sides">The number of faces.</param>
		/// <returns>A value between 1 and <paramref name="sides"/> inclusive.</returns>
		private int NextFace(int sides) => random.Next(1, sides + 1);
	}
}
