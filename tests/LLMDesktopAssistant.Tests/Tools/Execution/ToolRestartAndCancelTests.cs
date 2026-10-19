using LLMDesktopAssistant.Addons;
using LLMDesktopAssistant.Agents;
using LLMDesktopAssistant.LLM.Domain;
using LLMDesktopAssistant.LLM.MVVM.Additional;
using LLMDesktopAssistant.LLM.Services;
using LLMDesktopAssistant.LLM.Services.Agents;
using LLMDesktopAssistant.LLM.Settings;
using LLMDesktopAssistant.MCP;
using LLMDesktopAssistant.Tests.Tools;
using LLMDesktopAssistant.Tools;
using LLMDesktopAssistant.Tools.Consents;
using LLMDesktopAssistant.Tools.Execution;
using LLMDesktopAssistant.Tools.Execution.Dispatch;
using LLMDesktopAssistant.Tools.Execution.Domain;
using LLMDesktopAssistant.Tools.Execution.Orchestrators;
using LLMDesktopAssistant.Tools.Execution.Services;
using LLMDesktopAssistant.Utils;
using Microsoft.Extensions.DependencyInjection;
using DomainToolCall = LLMDesktopAssistant.LLM.Domain.ToolCall;

namespace LLMDesktopAssistant.Tests.Tools.Execution
{
	/// <summary>
	/// Restart and cancel of a single call: initialization resets the reused call and links its own cancellation
	/// source to the run, and <see cref="ChatToolCallService"/> is the user's entry point for both actions.
	/// </summary>
	public class ToolRestartAndCancelTests
	{
		// ==============================
		// === Initialization         ===
		// ==============================

		[Fact]
		public void Initialize_ResetsEverythingThePreviousRunLeft()
		{
			var session = ToolTestContext.Create(ToolTestContext.CreateTool());
			session.Call.Status = ToolStatus.Success;
			session.Call.ResultContent = "the old result";
			session.Call.StatusTitle = "old title";
			session.Call.UseMarkdown = true;
			session.Call.SetProgress(0.5, 0, 1);
			session.Call.AddAttachment(new AdditionalMessageContentPart { Content = "old", IsRestorable = false });

			var ct = CancellationToken.None;
			new ToolInitializationService().Initialize(session.Context, ref ct);

			Assert.Equal(ToolStatus.None, session.Call.Status);
			Assert.Null(session.Call.ResultContent);
			Assert.Null(session.Call.StatusTitle);
			Assert.False(session.Call.UseMarkdown);
			Assert.Null(session.Call.Progress);
			Assert.Empty(session.Call.GetAttachments());
		}

		[Fact]
		public void Initialize_LinksTheCallToTheRunWithoutTheOtherWayAround()
		{
			var session = ToolTestContext.Create(ToolTestContext.CreateTool());
			using var run = new CancellationTokenSource();
			var ct = run.Token;

			new ToolInitializationService().Initialize(session.Context, ref ct);

			// The call's own source cancels the call and nothing else: the run and its siblings survive.
			session.Call.CancellationTokenSource!.Cancel();
			Assert.False(run.IsCancellationRequested);
			Assert.True(ct.IsCancellationRequested);
		}

		[Fact]
		public void Initialize_LetsTheRunCancelTheCall()
		{
			var session = ToolTestContext.Create(ToolTestContext.CreateTool());
			using var run = new CancellationTokenSource();
			var ct = run.Token;
			new ToolInitializationService().Initialize(session.Context, ref ct);

			run.Cancel();

			Assert.True(session.Call.CancellationTokenSource!.IsCancellationRequested);
		}

		// ==============================
		// === The user's actions     ===
		// ==============================

		[Fact]
		public void Cancel_IsAvailableOnlyWhileTheCallIsUnfinished()
		{
			var (service, _, call) = CreateSession(ToolStatus.Executing);

			Assert.True(service.CanCancel(call));

			service.Cancel(call);

			Assert.True(call.CancellationTokenSource!.IsCancellationRequested);
			Assert.False(service.CanCancel(call));
		}

		[Fact]
		public void Cancel_OfAFinishedCall_DoesNothing()
		{
			var (service, _, call) = CreateSession(ToolStatus.Success);

			Assert.False(service.CanCancel(call));

			service.Cancel(call);

			Assert.False(call.CancellationTokenSource!.IsCancellationRequested);
		}

		[Fact]
		public void Cancel_AwaitsApprovalAsARunningCall()
		{
			var (service, _, call) = CreateSession(ToolStatus.WaitingForApproval);

			Assert.True(service.CanCancel(call));
		}

