using System.ComponentModel;
using System.Globalization;
using System.Text.Json.Nodes;
using AngleSharp.Dom;
using LLMDesktopAssistant.Localization;
using LLMDesktopAssistant.Tools.Execution.Domain;
using LLMDesktopAssistant.Tools.Execution.Support;
using LLMDesktopAssistant.Utils.Dice;
using Material.Icons;

namespace LLMDesktopAssistant.Tools.Implementations
{
	[ToolModule(chatScoped: false)]
	public class RandomToolModule : ToolModule
	{
		private Random _random = Random.Shared;

		public RandomToolModule()
		{
			AddTool(new ToolInitializationInfo
			{
				Executor = GenerateGUID,
				Name = "random-guid",
				Description = "Generates a globally unique identifier (GUID).",
				NameKey = Locale.GetKey("tool.name.random-guid"),
				DescriptionKey = Locale.GetKey("tool.description.random-guid"),
				CategoryKey = Locale.GetKey("tool.category.random")
			});

			AddTool(new ToolInitializationInfo
			{
				Executor = GenerateRandomInteger,
				Name = "random-integer",
				Description = "Generates a random integer number.",
				NameKey = Locale.GetKey("tool.name.random-integer"),
				DescriptionKey = Locale.GetKey("tool.description.random-integer"),
				CategoryKey = Locale.GetKey("tool.category.random")
			});

			AddTool(new ToolInitializationInfo
			{
				Executor = GenerateRandomFloat,
				Name = "random-float",
				Description = "Generates a random floating-point number.",
				NameKey = Locale.GetKey("tool.name.random-float"),
				DescriptionKey = Locale.GetKey("tool.description.random-float"),
				CategoryKey = Locale.GetKey("tool.category.random")
			});

			AddTool(new ToolInitializationInfo
			{
				Executor = GenerateCoinFlip,
				Name = "random-coin_flip",
				Description = "Simulates flipping a coin. Returns 'Heads' or 'Tails'.",
				NameKey = Locale.GetKey("tool.name.random-coin_flip"),
				DescriptionKey = Locale.GetKey("tool.description.random-coin_flip"),
				CategoryKey = Locale.GetKey("tool.category.random")
			});

			AddTool(new ToolInitializationInfo
			{
				Executor = GenerateCheckChance,
				Name = "random-check_chance",
				Description = "Simulates a chance check with a given probability. Returns 'Success' or 'Failure'.",
				NameKey = Locale.GetKey("tool.name.random-check_chance"),
				DescriptionKey = Locale.GetKey("tool.description.random-check_chance"),
				CategoryKey = Locale.GetKey("tool.category.random")
			});

			AddTool(new ToolInitializationInfo
			{
				Executor = RollDice,
				Name = "random-dice",
				Aliases = ["random-dice_roll"],
				Description = """
					Rolls dice written in standard dice notation and returns the total with a per-group breakdown.
					Notation: XdY (2d6, d20, d% for a d100), flat modifiers (1d20+5), several groups (1d8+2d6+3),
					kh/kl (keep highest/lowest, 4d6kh3, 2d20kh1 for advantage), dh/dl (drop highest/lowest),
					! or !N (explode on the maximum face, or on N and above), rN / rrN (reroll dice showing N once / repeatedly).
					Examples: 2d6, 1d20+5, 2d20kh1, 4d6dl1, 3d6!, 4d6r1.
					""",
				NameKey = Locale.GetKey("tool.name.random-dice"),
				DescriptionKey = Locale.GetKey("tool.description.random-dice"),
				CategoryKey = Locale.GetKey("tool.category.random")
			});

			AddTool(new ToolInitializationInfo
			{
				Executor = GenerateRandomItemsFromList,
				Name = "random-items_from_list",
				Description = "Selects a random item from a provided list.",
				NameKey = Locale.GetKey("tool.name.random-items_from_list"),
				DescriptionKey = Locale.GetKey("tool.description.random-items_from_list"),
				CategoryKey = Locale.GetKey("tool.category.random")
			});

			AddTool(new ToolInitializationInfo
			{
				Executor = GenerateRandomShuffledList,
				Name = "random-shuffle_list",
				Description = "Returns a shuffled version of a provided list.",
				NameKey = Locale.GetKey("tool.name.random-shuffle_list"),
				DescriptionKey = Locale.GetKey("tool.description.random-shuffle_list"),
				CategoryKey = Locale.GetKey("tool.category.random")
			});
		}

