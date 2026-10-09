using System.Collections.Immutable;
using LLMDesktopAssistant.Localization;
using LLMDesktopAssistant.SlashCommands.Arguments;

namespace LLMDesktopAssistant.Tests.SlashCommands
{
	public class SlashCommandArgumentBinderTests
	{
		private sealed class FakeFormatProvider : ISlashCommandArgumentFormatProvider
		{
			public Func<string, bool> Validate { get; set; } = _ => true;
			public LocaleKeyBase? ValidationError { get; set; }
			public Func<string, object?> Converter { get; set; } = raw => raw;

			public int ValidateCalls { get; private set; }
			public int ConvertCalls { get; private set; }

			public bool CanComplete => false;

			public bool TryValidate(string raw, out LocaleKeyBase? error)
			{
				ValidateCalls++;
				error = ValidationError;
				return Validate(raw);
			}

			public object? Convert(string raw)
			{
				ConvertCalls++;
				return Converter(raw);
			}

			public IEnumerable<SlashCommandCompletionItem> Complete(string prefix, SlashCommandCompletionContext ctx) => [];
		}

		private static SlashCommandArgument Argument(string? @default = null, bool required = false,
			ISlashCommandArgumentFormatProvider? format = null)
		{
			return new SlashCommandArgument
			{
				Name = Locale.GetKey("test.argument"),
				Required = required,
				Default = @default,
				Format = format
			};
		}

		private static SlashCommandRawArgument Raw(string value, SlashCommandArgument? definition = null,
			bool quoted = false)
		{
			return new SlashCommandRawArgument
			{
				Definition = definition,
				Raw = value,
				Unescaped = value,
				WasQuoted = quoted,
				Position = 0,
				Length = value.Length,
				ValuePosition = 0,
				KeyLength = 0
			};
		}

		private static SlashCommandParsedArguments RawResult(
			IEnumerable<SlashCommandRawArgument>? positionals = null,
			IEnumerable<KeyValuePair<string, SlashCommandRawArgument>>? keyed = null,
			string rawArguments = "",
			string rawPositionalArguments = "",
			string restPositionalArguments = "",
			int errorPosition = -1,
			LocaleKeyBase? error = null)
		{
			return new SlashCommandParsedArguments
			{
				RawArguments = rawArguments,
				RawPositionalArguments = rawPositionalArguments,
				RestPositional = restPositionalArguments.Length == 0 ? null : Raw(restPositionalArguments),
				Positionals = positionals is null ? [] : [.. positionals],
				Keyed = keyed is null ? [] : ImmutableDictionary.CreateRange(keyed),
				ErrorPosition = errorPosition,
				Error = error
			};
		}

		private static SlashCommandArgumentSchema SchemaWithKey(string key, SlashCommandArgument argument)
		{
			return new SlashCommandArgumentSchema
			{
				Keyed = ImmutableDictionary<string, SlashCommandArgument>.Empty.Add(key, argument)
			};
		}

		[Fact]
		public void EmptySchema_And_NoArguments_BindToNothing()
		{
			var result = SlashCommandArgumentBinder.Bind(new SlashCommandArgumentSchema(), RawResult());

			Assert.True(result.IsValid);
			Assert.Empty(result.Positionals);
			Assert.Empty(result.Keyed);
			Assert.Equal(string.Empty, result.RawPositionalArguments);
		}

		[Fact]
		public void Positionals_AreBoundByIndex()
		{
			var schema = new SlashCommandArgumentSchema { Positionals = [Argument(), Argument()] };

			var result = SlashCommandArgumentBinder.Bind(schema, RawResult(positionals: [Raw("first"), Raw("second")]));

			Assert.True(result.IsValid);
			Assert.Equal(2, result.Positionals.Count);
			Assert.Equal("first", result.Positionals[0].Ready);
			Assert.Equal("first", result.Positionals[0].Value);
			Assert.Equal("second", result.Positionals[1].Ready);
		}

		[Fact]
		public void MissingRequiredPositional_Blocks()
		{
			var schema = new SlashCommandArgumentSchema { Positionals = [Argument(required: true)] };

			var result = SlashCommandArgumentBinder.Bind(schema, RawResult());

			Assert.False(result.IsValid);
			Assert.Equal("command.error.missing_argument", result.Error!.Key);
			Assert.Empty(result.Positionals);
		}

