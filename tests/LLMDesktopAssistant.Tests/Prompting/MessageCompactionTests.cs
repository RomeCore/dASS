using System.Text.Json;
using LLMDesktopAssistant.Agents;
using LLMDesktopAssistant.LLM.Domain;
using LLMDesktopAssistant.LLM.Services.Agents;
using LLMDesktopAssistant.LLM.Services.Prompting;
using LLMDesktopAssistant.Prompting;
using LLMDesktopAssistant.Tests.Storage;
using LLMDesktopAssistant.Users;
using LLTSharp;
using LLTSharp.Metadata;
using RCLargeLanguageModels.Tasks;

namespace LLMDesktopAssistant.Tests.Prompting;

/// <summary>
/// Tests for the per-message compaction rules derived from effective checkpoints.
/// The checkpoint coverage is computed by <see cref="AgentEffectiveMessagesProvider"/>;
/// the compaction effects themselves are applied by <see cref="ChatMessageQuoteRenderer"/>.
/// </summary>
[Collection("Prompting")]
public class MessageCompactionTests
{
	// ==================== Effective checkpoint coverage (provider) ====================

	private static AgentEffectiveMessagesProvider CreateProvider(Chat chat, out FakeMessageVisibilityService visibility)
	{
		visibility = new FakeMessageVisibilityService();
		return new AgentEffectiveMessagesProvider(chat, new FakeChatSettingsService(), visibility);
	}

	private static ContextCheckpointKind GetCheckpointKind(EffectiveChatContext context, string content)
		=> context.Messages.Single(m => m.BranchedMessage.Message.Content == content).AffectedCheckpoints;

	private static bool HasCompaction(ContextCheckpointKind kind)
		=> (kind & (ContextCheckpointKind.ToolCompaction
			| ContextCheckpointKind.ForcedToolCompaction
			| ContextCheckpointKind.ReasoningCompaction)) != ContextCheckpointKind.None;

	[Fact]
	public void NoCheckpoints_NoCompactionRules()
	{
		var chat = PromptingTestHelpers.CreateChat(
			PromptingTestHelpers.User("u0", 0),
			PromptingTestHelpers.Assistant("a1", 1));
		var provider = CreateProvider(chat, out _);
		var agent = PromptingTestHelpers.CreateAgent();

		var effective = provider.GetEffectiveMessages(agent);

		Assert.All(effective.Messages, m => Assert.False(HasCompaction(m.AffectedCheckpoints)));
	}

	[Fact]
	public void ToolCompaction_CoversMessagesUpToItsIndexInclusive()
	{
		var u0 = PromptingTestHelpers.User("u0", 0);
		var a1 = PromptingTestHelpers.Assistant("a1", 1);
		var u2 = PromptingTestHelpers.User("u2", 2);
		var a3 = PromptingTestHelpers.Assistant("a3", 3);
		var chat = PromptingTestHelpers.CreateChat(u0, a1, u2, a3);
		PromptingTestHelpers.AddCheckpoint(u2, ContextCheckpointKind.ToolCompaction);

		var provider = CreateProvider(chat, out _);
		var agent = PromptingTestHelpers.CreateAgent();

		var effective = provider.GetEffectiveMessages(agent);

		Assert.True(GetCheckpointKind(effective, "u0").HasFlag(ContextCheckpointKind.ToolCompaction));
		Assert.True(GetCheckpointKind(effective, "u2").HasFlag(ContextCheckpointKind.ToolCompaction));
		Assert.False(GetCheckpointKind(effective, "a3").HasFlag(ContextCheckpointKind.ToolCompaction));
	}

