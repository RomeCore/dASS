using System.Collections.Immutable;
using LLMDesktopAssistant.Addons;
using LLMDesktopAssistant.Agents;
using LLMDesktopAssistant.LLM.Domain;
using LLMDesktopAssistant.LLM.Services.Prompting;
using LLMDesktopAssistant.LLM.Services.Tools;
using LLMDesktopAssistant.Prompting;
using LLMDesktopAssistant.Prompting.Context;
using LLMDesktopAssistant.Prompting.Hooks;
using LLMDesktopAssistant.Tests.Storage;
using LLMDesktopAssistant.Tools;
using RCLargeLanguageModels.Tasks;
using RCMessages = RCLargeLanguageModels.Messages;
using RCTools = RCLargeLanguageModels.Tools;

namespace LLMDesktopAssistant.Tests.Prompting;

/// <summary>
/// Builds an <see cref="AgentPromptComposer"/> on top of fakes, so the composed prompt can be asserted end to end
/// without the chat pipeline, a database or a real model. The effective-messages provider, the anchor processor and
/// the visibility service are the real implementations (driven by the test chat); everything else is a fake.
/// </summary>
internal sealed class PromptComposerHarness
{
	private readonly FakeChatSettingsService _chatSettings = new();
	private readonly FakeMessageVisibilityService _visibility = new();
	private readonly List<IPromptContextProvider> _providers = [];
	private readonly List<IPromptBuildingHook> _hooks = [];

	public Chat Chat { get; }
	public FakeChatSettingsService ChatSettings => _chatSettings;
	public RecordingToolsetCache ToolsetCache { get; } = new();
	public RecordingPromptDumpService Dump { get; } = new();

	public PromptComposerHarness(params BranchedMessage[] messages)
	{
		Chat = PromptingTestHelpers.CreateChat(messages);
	}

	public PromptComposerHarness WithSection(FakeSection section)
	{
		_providers.Add(section);
		return this;
	}

	public PromptComposerHarness WithLiveTail(string text)
	{
		_providers.Add(new FakeLiveTailProvider(text));
		return this;
	}

	public PromptComposerHarness WithSupersede(string discriminator, string text)
	{
		_providers.Add(new FakeSupersedeProvider(discriminator, text));
		return this;
	}

	public PromptComposerHarness WithProvider(IPromptContextProvider provider)
	{
		_providers.Add(provider);
		return this;
	}

	public PromptComposerHarness WithHook(IPromptBuildingHook hook)
	{
		_hooks.Add(hook);
		return this;
	}

	public AgentPromptComposer CreateComposer() => new(
		Chat,
		_chatSettings,
		new FakeQuoteRenderer(),
		new AgentEffectiveMessagesProvider(Chat, _chatSettings, _visibility),
		_hooks,
		ToolsetCache,
		new PromptAnchoredSectionProcessor(Chat, _chatSettings),
		new FakeSupersedeProcessor(),
		new FakePromptContextCollector(_providers),
		Dump);

	private sealed class FakeQuoteRenderer : IChatMessageQuoteRenderer
	{
		public MessageRenderingResult Render(BranchedMessage message,
			MessagePartsFacet parts = MessagePartsFacet.Default,
			MessageAuthorIdentity identity = MessageAuthorIdentity.Default,
			ContextCheckpointKind appliedCheckpoints = ContextCheckpointKind.None)
			=> new($"QUOTE({message.Message.Content})", []);
	}

	private sealed class FakeSupersedeProcessor : IPromptSupersedeContextProcessor
	{
		public void Process(ChatAgentDescriptor agent, EffectiveChatContext effectiveContext,
			IEnumerable<IPromptSupersedeContextProvider> providers)
		{
		}
	}

	private sealed class FakeLiveTailProvider(string text) : IPromptLiveTailContextProvider
	{
		public string Provide(EffectiveChatContext context) => text;
	}

	private sealed class FakeSupersedeProvider(string discriminator, string text) : IPromptSupersedeContextProvider
	{
		public string Discriminator => discriminator;

		public PromptSupersedeStampBase? GetNewStamp(PromptSupersedeStampBase? previousStamp, EffectiveChatContext context)
			=> null;

		public string Render(PromptSupersedeStampBase stamp) => text;
	}

	private sealed class FakePromptContextCollector(IEnumerable<IPromptContextProvider> providers)
		: IAddonSetCollector<PromptContextInfo>
	{
		private readonly List<PromptContextInfo> _infos = [.. providers.Select(p => new PromptContextInfo { Provider = p })];

		public IEnumerable<PromptContextInfo> GetAvailableAddons() => _infos;

		public IEnumerable<PromptContextInfo> GetAddonsForChat() => _infos;

		public IEnumerable<PromptContextInfo> GetAddonsForAgent(ChatAgentDescriptor agent) => _infos;
	}

	internal sealed class RecordingToolsetCache : IToolsetCacheService
	{
		public ImmutableDictionary<string, ToolInfo> AvailableTools { get; } = ImmutableDictionary<string, ToolInfo>.Empty;

		public ImmutableDictionary<string, ToolInfo> AliasedTools { get; } = ImmutableDictionary<string, ToolInfo>.Empty;

		public ImmutableDictionary<string, ToolInfo> ValidTools { get; } = ImmutableDictionary<string, ToolInfo>.Empty;

		public ImmutableDictionary<string, ToolInfo> ValidAliasedTools { get; } = ImmutableDictionary<string, ToolInfo>.Empty;

		public int InvalidateCount { get; private set; }

		public void Invalidate(ChatAgentDescriptor agent) => InvalidateCount++;
	}

	internal sealed class RecordingPromptDumpService : IPromptDumpService
	{
		public List<(IReadOnlyList<RCMessages.IMessage> Messages, IReadOnlyList<RCTools.ITool> Tools, string? Context)> Dumps { get; } = [];

		public void Dump(IEnumerable<RCMessages.IMessage> messages, IEnumerable<RCTools.ITool> tools, string? scmContext = null)
			=> Dumps.Add((messages.ToList(), tools.ToList(), scmContext));
	}
}