		[Fact]
		public void AbsentOptionalPositionalWithDefault_UsesTheDefault()
		{
			var schema = new SlashCommandArgumentSchema { Positionals = [Argument(@default: "fallback")] };

			var result = SlashCommandArgumentBinder.Bind(schema, RawResult());

			Assert.True(result.IsValid);
			Assert.Equal("fallback", result.Positionals[0].Ready);
			Assert.Equal("fallback", result.Positionals[0].Value);
		}

		[Fact]
		public void AbsentOptionalPositional_KeepsItsSlot()
		{
			var schema = new SlashCommandArgumentSchema { Positionals = [Argument(), Argument()] };

			var result = SlashCommandArgumentBinder.Bind(schema, RawResult(positionals: [Raw("first")]));

			Assert.True(result.IsValid);
			Assert.Equal(2, result.Positionals.Count);
			Assert.Equal("first", result.Positionals[0].Ready);
			Assert.Null(result.Positionals[1].Raw);
			Assert.Null(result.Positionals[1].Ready);
			Assert.Null(result.Positionals[1].Value);
		}

		[Fact]
		public void SurplusPositional_Blocks()
		{
			var schema = new SlashCommandArgumentSchema { Positionals = [Argument()] };

			var result = SlashCommandArgumentBinder.Bind(schema, RawResult(positionals: [Raw("a"), Raw("b")]));

			Assert.False(result.IsValid);
			Assert.Equal("command.error.too_many_arguments", result.Error!.Key);
		}

		[Fact]
		public void KeyedArgument_IsBoundByItsDeclaredKey()
		{
			var schema = SchemaWithKey("wait", Argument(@default: "false"));

			var result = SlashCommandArgumentBinder.Bind(schema,
				RawResult(keyed: [KeyValuePair.Create("wait", Raw("true"))]));

			Assert.True(result.IsValid);
			Assert.Equal("true", result.Keyed["wait"].Ready);
			Assert.Equal("true", result.Keyed["wait"].Value);
		}

		[Fact]
		public void AbsentKeyedArgumentWithDefault_UsesTheDefault()
		{
			var schema = SchemaWithKey("wait", Argument(@default: "false"));

			var result = SlashCommandArgumentBinder.Bind(schema, RawResult());

			Assert.True(result.IsValid);
			Assert.Equal("false", result.Keyed["wait"].Ready);
		}

		[Fact]
		public void AbsentRequiredKeyedArgument_Blocks()
		{
			var schema = SchemaWithKey("wait", Argument(required: true));

			var result = SlashCommandArgumentBinder.Bind(schema, RawResult());

			Assert.False(result.IsValid);
			Assert.Equal("command.error.missing_argument", result.Error!.Key);
		}

		[Fact]
		public void AbsentOptionalKeyedArgumentWithoutDefault_IsAbsent()
		{
			var schema = SchemaWithKey("wait", Argument());

			var result = SlashCommandArgumentBinder.Bind(schema, RawResult());

			Assert.True(result.IsValid);
			Assert.False(result.Keyed.ContainsKey("wait"));
		}

		[Fact]
		public void UndeclaredKeyedArgument_IsIgnored()
		{
			var result = SlashCommandArgumentBinder.Bind(new SlashCommandArgumentSchema(),
				RawResult(keyed: [KeyValuePair.Create("other", Raw("x"))]));

			Assert.True(result.IsValid);
			Assert.Empty(result.Keyed);
		}

		[Fact]
		public void InvalidValue_Blocks_AndIsNotConverted()
		{
			var format = new FakeFormatProvider
			{
				Validate = _ => false,
				ValidationError = Locale.GetKey("test.invalid")
			};
			var schema = SchemaWithKey("wait", Argument(format: format));

			var result = SlashCommandArgumentBinder.Bind(schema,
				RawResult(keyed: [KeyValuePair.Create("wait", Raw("maybe"))]));

			Assert.False(result.IsValid);
			Assert.Equal("test.invalid", result.Error!.Key);
			Assert.Equal(0, format.ConvertCalls);
			Assert.Empty(result.Keyed);
		}

		[Fact]
		public void InvalidValue_WithoutProviderError_FallsBackToInvalidArgument()
		{
			var format = new FakeFormatProvider { Validate = _ => false, ValidationError = null };
			var schema = SchemaWithKey("wait", Argument(format: format));

			var result = SlashCommandArgumentBinder.Bind(schema,
				RawResult(keyed: [KeyValuePair.Create("wait", Raw("maybe"))]));

			Assert.False(result.IsValid);
			Assert.Equal("command.error.invalid_argument", result.Error!.Key);
		}

