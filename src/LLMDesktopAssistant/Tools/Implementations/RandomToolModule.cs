using System.ComponentModel;
using LLMDesktopAssistant.Localization;
using LLMDesktopAssistant.Tools.Execution.Domain;
using LLMDesktopAssistant.Tools.Execution.Support;
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
				Name = "random-GUID",
				Description = "Generates a globally unique identifier (GUID).",
				NameKey = Locale.GetKey("tool.name.random-GUID"),
				DescriptionKey = Locale.GetKey("tool.description.random-GUID"),
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
				Executor = GenerateRandomDiceRoll,
				Name = "random-dice_roll",
				Description = "Simulates rolling dice (e.g., 2d6, 1d20).",
				NameKey = Locale.GetKey("tool.name.random-dice_roll"),
				DescriptionKey = Locale.GetKey("tool.description.random-dice_roll"),
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
			[Description("The probability of success as a percentage from 0 to 100")] double chance)
		{
			var gained = _random.NextDouble() * 100;
			var result = gained <= chance;
			var value = $"{gained} < {chance}? " + (result ? "Success" : "Failure");

			ctx.Call.SetResult(value);
			ctx.Call.SetStatus(result ? MaterialIconKind.Check : MaterialIconKind.Close,
				$"{chance}%: " + (result ? LocalizationManager.LocalizeStatic("tool.status.random-check_chance.success") : LocalizationManager.LocalizeStatic("tool.status.random-check_chance.failure")));
			return ToolExecutionResult.Succeeded;
		}

		private ToolExecutionResult GenerateRandomDiceRoll(IToolExecutionContext ctx,
			[Description("Count of dices to roll")] int numberOfDice = 1,
			[Description("Number of sides per die")] int sides = 6)
		{
			var rolls = new List<int>();
			for (int i = 0; i < numberOfDice; i++)
			{
				rolls.Add(_random.Next(1, sides + 1));
			}

			var total = rolls.Sum();
			var value = $"{total} (Rolls: {string.Join(", ", rolls)})";

			ctx.Call.SetResult(value);
			ctx.Call.SetStatus(sides switch
			{
				4 => MaterialIconKind.DiceD4,
				6 => MaterialIconKind.DiceD6,
				8 => MaterialIconKind.DiceD8,
				10 => MaterialIconKind.DiceD10,
				12 => MaterialIconKind.DiceD12,
				20 => MaterialIconKind.DiceD20,
				_ => MaterialIconKind.DiceMultiple
			},
				$"{LocalizationManager.LocalizeStatic("tool.status.random-dice_roll.rolled")} {numberOfDice}d{sides}: {string.Join(", ", rolls)}");
			return ToolExecutionResult.Succeeded;
		}

		private ToolExecutionResult GenerateRandomItemsFromList(IToolExecutionContext ctx,
			[Description("List of items")] string[] items,
			[Description("Number of items to select")] int count = 1)
		{
			var values = _random.GetItems(items, count);
			var value = string.Join(", ", values);

			ctx.Call.SetResult(value);
			ctx.Call.SetStatus(MaterialIconKind.Shuffle, value);
			return ToolExecutionResult.Succeeded;
		}

		private ToolExecutionResult GenerateRandomShuffledList(IToolExecutionContext ctx,
			[Description("List of items to shuffle")] string[] items)
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