		[Fact]
		public void CanRestart_RequiresAFinishedCallAndAnExistingTool()
		{
			var (running, _, runningCall) = CreateSession(ToolStatus.Executing);
			Assert.False(running.CanRestart(runningCall));

			var (service, _, call) = CreateSession(ToolStatus.Success);
			Assert.True(service.CanRestart(call));
		}

		[Fact]
		public void CanRestart_IsFalseWhenTheToolIsGone()
		{
			var (service, _, call) = CreateSession(ToolStatus.Success, toolName: "a tool that no longer exists");

			Assert.False(service.CanRestart(call));
		}

		[Fact]
		public async Task Restart_RunsTheRestartSequenceWithoutPolicyOrConfirmation()
		{
			var (service, stages, call) = CreateSession(ToolStatus.Success);

			await service.RestartAsync(CreateHost(), call, CancellationToken.None);

			Assert.Equal(
				["initialization", "streaming", "validation", "synchronization", "preview", "execution", "settlement"],
				stages.Calls);
		}

		[Fact]
		public async Task Restart_DoesNothingWhenTheToolIsGone()
		{
			var (service, stages, call) = CreateSession(ToolStatus.Success, toolName: "vanished");

			await service.RestartAsync(CreateHost(), call, CancellationToken.None);

			Assert.Empty(stages.Calls);
		}

		private static AssistantMessage CreateHost() => new()
		{
			AgentStageId = Guid.Empty,
			SenderAgentId = Guid.Empty,
			Status = AssistantMessageStatus.Success
		};

		/// <summary>
		/// Builds a chat-scoped call service over one tool, a real dispatcher and recording stages.
		/// </summary>
		private static (ChatToolCallService Service, RecordingStages Stages, DomainToolCall Call) CreateSession(
			ToolStatus status, string? toolName = null)
		{
			var tool = ToolTestContext.CreateTool("restartable");

			var services = new ServiceCollection();
			services.AddSingleton<IAddonAccessor<ToolInfo>>(new FakeAddonAccessor(tool));
			var provider = services.BuildServiceProvider();
			var chat = new Chat(provider);

			var call = new DomainToolCall
			{
				ToolCallId = Guid.NewGuid().ToString("N"),
				ToolName = toolName ?? tool.Name,
				Status = status,
				CancellationTokenSource = new CancellationTokenSource()
			};

			var stages = new RecordingStages();
			var dispatcher = new ToolInvocationDispatcher(
				new ChatToolOrchestrator(stages, stages, stages, stages, stages, stages, stages, stages, stages),
				new ChatRestartOrchestrator(stages, stages, stages, stages, stages, stages, stages),
				new ToolCommandOrchestrator(stages, stages, stages, stages, stages, stages, stages),
				new AgentTaskToolOrchestrator(stages, stages, stages, stages, stages, stages, stages, stages),
				new ScriptToolOrchestrator(stages, stages, stages, stages, stages));

			var service = new ChatToolCallService(
				chat,
				new ToolsetCollector(chat, new FakeChatSettings(), new FakeMcp(), provider),
				dispatcher,
				new ToolMemorizationService(),
				new FakeAgentManager(),
				new FakeChatSettings());

			return (service, stages, call);
		}

		private sealed class FakeAddonAccessor(params ToolInfo[] tools) : IAddonAccessor<ToolInfo>
		{
			public ReadOnlyObservableCollection<ToolInfo> Addons { get; } = new(tools.ToList());
		}

		private sealed class FakeChatSettings : IChatSettingsService
		{
			public ChatSettings Settings { get; } = new();

			public event EventHandler? SettingsChanged
			{
				add { }
				remove { }
			}

			public void SetSettings(ChatSettings settings) => throw new NotSupportedException();

			public void LoadFromProfile(string profileName = "-default") => throw new NotSupportedException();
		}

		private sealed class FakeAgentManager : IAgentManagementService
		{
			public IEnumerable<Guid> ListAgentIds() => [];

			public IEnumerable<(ChatAgentDescriptor Agent, bool IsGlobal)> ListAgents() => [];

			public ChatAgentDescriptor GetAgentDescriptor(Guid agentId) => throw new NotSupportedException();

			public ChatAgentDescriptor? TryGetAgentDescriptor(Guid agentId) => null;
		}

		private sealed class FakeMcp : IMCPManagementService
		{
			public bool HasMCPConnections() => false;

			public Task EnsureCurrentMCPConnectionsAsync(CancellationToken cancellationToken = default) =>
				Task.CompletedTask;

			public MCPToolModule[] GetMCPTools() => [];
		}
	}
}
