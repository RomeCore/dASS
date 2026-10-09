using System.Collections.Immutable;
using LLMDesktopAssistant.Agents;
using LLMDesktopAssistant.Agents.SubAgents;
using LLMDesktopAssistant.Agents.Tasks;
using LLMDesktopAssistant.LLM.Domain;
using LLMDesktopAssistant.LLM.MVVM.Additional;
using LLMDesktopAssistant.LLM.Services;
using LLMDesktopAssistant.LLM.Services.Agents;
using LLMDesktopAssistant.LLM.Services.Tools;
using LLMDesktopAssistant.LLM.Settings;
using LLMDesktopAssistant.Localization;
using LLMDesktopAssistant.SlashCommands;
using LLMDesktopAssistant.SlashCommands.Arguments;
using LLMDesktopAssistant.SlashCommands.Execution;
using LLMDesktopAssistant.SlashCommands.Providers;
using LLMDesktopAssistant.Tools;

namespace LLMDesktopAssistant.Tests.SlashCommands
{
	/// <summary>
	/// The <c>/agent:&lt;name&gt;</c> executor: the sub-agent launch, the chat-level policy override, the
	/// fire-and-forget default and the <c>wait</c> result injection.
	/// </summary>
	public class SubAgentCommandExecutorTests
	{
		private sealed class FakeParamsResolver : ISubAgentTaskParamsResolver
		{
			public TaskSubAgentDescriptor? LastDescriptor { get; private set; }
			public AgentChatMessage[] LastMessages { get; private set; } = [];
			public ToolPolicyMask? LastPolicy { get; private set; }
			public List<string> Errors { get; set; } = [];
			public bool ThrowNotFound { get; set; }

			public AgentTaskLaunchParameters Resolve(AgentTaskLaunchParameters sourceParameters,
				TaskSubAgentDescriptor descriptor, IEnumerable<AgentChatMessage> additionalMessages,
				out List<string> errors, ToolPolicyMask? policyOverride = null)
			{
				if (ThrowNotFound)
					throw new KeyNotFoundException();

				LastDescriptor = descriptor;
				LastMessages = [.. additionalMessages];
				LastPolicy = policyOverride;
				errors = Errors;
				return sourceParameters;
			}
		}

		private sealed class FakeAgentTaskExecutor : IAgentTaskExecutor
		{
			public int Calls { get; private set; }
			public AgentTaskLaunchParameters? LastParameters { get; private set; }
			public CancellationToken LastToken { get; private set; }
			public string? ResultContent { get; set; }

			public AgentTask? Current => null;

			public AgentTask Execute(AgentTaskLaunchParameters parameters, CancellationToken cancellationToken = default)
			{
				Calls++;
				LastParameters = parameters;
				LastToken = cancellationToken;

				var task = new AgentTask
				{
					Id = Guid.NewGuid(),
					Parent = null,
					LaunchParameters = parameters,
					Completion = Task.FromResult<AgentTask>(null!),
					CancellationTokenSource = new CancellationTokenSource()
				};
				task.LastGeneratedContent = ResultContent;
				return task;
			}
		}

		private sealed class FakeChatSettings : IChatSettingsService
		{
			public ChatSettings Settings { get; } = new();
			public event EventHandler? SettingsChanged { add { } remove { } }
			public void SetSettings(ChatSettings settings) { }
			public void LoadFromProfile(string profileName) { }
		}

		private sealed class FakeToolsetCache : IToolsetCacheService
		{
			public ImmutableDictionary<string, ToolInfo> AvailableTools => [];
			public ImmutableDictionary<string, ToolInfo> AliasedTools => [];
			public ImmutableDictionary<string, ToolInfo> ValidTools => [];
			public ImmutableDictionary<string, ToolInfo> ValidAliasedTools => [];

			public void Invalidate(ChatAgentDescriptor? agent)
			{
			}
		}

		private readonly FakeParamsResolver _resolver = new();
		private readonly FakeAgentTaskExecutor _executor = new();
		private readonly FakeChatSettings _chatSettings = new();
		private readonly FakeToolsetCache _toolsetCache = new();

		public SubAgentCommandExecutorTests()
		{
			// Resolve the policy at the chat level so the effective policy does not reach into the app settings.
			_chatSettings.Settings.SubAgents.PolicyInheritance = ChatSettingsInheritanceLevel.Agent;
		}