		private ToolExecutionResult GenerateGUID(IToolExecutionContext ctx)
		{
			var value = Guid.NewGuid();
			ctx.Call.SetResult(value.ToString());
			ctx.Call.SetStatus(MaterialIconKind.IdCard, value.ToString());
			return ToolExecutionResult.Succeeded;
		}

		private ToolExecutionResult GenerateRandomInteger(IToolExecutionContext ctx,
			[Description("The minimum inclusive value")] long minValue,
			[Description("The maximum inclusive value")] long maxValue)
		{
			var value = _random.NextInt64(minValue, maxValue + 1);
			ctx.Call.SetResult(value.ToString());
			ctx.Call.SetStatus(MaterialIconKind.DiceMultipleOutline, value.ToString());
			return ToolExecutionResult.Succeeded;
		}

		private ToolExecutionResult GenerateRandomFloat(IToolExecutionContext ctx,
			[Description("The minimum inclusive value")] double minValue,
			[Description("The maximum inclusive value")] double maxValue)
		{
			var range = maxValue - minValue;
			var value = _random.NextDouble() * range + minValue;
			ctx.Call.SetResult(value.ToString());
			ctx.Call.SetStatus(MaterialIconKind.DiceMultipleOutline, value.ToString());
			return ToolExecutionResult.Succeeded;
		}

		private ToolExecutionResult GenerateCoinFlip(IToolExecutionContext ctx)
		{
			bool result = _random.NextDouble() < 0.5;
			var value = result ? "Heads" : "Tails";

			ctx.Call.SetResult(value);
			ctx.Call.SetStatus(MaterialIconKind.Coins,
				result ? LocalizationManager.LocalizeStatic("tool.status.random-coin_flip.heads") : LocalizationManager.LocalizeStatic("tool.status.random-coin_flip.tails"));
			return ToolExecutionResult.Succeeded;
		}

		private ToolExecutionResult GenerateCheckChance(IToolExecutionContext ctx,
			[Description("The probability of success as a percentage from 0 to 100")]
			[MainArgument] double chance)
		{
			var gained = _random.NextDouble() * 100;
			var result = gained <= chance;
			var value = $"{gained} < {chance}? " + (result ? "Success" : "Failure");

			ctx.Call.SetResult(value);
			ctx.Call.SetStatus(result ? MaterialIconKind.Check : MaterialIconKind.Close,
				$"{chance}%: " + (result ? LocalizationManager.LocalizeStatic("tool.status.random-check_chance.success") : LocalizationManager.LocalizeStatic("tool.status.random-check_chance.failure")));
			return ToolExecutionResult.Succeeded;
		}

