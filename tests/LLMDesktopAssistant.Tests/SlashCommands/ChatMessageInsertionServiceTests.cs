using System.Text.Json.Nodes;
using LLMDesktopAssistant.Hooks;
using LLMDesktopAssistant.Hooks.Chat;
using LLMDesktopAssistant.LLM.Domain;
using LLMDesktopAssistant.LLM.Services;
using LLMDesktopAssistant.Localization;
using LLMDesktopAssistant.SlashCommands;
using LLMDesktopAssistant.SlashCommands.Arguments;
using LLMDesktopAssistant.SlashCommands.Execution;
using LLMDesktopAssistant.SlashCommands.Resolution;
using LLMDesktopAssistant.StructuredValues.Converters;
using LLMDesktopAssistant.Tests.Fakes;
using LLMDesktopAssistant.Tests.Storage;

namespace LLMDesktopAssistant.Tests.SlashCommands
{
	/// <summary>
	/// The message-insertion host: the pre-flight check, the guard, content/escape handling and the intent ceiling.
	/// </summary>
	public class ChatMessageInsertionServiceTests
	{
		private sealed class FakeResolver : ISlashCommandResolver
		{
			private readonly Dictionary<string, SlashCommandInfo> _commands = new(StringComparer.OrdinalIgnoreCase);

			public void Add(SlashCommandInfo command) => _commands[command.Name] = command;

			public SlashCommandResolution Resolve(string token) => _commands.TryGetValue(token, out var command)
				? new SlashCommandResolution(command, null, SlashCommandResolutionStatus.Exact, [])
				: new SlashCommandResolution(null, Locale.GetFormattedKey("command.error.unknown", token),
					SlashCommandResolutionStatus.Unknown, []);
		}

		private sealed class RecordingExecutor : ISlashCommandExecutor
		{
			public int Calls { get; private set; }
			public SlashCommandExecutionContext? LastContext { get; private set; }
			public SlashCommandExecutionResult Result { get; set; } = SlashCommandExecutionResult.Ok();
			public Exception? Throws { get; set; }

			public SlashCommandArgumentSchema? ArgumentSchema { get; set; }

			public Task<SlashCommandExecutionResult> ExecuteAsync(SlashCommandExecutionContext ctx, CancellationToken ct)
			{
				Calls++;
				LastContext = ctx;
				return Throws is null ? Task.FromResult(Result) : Task.FromException<SlashCommandExecutionResult>(Throws);
			}
		}

		private sealed class Harness : IDisposable
		{
			public ChatStorageTestContext Storage { get; } = new();
			public FakeChatSettingsService Settings { get; } = new();
			public FakeResolver Resolver { get; } = new();
			public FakeHookDispatcher Hooks { get; } = new();
			public ChatMessageInsertionService Service { get; }

			public Harness()
			{
				Service = new ChatMessageInsertionService(
					Storage.Chat,
					Storage.Service,
					Settings,
					new ChatCommandInvocationService(Storage.Chat, Resolver, new ChatExecutionTokenService()),
					new FakeUserManagementService(),
					Hooks);
			}

			public UserMessage LastMessage => (UserMessage)Storage.Chat.Messages[^1].Message;

			/// <summary>
			/// Appends the input and runs the command it carries, which is what the production caller does in two
			/// steps so that the queue can drop an entry in between.
			/// </summary>
			public async Task<UserInputInsertionResult> InsertAsync(UserInputState input, bool generateIntent,
				int? editIndex = null)
			{
				var insertion = await Service.AppendUserInputAsync(input, editIndex);
				Assert.NotNull(insertion);
				return await Service.RunInsertedCommandAsync(insertion!, generateIntent);
			}

			public void Dispose() => Storage.Dispose();
		}

		private static UserInputState Input(string content) => new() { Content = content, SenderLogin = "user" };

		private static SlashCommandInfo Command(string name, ISlashCommandExecutor executor, bool generate = true) => new()
		{
			Name = name,
			Namespaces = ["skill"],
			Generate = generate,
			Executor = executor
		};

		private static SlashCommandArgumentSchema RestSchema() => new()
		{
			RestPositional = new SlashCommandArgument { Name = Locale.GetKey("test.argument.rest") }
		};