		private static UserMessage Message(string content)
		{
			var message = new UserMessage
			{
				Content = content,
				SenderLogin = "user",
				Visibility = MessageVisibility.Always,
				VisibleTo = [],
				IsVisibleToWhiteList = false
			};

			// Raise the collection events synchronously (see SkillCommandExecutorTests): marshalling to the Avalonia
			// UI thread deadlocks the test host when two test classes do it in parallel.
			message.AdditionalData.RaiseInUIThread = false;
			return message;
		}

		private static SlashCommandBoundArguments Arguments(string rest, bool wait) => new()
		{
			Positionals = [],
			Keyed = ImmutableDictionary<string, ParsedSlashCommandArgument>.Empty.Add("wait",
				new ParsedSlashCommandArgument
				{
					Definition = new SlashCommandArgument { Name = Locale.GetKey("command.argument.wait") },
					Value = wait
				}),
			RawPositionalArguments = rest,
			RestPositionalArguments = rest
		};

		private SlashCommandExecutionContext Context(UserMessage message, string rest, bool wait) => new()
		{
			Chat = null!,
			Message = message,
			Command = new SlashCommandInfo { Name = "web-searcher", Namespaces = ["agent"] },
			Token = "agent:web-searcher",
			RawToken = "web-searcher",
			RawText = message.Content,
			RawArguments = rest,
			Arguments = Arguments(rest, wait),
			GenerateIntent = true,
			Services = null!
		};

		private SubAgentCommandExecutor Executor()
			=> new(new SubAgentInfo { Name = "web-searcher", Description = "searches the web" },
				_resolver, _executor, _chatSettings, _toolsetCache);

		[Fact]
		public async Task LaunchesTheSubAgent_WhenNotWaiting()
		{
			var message = Message("/agent:web-searcher find it");
			var ctx = Context(message, "find it", wait: false);

			var result = await Executor().ExecuteAsync(ctx, CancellationToken.None);

			Assert.True(result.IsSuccess);
			Assert.True(result.Generate);
			Assert.Equal(ModelFacingMode.Raw, result.ModelFacingMode);
			Assert.Contains("web-searcher", result.EffectSummary!);

			Assert.Equal(1, _executor.Calls);
			Assert.Equal("web-searcher", _executor.LastParameters!.TaskName);
			Assert.Same(message, _executor.LastParameters.TriggeredMessage);
			Assert.Equal(CancellationToken.None, _executor.LastToken);

			var input = Assert.IsType<AgentUserMessage>(Assert.Single(_resolver.LastMessages));
			Assert.Equal("find it", input.Content);
		}

		[Fact]
		public async Task PassesTheChatLevelPolicy_ThroughTheResolverOverride()
		{
			var message = Message("/agent:web-searcher x");
			var ctx = Context(message, "x", wait: false);
			var expected = _chatSettings.Settings.SubAgents.GetEffectivePolicy();

			await Executor().ExecuteAsync(ctx, CancellationToken.None);

			Assert.Equal(expected, _resolver.LastPolicy);
		}

		[Fact]
		public async Task InjectsTheResult_WhenWaiting()
		{
			_executor.ResultContent = "the answer";
			var message = Message("/agent:web-searcher x");
			var ctx = Context(message, "x", wait: true);

			var result = await Executor().ExecuteAsync(ctx, CancellationToken.None);

			Assert.True(result.IsSuccess);
			Assert.Contains("finished", result.EffectSummary!);

			var part = Assert.Single(message.AdditionalData.OfType<AdditionalMessageContentPart>());
			Assert.StartsWith("[USER HAS LAUNCHED AGENT THAT FINISHED WITH MESSAGE]:", part.Content);
			Assert.Contains("the answer", part.Content);
			Assert.False(part.IsRestorable);
			Assert.Equal("command.agent.result", part.ChipTitle!.Key);
		}

		[Fact]
		public async Task ReturnsAnError_WhenTheSubAgentIsMissing()
		{
			_resolver.ThrowNotFound = true;
			var message = Message("/agent:web-searcher x");
			var ctx = Context(message, "x", wait: false);

			var result = await Executor().ExecuteAsync(ctx, CancellationToken.None);

			Assert.False(result.IsSuccess);
			Assert.Equal("command.error.sub_agent_not_found", result.Error!.Key);
		}

		[Fact]
		public async Task ReturnsAnError_WhenResolutionReportsErrors()
		{
			_resolver.Errors = ["Skill was not found: x"];
			var message = Message("/agent:web-searcher x");
			var ctx = Context(message, "x", wait: false);

			var result = await Executor().ExecuteAsync(ctx, CancellationToken.None);

			Assert.False(result.IsSuccess);
		}
	}
}