		[Fact]
		public void ValidValue_IsConverted()
		{
			var format = new FakeFormatProvider { Converter = raw => raw == "true" };
			var schema = SchemaWithKey("wait", Argument(format: format));

			var result = SlashCommandArgumentBinder.Bind(schema,
				RawResult(keyed: [KeyValuePair.Create("wait", Raw("true"))]));

			Assert.True(result.IsValid);
			Assert.Equal(true, result.Keyed["wait"].Value);
			Assert.Equal(1, format.ValidateCalls);
			Assert.Equal(1, format.ConvertCalls);
		}

		[Fact]
		public void Defaults_AreConvertedButNotValidated()
		{
			var format = new FakeFormatProvider
			{
				Validate = _ => false,
				Converter = raw => $"<{raw}>"
			};
			var schema = SchemaWithKey("wait", Argument(@default: "false", format: format));

			var result = SlashCommandArgumentBinder.Bind(schema, RawResult());

			Assert.True(result.IsValid);
			Assert.Equal(0, format.ValidateCalls);
			Assert.Equal("false", result.Keyed["wait"].Ready);
			Assert.Equal("<false>", result.Keyed["wait"].Value);
		}

		[Fact]
		public void UserSuppliedValue_KeepsItsRawArgument()
		{
			var schema = new SlashCommandArgumentSchema { Positionals = [Argument()] };

			var result = SlashCommandArgumentBinder.Bind(schema, RawResult(positionals: [Raw("first")]));

			Assert.NotNull(result.Positionals[0].Raw);
			Assert.Equal("first", result.Positionals[0].Raw!.Unescaped);
			Assert.Equal("first", result.Positionals[0].Ready);
			Assert.Equal("first", result.Positionals[0].Value);
		}

		[Fact]
		public void DefaultedValue_HasNoRawArgumentButHasReady()
		{
			var schema = new SlashCommandArgumentSchema { Positionals = [Argument(@default: "fallback")] };

			var result = SlashCommandArgumentBinder.Bind(schema, RawResult());

			Assert.Null(result.Positionals[0].Raw);
			Assert.Equal("fallback", result.Positionals[0].Ready);
			Assert.Equal("fallback", result.Positionals[0].Value);
		}

		[Fact]
		public void ParseError_IsPropagated()
		{
			var result = SlashCommandArgumentBinder.Bind(new SlashCommandArgumentSchema(),
				RawResult(error: Locale.GetKey("command.error.parse_error"), rawPositionalArguments: "rest",
					errorPosition: 3));

			Assert.False(result.IsValid);
			Assert.Equal("command.error.parse_error", result.Error!.Key);
			Assert.Equal(3, result.ErrorPosition);
			Assert.Equal("rest", result.RawPositionalArguments);
			Assert.Empty(result.Positionals);
		}

		[Fact]
		public void BindingError_HasNoPosition()
		{
			var schema = new SlashCommandArgumentSchema { Positionals = [Argument(required: true)] };

			var result = SlashCommandArgumentBinder.Bind(schema, RawResult());

			Assert.False(result.IsValid);
			Assert.Equal("command.error.missing_argument", result.Error!.Key);
			Assert.Equal(-1, result.ErrorPosition);
		}

		[Fact]
		public void RestPositional_IsCarriedThrough()
		{
			var schema = new SlashCommandArgumentSchema { RestPositional = Argument() };

			var result = SlashCommandArgumentBinder.Bind(schema, RawResult(rawPositionalArguments: "hello world"));

			Assert.True(result.IsValid);
			Assert.Equal("hello world", result.RawPositionalArguments);
		}

		[Fact]
		public void RawAndRestPositionalArguments_AreCarriedThroughSeparately()
		{
			var schema = new SlashCommandArgumentSchema { RestPositional = Argument() };

			var result = SlashCommandArgumentBinder.Bind(schema,
				RawResult(rawPositionalArguments: "one two", restPositionalArguments: "two"));

			Assert.True(result.IsValid);
			Assert.Equal("one two", result.RawPositionalArguments);
			Assert.Equal("two", result.RestPositionalArguments);
		}
	}
}