	[Fact]
	public void ReasoningCompaction_SetsCompactionFlag()
	{
		var u0 = PromptingTestHelpers.User("u0", 0);
		var a1 = PromptingTestHelpers.Assistant("a1", 1);
		var u2 = PromptingTestHelpers.User("u2", 2);
		var chat = PromptingTestHelpers.CreateChat(u0, a1, u2);
		PromptingTestHelpers.AddCheckpoint(a1, ContextCheckpointKind.ReasoningCompaction);

		var provider = CreateProvider(chat, out _);
		var agent = PromptingTestHelpers.CreateAgent();

		var effective = provider.GetEffectiveMessages(agent);

		Assert.True(GetCheckpointKind(effective, "u0").HasFlag(ContextCheckpointKind.ReasoningCompaction));
		Assert.True(GetCheckpointKind(effective, "a1").HasFlag(ContextCheckpointKind.ReasoningCompaction));
		Assert.False(GetCheckpointKind(effective, "u2").HasFlag(ContextCheckpointKind.ReasoningCompaction));
	}

	[Fact]
	public void DisabledFlags_MaskOutBits()
	{
		var u0 = PromptingTestHelpers.User("u0", 0);
		var u1 = PromptingTestHelpers.User("u1", 1);
		var chat = PromptingTestHelpers.CreateChat(u0, u1);
		PromptingTestHelpers.AddCheckpoint(u0,
			ContextCheckpointKind.ToolCompaction | ContextCheckpointKind.ReasoningCompaction);

		var provider = CreateProvider(chat, out _);
		var agent = PromptingTestHelpers.CreateAgent();
		agent.Context.DisabledFlags = ContextCheckpointKind.ToolCompaction;

		var effective = provider.GetEffectiveMessages(agent);

		var kind = GetCheckpointKind(effective, "u0");
		Assert.False(kind.HasFlag(ContextCheckpointKind.ToolCompaction));
		Assert.True(kind.HasFlag(ContextCheckpointKind.ReasoningCompaction));
	}

	[Fact]
	public void CutOnlyCheckpoint_NeverCovers()
	{
		var u0 = PromptingTestHelpers.User("u0", 0);
		var u1 = PromptingTestHelpers.User("u1", 1);
		var chat = PromptingTestHelpers.CreateChat(u0, u1);
		PromptingTestHelpers.AddCheckpoint(u0, ContextCheckpointKind.Shield | ContextCheckpointKind.Summary);

		var provider = CreateProvider(chat, out _);
		var agent = PromptingTestHelpers.CreateAgent();

		var effective = provider.GetEffectiveMessages(agent);

		Assert.All(effective.Messages, m => Assert.False(HasCompaction(m.AffectedCheckpoints)));
	}

	// ==================== Compaction effects (quote renderer) ====================

	private static ChatMessageQuoteRenderer CreateRenderer()
	{
		var template = new FakeTextTemplate(context => JsonSerializer.Serialize(context));
		return new ChatMessageQuoteRenderer(
			new FakeTemplateLibraryAccessor(template),
			new FakeUserManagementService(),
			new FakeAgentManagementService(),
			[],
			[],
			[]);
	}

	private static string RenderToolResultMessage(ChatMessageQuoteRenderer renderer, bool canBeCompacted,
		ContextCheckpointKind appliedCheckpoints, ToolStatus status = ToolStatus.Success)
	{
		var message = PromptingTestHelpers.Assistant("assistant output", 1);
		((AssistantMessage)message.Message).ToolCalls.Add(new ToolCall
		{
			ToolName = "test-tool",
			ToolCallId = "tc-1",
			CompletionToken = new CompletionSource().Token,
			Arguments = "{}",
			ResultContent = "RAW TOOL RESULT",
			Status = status,
			CanBeCompacted = canBeCompacted,
		});

		return renderer.Render(message, MessagePartsFacet.ToolCallResults,
			MessageAuthorIdentity.Default, appliedCheckpoints).Content;
	}

	private static string RenderReasoningMessage(ChatMessageQuoteRenderer renderer, ContextCheckpointKind appliedCheckpoints)
	{
		var message = PromptingTestHelpers.Assistant("assistant output", 1);
		((AssistantMessage)message.Message).ReasoningContent = "SECRET REASONING";

		return renderer.Render(message, MessagePartsFacet.Reasoning,
			MessageAuthorIdentity.Default, appliedCheckpoints).Content;
	}