		[Fact]
		public async Task CanInsertUserInputAsync_APlainMessage_IsAccepted_AndHasNoSideEffect()
		{
			using var harness = new Harness();

			var check = await harness.Service.CanInsertUserInputAsync(Input("hello there"), UserSubmitKind.Direct, generate: true);

			Assert.True(check.Success);
			Assert.Null(check.Error);
			Assert.Empty(harness.Storage.Chat.Messages);
		}

		[Fact]
		public async Task CanInsertUserInputAsync_AnUnknownCommand_IsRefused()
		{
			using var harness = new Harness();

			var check = await harness.Service.CanInsertUserInputAsync(Input("/nope"), UserSubmitKind.Direct, generate: true);

			Assert.False(check.Success);
			Assert.Equal("command.error.unknown", check.Error!.Key);
			Assert.Equal(-1, check.ErrorPosition);
			Assert.Empty(harness.Storage.Chat.Messages);
		}

		[Fact]
		public async Task CanInsertUserInputAsync_ACommandWithBadArguments_IsRefused()
		{
			using var harness = new Harness();
			var schema = new SlashCommandArgumentSchema
			{
				Positionals = [new SlashCommandArgument { Name = Locale.GetKey("test.arg"), Required = true }]
			};
			harness.Resolver.Add(Command("grilling", new RecordingExecutor() { ArgumentSchema = schema }));

			var check = await harness.Service.CanInsertUserInputAsync(Input("/grilling"), UserSubmitKind.Direct, generate: true);

			Assert.False(check.Success);
			Assert.Equal("command.error.missing_argument", check.Error!.Key);
		}

		[Fact]
		public async Task CanInsertUserInputAsync_AValidCommand_IsAccepted()
		{
			using var harness = new Harness();
			harness.Resolver.Add(Command("grilling", new RecordingExecutor() { ArgumentSchema = RestSchema() }));

			Assert.True((await harness.Service.CanInsertUserInputAsync(Input("/grilling do it"), UserSubmitKind.Direct, generate: true)).Success);
		}

		[Fact]
		public async Task CanInsertUserInputAsync_APlainEdit_IsAccepted()
		{
			using var harness = new Harness();

			Assert.True((await harness.Service.CanInsertUserInputAsync(Input("hello there"), UserSubmitKind.Direct, generate: true)).Success);
		}

		[Fact]
		public async Task CanInsertUserInputAsync_AnEditWithAnUnknownCommand_IsRefused()
		{
			using var harness = new Harness();

			var check = await harness.Service.CanInsertUserInputAsync(Input("/nope"), UserSubmitKind.Direct, generate: true);

			Assert.False(check.Success);
			Assert.Equal("command.error.unknown", check.Error!.Key);
		}

		[Fact]
		public async Task CanInsertUserInputAsync_AnEditWithBadArguments_IsRefused()
		{
			using var harness = new Harness();
			var schema = new SlashCommandArgumentSchema
			{
				Positionals = [new SlashCommandArgument { Name = Locale.GetKey("test.arg"), Required = true }]
			};
			harness.Resolver.Add(Command("grilling", new RecordingExecutor() { ArgumentSchema = schema }));

			var check = await harness.Service.CanInsertUserInputAsync(Input("/grilling"), UserSubmitKind.Direct, generate: true);

			Assert.False(check.Success);
			Assert.Equal("command.error.missing_argument", check.Error!.Key);
		}

		[Fact]
		public async Task CanInsertUserInputAsync_AValidEditCommand_IsAccepted()
		{
			using var harness = new Harness();
			harness.Resolver.Add(Command("grilling", new RecordingExecutor() { ArgumentSchema = RestSchema() }));

			Assert.True((await harness.Service.CanInsertUserInputAsync(Input("/grilling do it"), UserSubmitKind.Direct, generate: true)).Success);
		}

		[Fact]
		public async Task CanInsertUserInputAsync_AnEdit_WhenCommandsAreDisabled_IsAccepted()
		{
			using var harness = new Harness();
			harness.Settings.Settings.Commands.AddonsCategoryEnabled = false;

			Assert.True((await harness.Service.CanInsertUserInputAsync(Input("/nope"), UserSubmitKind.Direct, generate: true)).Success);
		}

