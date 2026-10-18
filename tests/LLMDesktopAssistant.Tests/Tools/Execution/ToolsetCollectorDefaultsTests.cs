using LLMDesktopAssistant.Addons;
using LLMDesktopAssistant.Agents;
using LLMDesktopAssistant.LLM.Domain;
using LLMDesktopAssistant.LLM.Services;
using LLMDesktopAssistant.LLM.Services.Agents;
using LLMDesktopAssistant.LLM.Settings;
using LLMDesktopAssistant.MCP;
using LLMDesktopAssistant.Tests.Tools;
using LLMDesktopAssistant.Tools;
using LLMDesktopAssistant.Tools.Consents;
using LLMDesktopAssistant.Utils;
using Microsoft.Extensions.DependencyInjection;

namespace LLMDesktopAssistant.Tests.Tools.Execution
{
	/// <summary>
	/// The toolset default must reach every tool, not only the ones with an explicit change entry. The
	/// tool card shows <c>change ?? tool ?? toolset default ?? PolicyBased</c>; the runtime has to agree.
	/// </summary>
	public class ToolsetCollectorDefaultsTests
	{
		[Fact]
		public void AToolWithoutAChange_GetsTheToolsetDefaultAndTheContextFactory()
		{
			var tool = ToolTestContext.CreateTool("untouched");
			var (collector, agent) = CreateCollector(tool);
			agent.Tools.Toolset.ToolsetConfiguration.DefaultApprovalLevel = ToolApprovalLevel.AlwaysAsk;

			var resolved = collector.GetAddonsForAgent(agent).Single(t => t.Name == "untouched");

			Assert.Equal(ToolApprovalLevel.AlwaysAsk, resolved.ApprovalLevel);
			Assert.NotNull(resolved.ContextFactory);
		}

		[Fact]
		public void AToolDeclaration_WinsOverTheToolsetDefault()
		{
			var tool = ToolTestContext.CreateTool("declared",
				info => info.ApprovalLevel = ToolApprovalLevel.AlwaysDisallow);
			var (collector, agent) = CreateCollector(tool);
			agent.Tools.Toolset.ToolsetConfiguration.DefaultApprovalLevel = ToolApprovalLevel.AlwaysAsk;

			var resolved = collector.GetAddonsForAgent(agent).Single(t => t.Name == "declared");

			Assert.Equal(ToolApprovalLevel.AlwaysDisallow, resolved.ApprovalLevel);
		}

		[Fact]
		public void APerToolChange_WinsOverTheToolsetDefault()
		{
			var tool = ToolTestContext.CreateTool("changed");
			var (collector, agent) = CreateCollector(tool);
			var toolset = agent.Tools.Toolset.ToolsetConfiguration;
			toolset.DefaultApprovalLevel = ToolApprovalLevel.AlwaysAsk;
			toolset.Changes[tool.Key] = new ToolChange { ApprovalLevel = ToolApprovalLevel.PolicyBased };

			var resolved = collector.GetAddonsForAgent(agent).Single(t => t.Name == "changed");

			Assert.Equal(ToolApprovalLevel.PolicyBased, resolved.ApprovalLevel);
		}

		private static (ToolsetCollector Collector, ChatAgentDescriptor Agent) CreateCollector(ToolInfo tool)
		{
			var services = new ServiceCollection();
			services.AddSingleton<IAddonAccessor<ToolInfo>>(new FakeAddonAccessor(tool));
			services.AddSingleton<IToolMemorizationService, ToolMemorizationService>();
			services.AddSingleton<IAgentManagementService>(new FakeAgentManager());
			var provider = services.BuildServiceProvider();

			var chat = new Chat(provider);
			var collector = new ToolsetCollector(chat, new FakeChatSettings(), new FakeMcp(), provider);

			return (collector, new ChatAgentDescriptor());
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

			public ChatAgentDescriptor GetAgentDescriptor(Guid agentId) =>
				throw new NotSupportedException();

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
