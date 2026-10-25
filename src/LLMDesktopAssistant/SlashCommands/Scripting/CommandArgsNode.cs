using System.Collections.Immutable;
using LLMDesktopAssistant.SlashCommands.Arguments;
using LLMDesktopAssistant.StructuredValues.Const;

namespace LLMDesktopAssistant.SlashCommands.Scripting
{
	/// <summary>
	/// Builds the neutral structured representation of a command's bound arguments, shared by every script engine:
	/// <c>{ positionals = {...}, keyed = {...}, rest = "...", raw = "..." }</c> with converted values.
	/// </summary>
	/// <remarks>
	/// Lua converts the node to a table, Python to a dict literal and C# to a JSON tree - the shape itself is decided
	/// once, here.
	/// </remarks>
	public static class CommandArgsNode
	{
		/// <summary>Builds the <c>command_args</c> node for the given bound arguments.</summary>
		/// <param name="arguments">The bound arguments (defaults applied, values converted).</param>
		/// <param name="rawArguments">The verbatim argument text after the command token.</param>
		public static ConstNodeDictionaryValue Create(SlashCommandBoundArguments arguments, string rawArguments)
		{
			var positionals = new ConstNodeArrayValue
			{
				Items = arguments.Positionals.Select(argument => FromObject(argument.Value)).ToImmutableList()
			};

			var keyed = new ConstNodeDictionaryValue
			{
				Items = arguments.Keyed.ToImmutableDictionary(
					pair => pair.Key,
					pair => FromObject(pair.Value.Value))
			};

			var items = ImmutableDictionary.CreateBuilder<string, ConstNodeValue>();
			items["positionals"] = positionals;
			items["keyed"] = keyed;
			items["rest"] = new ConstNodeStringValue { Value = arguments.RestPositionalArguments ?? string.Empty };
			items["raw"] = new ConstNodeStringValue { Value = rawArguments ?? string.Empty };

			return new ConstNodeDictionaryValue { Items = items.ToImmutable() };
		}

		private static ConstNodeValue FromObject(object? value) => value switch
		{
			null => ConstNodeNullValue.Instance,
			bool boolean => new ConstNodeBooleanValue { Value = boolean },
			string text => new ConstNodeStringValue { Value = text },
			double number => new ConstNodeNumberValue { Value = number },
			float number => new ConstNodeNumberValue { Value = number },
			decimal number => new ConstNodeNumberValue { Value = (double)number },
			int number => new ConstNodeNumberValue { Value = number },
			long number => new ConstNodeNumberValue { Value = number },
			short number => new ConstNodeNumberValue { Value = number },
			byte number => new ConstNodeNumberValue { Value = number },
			_ => new ConstNodeStringValue { Value = value.ToString() ?? string.Empty }
		};
	}
}