		[Fact]
		public async Task CanInsertUserInputAsync_WhenCommandsAreDisabled_IsAccepted()
		{
			using var harness = new Harness();
			harness.Settings.Settings.Commands.AddonsCategoryEnabled = false;

			Assert.True((await harness.Service.CanInsertUserInputAsync(Input("/nope"), UserSubmitKind.Direct, generate: true)).Success);
		}

		[Fact]
		public async Task Insert_AnUnknownCommand_InsertsTheMessageWithAnError_AndSkipsExecutionAndGeneration()
		{
			using var harness = new Harness();

			var result = await harness.InsertAsync(Input("/nope"), generateIntent: true);

			Assert.Equal("/nope", harness.LastMessage.Content);
			Assert.Equal("command.error.unknown", harness.LastMessage.Error!.Key);
			Assert.False(result.Generate);
		}

		[Fact]
		public async Task Insert_AValidCommand_InsertsTheMessage_RunsTheExecutor_AndGenerates()
		{
			using var harness = new Harness();
			var executor = new RecordingExecutor()
			{
				ArgumentSchema = RestSchema()
			};
			harness.Resolver.Add(Command("grilling", executor));

			var result = await harness.InsertAsync(Input("/grilling do it"), generateIntent: true);

			Assert.Equal("/grilling do it", harness.LastMessage.Content);
			Assert.Null(harness.LastMessage.Error);
			Assert.Equal(1, executor.Calls);
			Assert.True(result.Generate);

			var context = executor.LastContext!;
			Assert.Equal("skill:grilling", context.Token);
			Assert.Equal("grilling", context.RawToken);
			Assert.Equal("/grilling do it", context.RawText);
			Assert.Equal("do it", context.Arguments.RestPositionalArguments);
		}

		[Fact]
		public async Task Insert_ACancellableCommand_DoesNotGenerate()
		{
			using var harness = new Harness();
			harness.Resolver.Add(Command("grilling", new RecordingExecutor { Result = SlashCommandExecutionResult.Ok(generate: false) }));

			var result = await harness.InsertAsync(Input("/grilling"), generateIntent: true);

			Assert.False(result.Generate);
		}

		[Fact]
		public async Task Insert_ACommandWithGenerateFalse_DoesNotGenerate()
		{
			using var harness = new Harness();
			harness.Resolver.Add(Command("grilling", new RecordingExecutor(), generate: false));

			var result = await harness.InsertAsync(Input("/grilling"), generateIntent: true);

			Assert.False(result.Generate);
		}

		[Fact]
		public async Task Insert_APlainMessage_GeneratesPerTheIntent()
		{
			using var harness = new Harness();

			var result = await harness.InsertAsync(Input("hello"), generateIntent: true);

			Assert.Equal("hello", harness.LastMessage.Content);
			Assert.True(result.Generate);
		}

		[Fact]
		public async Task Insert_AnEscapedMessage_StoresTheUnescapedContent()
		{
			using var harness = new Harness();

			await harness.InsertAsync(Input("//hello"), generateIntent: false);

			Assert.Equal("/hello", harness.LastMessage.Content);
		}

		[Fact]
		public async Task Insert_WhenCommandsAreDisabled_SendsTheMessageLiterally()
		{
			using var harness = new Harness();
			harness.Settings.Settings.Commands.AddonsCategoryEnabled = false;

			await harness.InsertAsync(Input("/nope"), generateIntent: false);

			Assert.Equal("/nope", harness.LastMessage.Content);
			Assert.Null(harness.LastMessage.Error);
		}

		[Fact]
		public async Task Insert_WhenTheExecutorThrows_AttachesTheError_AndDoesNotGenerate()
		{
			using var harness = new Harness();
			harness.Resolver.Add(Command("grilling", new RecordingExecutor { Throws = new InvalidOperationException("boom") }));

			var result = await harness.InsertAsync(Input("/grilling"), generateIntent: true);

			Assert.NotNull(harness.LastMessage.Error);
			Assert.Equal("boom", harness.LastMessage.Error!.Value);
			Assert.False(result.Generate);
		}

