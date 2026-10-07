using LLMDesktopAssistant.Localization;

namespace LLMDesktopAssistant.SlashCommands.Arguments
{
	/// <summary>
	/// Turns the raw output of the argument grammar into bound arguments: applies schema defaults, checks required
	/// arguments, validates user-supplied values and converts them.
	/// </summary>
	/// <remarks>
	/// Pure and synchronous on purpose, so the whole argument contract is unit-testable without DI and without the
	/// parser. The caller (the message-insertion service) blocks the send when
	/// <see cref="SlashCommandBoundArguments.Error"/> is set.
	/// </remarks>
	public static class SlashCommandArgumentBinder
	{
		/// <summary>
		/// Binds <paramref name="parsed"/> against <paramref name="schema"/>. The verbatim positional text
		/// (<see cref="SlashCommandArgumentsResult.RawPositionalArguments"/> and
		/// <see cref="SlashCommandArgumentsResult.RestPositionalArguments"/>) is carried over untouched.
		/// </summary>
		/// <remarks>
		/// Binding stops at the first problem and returns empty collections with the error set. Defaults are converted
		/// but never validated (they are authored, not typed by the user).
		/// </remarks>
		public static SlashCommandBoundArguments Bind(
			SlashCommandArgumentSchema schema, SlashCommandArgumentsResult parsed)
		{
			ArgumentNullException.ThrowIfNull(schema);
			ArgumentNullException.ThrowIfNull(parsed);

			// 1. A syntax error from the grammar blocks the send outright.
			if (parsed.Error is not null)
				return Failure(parsed, parsed.Error);

			var positionals = ImmutableList.CreateBuilder<ParsedSlashCommandArgument>();
			var keyed = ImmutableDictionary.CreateBuilder<string, ParsedSlashCommandArgument>();

			// 2. Positionals, one bound entry per declared positional, in schema order.
			for (var i = 0; i < schema.Positionals.Count; i++)
			{
				var definition = schema.Positionals[i];

				if (i < parsed.Positionals.Count)
				{
					if (!TryBindValidated(definition, parsed.Positionals[i], out var bound, out var error))
						return Failure(parsed, error!);
					positionals.Add(bound!);
					continue;
				}

				if (definition.Default is not null)
				{
					positionals.Add(BindDefault(definition, definition.Default));
					continue;
				}

				if (definition.Required)
					return Failure(parsed, Locale.GetKey("command.error.missing_argument"));

				// Absent optional without a default: keep the slot so indices stay aligned with the schema. No raw argument
				// and no ready text — the argument simply is not there.
				positionals.Add(new ParsedSlashCommandArgument { Definition = definition });
			}

			if (parsed.Positionals.Count > schema.Positionals.Count)
				return Failure(parsed, Locale.GetKey("command.error.too_many_arguments"));

			// 3. Keyed arguments, for the declared keys only.
			foreach (var (key, definition) in schema.Keyed)
			{
				if (parsed.Keyed.TryGetValue(key, out var raw))
				{
					if (!TryBindValidated(definition, raw, out var bound, out var error))
						return Failure(parsed, error!);
					keyed[key] = bound!;
				}
				else if (definition.Default is not null)
				{
					keyed[key] = BindDefault(definition, definition.Default);
				}
				else if (definition.Required)
				{
					return Failure(parsed, Locale.GetKey("command.error.missing_argument"));
				}
			}

			return new SlashCommandBoundArguments
			{
				Positionals = positionals.ToImmutable(),
				Keyed = keyed.ToImmutable(),
				RawPositionalArguments = parsed.RawPositionalArguments,
				RestPositionalArguments = parsed.RestPositionalArguments
			};
		}

		/// <summary>
		/// Binds a user-supplied value: validates it through the format provider, then converts it.
		/// </summary>
		private static bool TryBindValidated(SlashCommandArgument definition, SlashCommandRawArgument raw,
			out ParsedSlashCommandArgument? bound, out LocaleKeyBase? error)
		{
			bound = null;
			error = null;

			if (definition.Format is not null && !definition.Format.TryValidate(raw.Unescaped, out error))
			{
				error ??= Locale.GetKey("command.error.invalid_argument");
				return false;
			}

			bound = BindFromInput(definition, raw);
			return true;
		}

		/// <summary>
		/// Binds a value the user wrote: the raw argument is kept (span and all) and the value is converted from its
		/// unescaped text.
		/// </summary>
		private static ParsedSlashCommandArgument BindFromInput(SlashCommandArgument definition, SlashCommandRawArgument raw)
		{
			return new ParsedSlashCommandArgument
			{
				Definition = definition,
				Raw = raw,
				Value = definition.Format is null ? raw.Unescaped : definition.Format.Convert(raw.Unescaped)
			};
		}

		/// <summary>
		/// Binds a schema default: there is no source text, so the raw argument stays <see langword="null"/>, and the
		/// default is converted but never validated (it is authored, not typed by the user).
		/// </summary>
		private static ParsedSlashCommandArgument BindDefault(SlashCommandArgument definition, string value)
		{
			return new ParsedSlashCommandArgument
			{
				Definition = definition,
				Value = definition.Format is null ? value : definition.Format.Convert(value)
			};
		}

		/// <summary>
		/// Builds a failed result: empty collections, the given error and the verbatim positional text carried over. The
		/// parser's error position rides along — it is -1 for an error the binder itself raised.
		/// </summary>
		private static SlashCommandBoundArguments Failure(SlashCommandArgumentsResult parsed, LocaleKeyBase error)
		{
			return new SlashCommandBoundArguments
			{
				Positionals = [],
				Keyed = [],
				RawPositionalArguments = parsed.RawPositionalArguments,
				RestPositionalArguments = parsed.RestPositionalArguments,
				Error = error,
				ErrorPosition = parsed.ErrorPosition
			};
		}
	}
}