		private ToolExecutionResult RollDice(IToolExecutionContext ctx,
			[Description("Dice expression in standard dice notation, e.g. '2d6+3', '2d20kh1', '4d6dl1', '3d6!'")]
			[MainArgument] string expression,
			[Description("How many times to roll the whole expression, 1 to 100")] int repeat = 1)
		{
			if (repeat < 1 || repeat > DiceLimits.MaxRepeat)
			{
				ctx.Call.SetResult($"Invalid repeat count {repeat}: expected a value between 1 and {DiceLimits.MaxRepeat}.");
				ctx.Call.SetStatus(MaterialIconKind.DiceMultipleOutline, expression);
				return ToolExecutionResult.Failed;
			}

			DiceExpression parsed;
			try
			{
				parsed = DiceExpressionParser.Parse(expression);
			}
			catch (DiceExpressionException exception)
			{
				ctx.Call.SetResult(exception.Message);
				ctx.Call.SetStatus(MaterialIconKind.DiceMultipleOutline, expression);
				return ToolExecutionResult.Failed;
			}

			var roller = new DiceRoller(_random);
			var rolls = new List<DiceRollResult>(repeat);
			for (var index = 0; index < repeat; index++)
				rolls.Add(roller.Roll(parsed));

			ctx.Call.SetResult(repeat == 1
				? rolls[0].ToText()
				: string.Join(Environment.NewLine, rolls.Select((roll, index) => $"#{index + 1} {roll.ToText()}")));

			var rolled = LocalizationManager.LocalizeStatic("tool.status.random-dice.rolled");
			var totals = string.Join(", ", rolls.Select(roll => roll.Total.ToString(CultureInfo.InvariantCulture)));

			ctx.Call.SetStatus(DiceIcon(parsed), rolls.Count == 1
				? $"{rolled} {expression} = {totals}"
				: $"{rolled} {expression} x{rolls.Count} = {totals}");
			ctx.Call.SetStructuredResult(new JsonObject()
			{
				["expression"] = expression,
				["repeat"] = rolls.Count,
				["rolls"] = new JsonArray(rolls.Select(roll => (JsonNode)new JsonObject()
				{
					["total"] = roll.Total,
					["modifier"] = roll.Modifier,
					["text"] = roll.ToText(),
					["groups"] = new JsonArray(roll.Groups.Select(group => (JsonNode)new JsonObject
					{
						["text"] = group.Group.ToNotation(),
						["value"] = group.Value,
						["dice"] = new JsonArray(group.Dice.Select(die => (JsonNode)new JsonObject()
						{
							["value"] = die.Value,
							["total"] = die.Total,
							["kept"] = die.Kept,
							["explosions"] = new JsonArray(die.Explosions.Select(face => (JsonNode?)face).ToArray()),
							["rerolledFrom"] = new JsonArray(die.RerolledFrom.Select(face => (JsonNode?)face).ToArray())
						}).ToArray())
					}).ToArray())
				}).ToArray())
			});

			return ToolExecutionResult.Succeeded;
		}

		/// <summary>
		/// Picks the icon of the expression's die, or the generic one when the groups do not agree.
		/// </summary>
		/// <param name="expression">The rolled expression.</param>
		/// <returns>The icon to show next to the call title.</returns>
		private static MaterialIconKind DiceIcon(DiceExpression expression)
		{
			var sides = expression.Groups.Select(group => group.Sides).Distinct().ToList();
			if (sides.Count != 1)
				return MaterialIconKind.DiceMultipleOutline;

			return sides[0] switch
			{
				4 => MaterialIconKind.DiceD4,
				6 => MaterialIconKind.DiceD6,
				8 => MaterialIconKind.DiceD8,
				10 => MaterialIconKind.DiceD10,
				12 => MaterialIconKind.DiceD12,
				20 => MaterialIconKind.DiceD20,
				_ => MaterialIconKind.DiceMultipleOutline
			};
		}

		private ToolExecutionResult GenerateRandomItemsFromList(IToolExecutionContext ctx,
			[Description("List of items")] [MainArgument] string[] items,
			[Description("Number of items to select")] int count = 1)
		{
			var values = _random.GetItems(items, count);
			var value = string.Join(", ", values);

			ctx.Call.SetResult(value);
			ctx.Call.SetStatus(MaterialIconKind.Shuffle, value);
			return ToolExecutionResult.Succeeded;
		}

		private ToolExecutionResult GenerateRandomShuffledList(IToolExecutionContext ctx,
			[Description("List of items to shuffle")] [MainArgument] string[] items)
		{
			_random.Shuffle(items);
			var value = string.Join(", ", items);

			ctx.Call.SetResult(value);
			ctx.Call.SetStatus(MaterialIconKind.Shuffle,
				string.Format(LocalizationManager.LocalizeStatic("tool.status.random-shuffle_list.shuffled"), items.Length));
			return ToolExecutionResult.Succeeded;
		}
	}
}