	[Fact]
	public void ToolCompaction_RespectsCanBeCompacted()
	{
		var renderer = CreateRenderer();

		var compactable = RenderToolResultMessage(renderer, canBeCompacted: true, ContextCheckpointKind.ToolCompaction);
		var rigid = RenderToolResultMessage(renderer, canBeCompacted: false, ContextCheckpointKind.ToolCompaction);

		Assert.Contains("COMPACTED", compactable);
		Assert.Contains("RAW TOOL RESULT", rigid);
	}

	[Fact]
	public void ForcedToolCompaction_IgnoresCanBeCompacted()
	{
		var renderer = CreateRenderer();

		var rendered = RenderToolResultMessage(renderer, canBeCompacted: false,
			ContextCheckpointKind.ForcedToolCompaction);

		Assert.Contains("COMPACTED", rendered);
	}

	[Fact]
	public void ReasoningCompaction_RemovesReasoningFromRenderedMessage()
	{
		var renderer = CreateRenderer();

		var compacted = RenderReasoningMessage(renderer, ContextCheckpointKind.ReasoningCompaction);
		var intact = RenderReasoningMessage(renderer, ContextCheckpointKind.None);

		Assert.DoesNotContain("SECRET REASONING", compacted);
		Assert.Contains("SECRET REASONING", intact);
	}

	[Fact]
	public void Placeholders_AreStatusAware()
	{
		var renderer = CreateRenderer();

		Assert.Contains("SUCCESSFUL", RenderToolResultMessage(renderer, true,
			ContextCheckpointKind.ToolCompaction, ToolStatus.Success));
		Assert.Contains("FAULTED", RenderToolResultMessage(renderer, true,
			ContextCheckpointKind.ToolCompaction, ToolStatus.Error));
		Assert.Contains("CANCELLED", RenderToolResultMessage(renderer, true,
			ContextCheckpointKind.ToolCompaction, ToolStatus.Cancelled));
	}

	// ==================== Fakes ====================

	private sealed class FakeTextTemplate(Func<object?, string> render) : ITextTemplate
	{
		public IMetadataCollection Metadata => throw new NotSupportedException();

		public string Render(object? context = null, TemplateFunctionSet? functions = null) => render(context);

		object ITemplate.Render(object? context, TemplateFunctionSet? functions) => Render(context, functions);
	}

	private sealed class FakeTemplateLibraryAccessor(ITextTemplate template) : ITemplateLibraryAccessor
	{
		public ITemplate GetTemplate(string id, params IMetadata[] metadata) => template;

		public IMessagesTemplate GetMessagesTemplate(string id, params IMetadata[] metadata)
			=> throw new NotSupportedException();

		public ITextTemplate GetTextTemplate(string id, params IMetadata[] metadata) => template;
	}

	private sealed class FakeUserManagementService : IUserManagementService
	{
		public IEnumerable<UserInformation> GetAllUsers() => [];
		public IEnumerable<UserInformation> GetLocalUsers() => [];
		public IEnumerable<UserInformation> GetRemoteUsers() => [];
		public bool IsLocalUser(string userLogin) => false;
		public IEnumerable<UserInformation> GetActiveUsers() => [];
		public UserInformation? FindByLogin(string login) => null;
		public UserInformation? RegisterUser(string login, string password, string? name, string? description) => null;
	}

	private sealed class FakeAgentManagementService : IAgentManagementService
	{
		public IEnumerable<Guid> ListAgentIds() => [];
		public IEnumerable<(ChatAgentDescriptor Agent, bool IsGlobal)> ListAgents() => [];
		public ChatAgentDescriptor GetAgentDescriptor(Guid agentId) => new();
		public ChatAgentDescriptor? TryGetAgentDescriptor(Guid agentId) => new();
	}
}
