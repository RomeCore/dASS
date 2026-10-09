using LLMDesktopAssistant.LLM.Domain;
using LLMDesktopAssistant.LLM.Services;
using LLMDesktopAssistant.Localization;
using LLMDesktopAssistant.SlashCommands;
using LLMDesktopAssistant.SlashCommands.Arguments;
using LLMDesktopAssistant.SlashCommands.Execution;
using LLMDesktopAssistant.SlashCommands.Resolution;
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

		private sealed class RecordingExecutionService : IChatExecutionService
		{
			public int GenerateCalls { get; private set; }

			public Task GenerateResponseAsync(CancellationToken cancellationToken = default)
			{
				GenerateCalls++;
				return Task.CompletedTask;
			}

			public Task GenerateResponseWithAgentAsync(Guid agentId, Guid agentStageId,
				CancellationToken cancellationToken = default) => Task.CompletedTask;
		}

		private sealed class Harness : IDisposable
		{
			public ChatStorageTestContext Storage { get; } = new();
			public FakeChatSettingsService Settings { get; } = new();
			public FakeResolver Resolver { get; } = new();
			public RecordingExecutionService Executor { get; } = new();
			public ChatMessageInsertionService Service { get; }

			public Harness()
			{
				Service = new ChatMessageInsertionService(
					Storage.Chat, Storage.Service, Settings, Resolver, new ChatExecutionTokenService(), Executor);
			}

			public UserMessage LastMessage => (UserMessage)Storage.Chat.Messages[^1].Message;

			public void Dispose() => Storage.Dispose();
		}

		private static UserInput Input(string content) => new() { Content = content, SenderLogin = "user" };

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
		public void CanInsertUserInput_APlainMessage_IsAccepted_AndHasNoSideEffect()
		{
			using var harness = new Harness();

			var check = harness.Service.CanInsertUserInput(Input("hello there"), generateIntent: true);

			Assert.True(check.Success);
			Assert.Null(check.Error);
			Assert.Empty(harness.Storage.Chat.Messages);
		}

		[Fact]
		public void CanInsertUserInput_AnUnknownCommand_IsRefused()
		{
			using var harness = new Harness();

			var check = harness.Service.CanInsertUserInput(Input("/nope"), generateIntent: true);

			Assert.False(check.Success);
			Assert.Equal("command.error.unknown", check.Error!.Key);
			Assert.Equal(-1, check.ErrorPosition);
			Assert.Empty(harness.Storage.Chat.Messages);
		}

		[Fact]
		public void CanInsertUserInput_ACommandWithBadArguments_IsRefused()
		{
			using var harness = new Harness();
			var schema = new SlashCommandArgumentSchema
			{
				Positionals = [new SlashCommandArgument { Name = Locale.GetKey("test.arg"), Required = true }]
			};
			harness.Resolver.Add(Command("grilling", new RecordingExecutor() { ArgumentSchema = schema }));

			var check = harness.Service.CanInsertUserInput(Input("/grilling"), generateIntent: true);

			Assert.False(check.Success);
			Assert.Equal("command.error.missing_argument", check.Error!.Key);
		}

		[Fact]
		public void CanInsertUserInput_AValidCommand_IsAccepted()
		{
			using var harness = new Harness();
			harness.Resolver.Add(Command("grilling", new RecordingExecutor() { ArgumentSchema = RestSchema() }));

			Assert.True(harness.Service.CanInsertUserInput(Input("/grilling do it"), generateIntent: true).Success);
		}

		[Fact]
		public void CanInsertUserInput_APlainEdit_IsAccepted()
		{
			using var harness = new Harness();

			Assert.True(harness.Service.CanInsertUserInput(Input("hello there"), generateIntent: true, editIndex: 0).Success);
		}

		[Fact]
		public void CanInsertUserInput_AnEditWithAnUnknownCommand_IsRefused()
		{
			using var harness = new Harness();

			var check = harness.Service.CanInsertUserInput(Input("/nope"), generateIntent: true, editIndex: 0);

			Assert.False(check.Success);
			Assert.Equal("command.error.unknown", check.Error!.Key);
		}

		[Fact]
		public void CanInsertUserInput_AnEditWithBadArguments_IsRefused()
		{
			using var harness = new Harness();
			var schema = new SlashCommandArgumentSchema
			{
				Positionals = [new SlashCommandArgument { Name = Locale.GetKey("test.arg"), Required = true }]
			};
			harness.Resolver.Add(Command("grilling", new RecordingExecutor() { ArgumentSchema = schema }));

			var check = harness.Service.CanInsertUserInput(Input("/grilling"), generateIntent: true, editIndex: 0);

			Assert.False(check.Success);
			Assert.Equal("command.error.missing_argument", check.Error!.Key);
		}

		[Fact]
		public void CanInsertUserInput_AValidEditCommand_IsAccepted()
		{
			using var harness = new Harness();
			harness.Resolver.Add(Command("grilling", new RecordingExecutor() { ArgumentSchema = RestSchema() }));

			Assert.True(harness.Service.CanInsertUserInput(Input("/grilling do it"), generateIntent: true, editIndex: 0).Success);
		}

		[Fact]
		public void CanInsertUserInput_AnEdit_WhenCommandsAreDisabled_IsAccepted()
		{
			using var harness = new Harness();
			harness.Settings.Settings.Commands.EnableCommands = false;

			Assert.True(harness.Service.CanInsertUserInput(Input("/nope"), generateIntent: true, editIndex: 0).Success);
		}

		[Fact]
		public void CanInsertUserInput_WhenCommandsAreDisabled_IsAccepted()
		{
			using var harness = new Harness();
			harness.Settings.Settings.Commands.EnableCommands = false;

			Assert.True(harness.Service.CanInsertUserInput(Input("/nope"), generateIntent: true).Success);
		}

		[Fact]
		public async Task Insert_AnUnknownCommand_InsertsTheMessageWithAnError_AndSkipsExecutionAndGeneration()
		{
			using var harness = new Harness();

			await harness.Service.InsertUserInputAsync(Input("/nope"), generateIntent: true);

			Assert.Equal("/nope", harness.LastMessage.Content);
			Assert.Equal("command.error.unknown", harness.LastMessage.Error!.Key);
			Assert.Equal(0, harness.Executor.GenerateCalls);
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

			await harness.Service.InsertUserInputAsync(Input("/grilling do it"), generateIntent: true);

			Assert.Equal("/grilling do it", harness.LastMessage.Content);
			Assert.Null(harness.LastMessage.Error);
			Assert.Equal(1, executor.Calls);
			Assert.Equal(1, harness.Executor.GenerateCalls);

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

			await harness.Service.InsertUserInputAsync(Input("/grilling"), generateIntent: true);

			Assert.Equal(0, harness.Executor.GenerateCalls);
		}

		[Fact]
		public async Task Insert_ACommandWithGenerateFalse_DoesNotGenerate()
		{
			using var harness = new Harness();
			harness.Resolver.Add(Command("grilling", new RecordingExecutor(), generate: false));

			await harness.Service.InsertUserInputAsync(Input("/grilling"), generateIntent: true);

			Assert.Equal(0, harness.Executor.GenerateCalls);
		}

		[Fact]
		public async Task Insert_APlainMessage_GeneratesPerTheIntent()
		{
			using var harness = new Harness();

			await harness.Service.InsertUserInputAsync(Input("hello"), generateIntent: true);

			Assert.Equal("hello", harness.LastMessage.Content);
			Assert.Equal(1, harness.Executor.GenerateCalls);
		}

		[Fact]
		public async Task Insert_AnEscapedMessage_StoresTheUnescapedContent()
		{
			using var harness = new Harness();

			await harness.Service.InsertUserInputAsync(Input("//hello"), generateIntent: false);

			Assert.Equal("/hello", harness.LastMessage.Content);
		}

		[Fact]
		public async Task Insert_WhenCommandsAreDisabled_SendsTheMessageLiterally()
		{
			using var harness = new Harness();
			harness.Settings.Settings.Commands.EnableCommands = false;

			await harness.Service.InsertUserInputAsync(Input("/nope"), generateIntent: false);

			Assert.Equal("/nope", harness.LastMessage.Content);
			Assert.Null(harness.LastMessage.Error);
		}

		[Fact]
		public async Task Insert_WhenTheExecutorThrows_AttachesTheError_AndDoesNotGenerate()
		{
			using var harness = new Harness();
			harness.Resolver.Add(Command("grilling", new RecordingExecutor { Throws = new InvalidOperationException("boom") }));

			await harness.Service.InsertUserInputAsync(Input("/grilling"), generateIntent: true);

			Assert.NotNull(harness.LastMessage.Error);
			Assert.Equal("boom", harness.LastMessage.Error!.Value);
			Assert.Equal(0, harness.Executor.GenerateCalls);
		}

		[Fact]
		public async Task Insert_AValidCommand_RecordsAnExecutedFingerprint()
		{
			using var harness = new Harness();
			harness.Resolver.Add(Command("grilling", new RecordingExecutor() { ArgumentSchema = RestSchema() }));

			await harness.Service.InsertUserInputAsync(Input("/grilling do it"), generateIntent: true);

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

			await harness.Service.InsertUserInputAsync(Input("/nope"), generateIntent: true);

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

			await harness.Service.InsertUserInputAsync(Input("/grilling"), generateIntent: true);

			var fingerprint = harness.LastMessage.AdditionalData.TryGet<SlashCommandFingerprint>();
			Assert.Equal(SlashCommandExecutionStatus.Failed, fingerprint!.Status);
			Assert.Equal("command.error.invalid_argument", fingerprint.Error!.Key);
			Assert.False(fingerprint.GenerateOutcome);
			Assert.Equal(0, harness.Executor.GenerateCalls);
		}

		[Fact]
		public async Task Insert_ACancelledCommand_RecordsACancelledFingerprint()
		{
			using var harness = new Harness();
			harness.Resolver.Add(Command("grilling", new RecordingExecutor { Throws = new OperationCanceledException() }));

			await harness.Service.InsertUserInputAsync(Input("/grilling"), generateIntent: true);

			var fingerprint = harness.LastMessage.AdditionalData.TryGet<SlashCommandFingerprint>();
			Assert.Equal(SlashCommandExecutionStatus.Cancelled, fingerprint!.Status);
			Assert.Null(fingerprint.Error);
			Assert.Null(harness.LastMessage.Error);
			Assert.Equal(0, harness.Executor.GenerateCalls);
		}

		[Fact]
		public async Task Insert_WhenTheExecutorOverridesTheMode_RecordsItInTheFingerprint()
		{
			using var harness = new Harness();
			harness.Resolver.Add(Command("grilling", new RecordingExecutor
			{
				Result = SlashCommandExecutionResult.Ok(modelFacingMode: ModelFacingMode.Neutral)
			}));

			await harness.Service.InsertUserInputAsync(Input("/grilling"), generateIntent: false);

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

			await harness.Service.InsertUserInputAsync(Input("/grilling"), generateIntent: false);

			var fingerprint = harness.LastMessage.AdditionalData.TryGet<SlashCommandFingerprint>();
			Assert.Equal(ModelFacingMode.Neutral, fingerprint!.ModelFacingMode);
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
