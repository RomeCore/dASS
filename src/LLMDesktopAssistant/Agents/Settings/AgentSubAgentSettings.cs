using LLMDesktopAssistant.Addons;
using LLMDesktopAssistant.Agents.Settings;
using LLMDesktopAssistant.SourceGenerators;

namespace LLMDesktopAssistant.Agents
{
	/// <summary>
	/// Describes the agent-level sub-agent settings: the local enable flag and the inheritable
	/// list of per-sub-agent changes.
	/// </summary>
	[SettingsRoute(nameof(ChatAgentDescriptor.SubAgents))]
	public partial class AgentSubAgentSettings : AgentSettingsCategoryBase, IAddonCategorySettings
	{
		/// <summary>
		/// Whether this category contributes sub-agents at all.
		/// </summary>
		public bool AddonsCategoryEnabled
		{
			get;
			set => SetProperty(ref field, value);
		} = true;

		private SubAgentsetConfiguration _subAgentset = new();
		/// <summary>
		/// Gets or sets the sub-agentset settings.
		/// </summary>
		[InheritedChatAgentSetting]
		public SubAgentsetConfiguration SubAgentset
		{
			get => _subAgentset;
			set => SetProperty(ref _subAgentset, value);
		}
	}
}
