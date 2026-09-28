using LLMDesktopAssistant.LLM.Services.Prompting;
using LLMDesktopAssistant.Prompting;
using LLMDesktopAssistant.SourceGenerators;

namespace LLMDesktopAssistant.Agents.Settings
{
	/// <summary>
	/// Agent context settings: visible rounds, disabled checkpoint kinds and prompt context mode.
	/// </summary>
	[SettingsRoute(nameof(ChatAgentDescriptor.Context))]
	public partial class AgentContextSettings : AgentSettingsCategoryBase
	{
		/// <summary>
		/// The prompt context mode that defines how the system prompt is assembled and cached.
		/// </summary>
		public PromptContextMode PromptMode
		{
			get => field;
			set => SetProperty(ref field, value);
		} = PromptContextMode.Hybrid;

		/// <summary>
		/// The maximum number of rounds that the agent can see in its context. If zero, there is no limit.
		/// </summary>
		[InheritedChatAgentSetting]
		public int MaxVisibleRounds
		{
			get => field;
			set => SetProperty(ref field, value);
		} = 0;

		/// <summary>
		/// Checkpoint kinds that are disabled (ignored) for this agent.
		/// </summary>
		[InheritedChatAgentSetting]
		public ContextCheckpointKind DisabledFlags
		{
			get => field;
			set => SetProperty(ref field, value);
		} = ContextCheckpointKind.None;

		/// <summary>
		/// The frozen system prompt snapshot (static prompt mode). Not inherited: per-agent only.
		/// </summary>
		public SystemPromptSnapshot? Snapshot
		{
			get => field;
			set => SetProperty(ref field, value);
		}

		/// <summary>
		/// Gets or sets the context-set settings for the agent.
		/// </summary>
		[InheritedChatAgentSetting]
		public ContextSetSettings ContextSet
		{
			get => field ??= new();
			set => SetProperty(ref field, value);
		}
	}
}