		[Fact]
		public async Task Insert_AValidCommand_RecordsAnExecutedFingerprint()
		{
			using var harness = new Harness();
			harness.Resolver.Add(Command("grilling", new RecordingExecutor() { ArgumentSchema = RestSchema() }));

			await harness.InsertAsync(Input("/grilling do it"), generateIntent: true);

			var fingerprint = harness.LastMessage.AdditionalData.TryGet<SlashCommandFingerprint>();
			Assert.NotNull(fingerprint);
			Assert.Equal("skill:grilling", fingerprint!.Token);
			Assert.Equal("do it", fingerprint.RestPositionalArguments);
			Assert.Equal(SlashCommandExecutionStatus.Executed, fingerprint.Status);
			Assert.True(fingerprint.GenerateIntent);
			Assert.True(fingerprint.GenerateOutcome);
			Assert.Null(fingerprint.Error);
			Assert.False(fingerprint.IsVisible);
			Assert.False(fingerprint.IsTemporary);
		}

		[Fact]
		public async Task Insert_AnUnknownCommand_RecordsAFailedFingerprint()
		{
			using var harness = new Harness();

			await harness.InsertAsync(Input("/nope"), generateIntent: true);

			var fingerprint = harness.LastMessage.AdditionalData.TryGet<SlashCommandFingerprint>();
			Assert.NotNull(fingerprint);
			Assert.Equal(SlashCommandExecutionStatus.Failed, fingerprint!.Status);
			Assert.Equal("command.error.unknown", fingerprint.Error!.Key);
			Assert.False(fingerprint.GenerateOutcome);
		}

		[Fact]
		public async Task Insert_WhenTheExecutorReturnsAnError_RecordsAFailedFingerprint()
		{
			using var harness = new Harness();
			harness.Resolver.Add(Command("grilling", new RecordingExecutor
			{
				Result = new SlashCommandExecutionResult(true, Locale.GetKey("command.error.invalid_argument"))
			}));

			var result = await harness.InsertAsync(Input("/grilling"), generateIntent: true);

			var fingerprint = harness.LastMessage.AdditionalData.TryGet<SlashCommandFingerprint>();
			Assert.Equal(SlashCommandExecutionStatus.Failed, fingerprint!.Status);
			Assert.Equal("command.error.invalid_argument", fingerprint.Error!.Key);
			Assert.False(fingerprint.GenerateOutcome);
			Assert.False(result.Generate);
		}

		[Fact]
		public async Task Insert_ACancelledCommand_RecordsACancelledFingerprint()
		{
			using var harness = new Harness();
			harness.Resolver.Add(Command("grilling", new RecordingExecutor { Throws = new OperationCanceledException() }));

			var result = await harness.InsertAsync(Input("/grilling"), generateIntent: true);

			var fingerprint = harness.LastMessage.AdditionalData.TryGet<SlashCommandFingerprint>();
			Assert.Equal(SlashCommandExecutionStatus.Cancelled, fingerprint!.Status);
			Assert.Null(fingerprint.Error);
			Assert.Null(harness.LastMessage.Error);
			Assert.False(result.Generate);
		}

		[Fact]
		public async Task Insert_WhenTheExecutorOverridesTheMode_RecordsItInTheFingerprint()
		{
			using var harness = new Harness();
			harness.Resolver.Add(Command("grilling", new RecordingExecutor
			{
				Result = SlashCommandExecutionResult.Ok(modelFacingMode: ModelFacingMode.Neutral)
			}));

			await harness.InsertAsync(Input("/grilling"), generateIntent: false);

			var fingerprint = harness.LastMessage.AdditionalData.TryGet<SlashCommandFingerprint>();
			Assert.Equal(ModelFacingMode.Neutral, fingerprint!.ModelFacingMode);
		}

		[Fact]
		public async Task Insert_WhenTheExecutorDoesNotOverrideTheMode_KeepsTheCommandsMode()
		{
			using var harness = new Harness();
			var command = Command("grilling", new RecordingExecutor());
			command.ModelFacingMode = ModelFacingMode.Neutral;
			harness.Resolver.Add(command);

			await harness.InsertAsync(Input("/grilling"), generateIntent: false);

			var fingerprint = harness.LastMessage.AdditionalData.TryGet<SlashCommandFingerprint>();
			Assert.Equal(ModelFacingMode.Neutral, fingerprint!.ModelFacingMode);
		}

