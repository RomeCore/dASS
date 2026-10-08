using System.Collections.Immutable;
using LLMDesktopAssistant.Data;
using LLMDesktopAssistant.Data.ChatModels;
using LLMDesktopAssistant.LLM.Services.Storage;
using LLMDesktopAssistant.Localization;
using LLMDesktopAssistant.SlashCommands;
using LLMDesktopAssistant.SlashCommands.Arguments;
using LLMDesktopAssistant.SlashCommands.Execution;

namespace LLMDesktopAssistant.Tests.SlashCommands
{
	/// <summary>
	/// The command fingerprint: building it from a resolved invocation and its persistence round-trip.
	/// </summary>
	public class SlashCommandFingerprintTests
	{
		private static SlashCommandInfo Command() => new()
		{
			Name = "grilling",
			Namespaces = ["skill", "matt-pocock"],
			SourceKind = SlashCommandSource.Skill,
			ModelFacingMode = ModelFacingMode.Raw
		};

		private static SlashCommandArgumentSchema Schema()
		{
			var keyed = ImmutableDictionary.CreateBuilder<string, SlashCommandArgument>();
			keyed["wait"] = new SlashCommandArgument { Name = Locale.GetKey("command.argument.wait") };

			return new SlashCommandArgumentSchema
			{
				Positionals = [new SlashCommandArgument { Name = Locale.GetKey("test.arg") }],
				Keyed = keyed.ToImmutable(),
				HasRestPositional = true
			};
		}

		private static SlashCommandBoundArguments Bind(string rawArguments)
		{
			Assert.True(SlashCommandArgumentParser.TryParse(Schema(), rawArguments, out var parsed));
			return SlashCommandArgumentBinder.Bind(Schema(), parsed);
		}

		[Fact]
		public void Create_FromAResolvedCommand_CapturesTheInvocation()
		{
			var fingerprint = SlashCommandFingerprint.Create("grilling", Command(), Bind("alpha beta wait=true"),
				generateIntent: true, generateOutcome: true, SlashCommandExecutionStatus.Executed, effectSummary: "did a thing");

			Assert.Equal("skill:matt-pocock:grilling", fingerprint.Token);
			Assert.Equal("grilling", fingerprint.CommandName);
			Assert.Equal(new[] { "skill", "matt-pocock" }, fingerprint.Namespaces);
			Assert.Equal(SlashCommandSource.Skill, fingerprint.SourceKind);
			Assert.Equal(new[] { "alpha" }, fingerprint.PositionalArguments);
			Assert.Equal("true", fingerprint.KeyedArguments["wait"]);
			Assert.Equal("beta", fingerprint.RestPositionalArguments);
			Assert.Equal(ModelFacingMode.Raw, fingerprint.ModelFacingMode);
			Assert.True(fingerprint.GenerateIntent);
			Assert.True(fingerprint.GenerateOutcome);
			Assert.Equal(SlashCommandExecutionStatus.Executed, fingerprint.Status);
			Assert.Null(fingerprint.Error);
			Assert.Equal("did a thing", fingerprint.EffectSummary);
			Assert.False(fingerprint.IsVisible);
			Assert.False(fingerprint.IsTemporary);
		}

		[Fact]
		public void Create_FromAnUnresolvedToken_IsBuiltFromTheTokenAlone()
		{
			var fingerprint = SlashCommandFingerprint.Create("skill:nope:thing", command: null, arguments: null,
				generateIntent: true, generateOutcome: false, SlashCommandExecutionStatus.Failed,
				error: Locale.GetKey("command.error.unknown"));

			Assert.Equal("skill:nope:thing", fingerprint.Token);
			Assert.Equal("thing", fingerprint.CommandName);
			Assert.Equal(new[] { "skill", "nope" }, fingerprint.Namespaces);
			Assert.Equal(SlashCommandSource.Unknown, fingerprint.SourceKind);
			Assert.Empty(fingerprint.PositionalArguments);
			Assert.Empty(fingerprint.KeyedArguments);
			Assert.Empty(fingerprint.RestPositionalArguments);
			Assert.Equal(SlashCommandExecutionStatus.Failed, fingerprint.Status);
			Assert.Equal("command.error.unknown", fingerprint.Error!.Key);
			Assert.False(fingerprint.GenerateOutcome);
		}

		[Fact]
		public void Fingerprint_RoundTripsThroughTheAdditionalDataSynchronizer()
		{
			using var database = new ChatDatabase(null);
			var fingerprint = SlashCommandFingerprint.Create("grilling", Command(), Bind("alpha beta wait=true"),
				generateIntent: true, generateOutcome: true, SlashCommandExecutionStatus.Executed, effectSummary: "summary");

			using var synchronizer = AdditionalChatDataSynchronizer.FromTarget(
				database, fingerprint, ChatDataParentKind.Message, 1);

			var stored = Assert.IsType<SlashCommandFingerprint>(database.AdditionalChatData.FindAll().Single().Data);

			Assert.Equal("skill:matt-pocock:grilling", stored.Token);
			Assert.Equal("grilling", stored.CommandName);
			Assert.Equal(new[] { "skill", "matt-pocock" }, stored.Namespaces);
			Assert.Equal(SlashCommandSource.Skill, stored.SourceKind);
			Assert.Equal(new[] { "alpha" }, stored.PositionalArguments);
			Assert.Equal("true", stored.KeyedArguments["wait"]);
			Assert.Equal("beta", stored.RestPositionalArguments);
			Assert.True(stored.GenerateIntent);
			Assert.True(stored.GenerateOutcome);
			Assert.Equal(SlashCommandExecutionStatus.Executed, stored.Status);
			Assert.Equal("summary", stored.EffectSummary);
			Assert.Null(stored.Error);
		}
	}
}