		/// <summary>
		/// Builds the answer a dispatcher gives: a verdict document exactly as a hook would return it.
		/// </summary>
		private static HookExecutionResult Verdict(string json) =>
			new(JsonNode.Parse(json).ToConstNodeValue(), null);

		[Fact]
		public async Task CanInsertUserInputAsync_AHookRefusal_RefusesTheSendWithTheReason()
		{
			using var harness = new Harness();
			harness.Hooks.Result = Verdict("""{ "decision": "disallow", "reason": "not now" }""");

			var check = await harness.Service.CanInsertUserInputAsync(Input("hello"), UserSubmitKind.Direct,
				generate: true);

			Assert.False(check.Success);
			Assert.Equal("not now", check.Error!.Value);
			Assert.Empty(harness.Storage.Chat.Messages);
			Assert.Equal("message.user.submit.direct", harness.Hooks.Calls[0].HookType);
			Assert.Equal("user", harness.Hooks.Calls[0].HookTarget);
		}

		[Fact]
		public async Task CanInsertUserInputAsync_AHookRefusalWithoutAReason_UsesTheDefaultText()
		{
			using var harness = new Harness();
			harness.Hooks.Result = Verdict("""{ "decision": "error" }""");

			var check = await harness.Service.CanInsertUserInputAsync(Input("hello"), UserSubmitKind.Queue,
				generate: false);

			Assert.False(check.Success);
			Assert.Equal("chat.hook.submit.error", check.Error!.Key);
			Assert.Equal("message.user.submit.queue", harness.Hooks.Calls[0].HookType);
		}

		[Fact]
		public async Task CanInsertUserInputAsync_AHookAllow_AcceptsTheSend()
		{
			using var harness = new Harness();
			harness.Hooks.Result = Verdict("""{ "decision": "allow" }""");

			var check = await harness.Service.CanInsertUserInputAsync(Input("hello"), UserSubmitKind.Edit,
				generate: true);

			Assert.True(check.Success);
			Assert.Equal("message.user.submit.edit", harness.Hooks.Calls[0].HookType);
		}

		[Fact]
		public async Task Insert_AHookRefusal_WritesNothing_AndSkipsTheCommand()
		{
			using var harness = new Harness();
			var executor = new RecordingExecutor();
			harness.Resolver.Add(Command("grilling", executor));
			harness.Hooks.Result = Verdict("""{ "decision": "disallow", "reason": "no" }""");

			var insertion = await harness.Service.AppendUserInputAsync(Input("/grilling"));

			Assert.Null(insertion);
			Assert.Empty(harness.Storage.Chat.Messages);
			Assert.Equal(0, executor.Calls);
			Assert.Equal("message.user.insert.pre", harness.Hooks.Calls[0].HookType);
		}

		[Fact]
		public async Task Insert_APlainMessage_FiresTheInsertHooksAroundTheWrite()
		{
			using var harness = new Harness();

			await harness.InsertAsync(Input("hello"), generateIntent: false);

			var events = harness.Hooks.Calls.Select(call => call.HookType).ToArray();
			Assert.Equal(["message.user.insert.pre", "message.user.insert.post"], events);
		}
	}

	/// <summary>
	/// The generation intent ceiling: a command may lower the caller's intent, never raise it.
	/// </summary>
	public class SlashCommandIntentTests
	{
		[Theory]
		[InlineData(true, true, true, true)]
		[InlineData(true, true, false, false)]
		[InlineData(true, false, false, false)]
		[InlineData(true, false, true, false)]
		[InlineData(false, true, true, false)]
		[InlineData(false, false, true, false)]
		[InlineData(false, true, false, false)]
		[InlineData(false, false, false, false)]
		public void Resolve_OnlyLowersTheIntent(bool intent, bool ceiling, bool outcome, bool expected)
			=> Assert.Equal(expected, SlashCommandIntent.Resolve(intent, ceiling, outcome));
	}
}
