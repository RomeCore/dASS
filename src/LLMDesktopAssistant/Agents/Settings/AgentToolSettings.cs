using LLMDesktopAssistant.Addons;
using LLMDesktopAssistant.Agents.Settings;
using LLMDesktopAssistant.LLM.Settings;
using LLMDesktopAssistant.SourceGenerators;
using LLMDesktopAssistant.Tools;

namespace LLMDesktopAssistant.Agents
{
	/// <summary>
	/// Describes an agent tool settings.
	/// Contains the tool policy group, the toolset group and the local tools enable flag.
	/// </summary>
	[SettingsRoute(nameof(ChatAgentDescriptor.Tools))]
	public partial class AgentToolSettings : AgentSettingsCategoryBase, IAddonCategorySettings
	{
		/// <summary>
		/// Whether this category contributes tools at all.
		/// </summary>
		public bool AddonsCategoryEnabled
		{
			get;
			set => SetProperty(ref field, value);
		} = true;

		private ToolPolicyMask _policy;
		/// <summary>
		/// Gets or sets the tool behaviour policy for this agent.
		/// Inherits the chat profile policy by default.
		/// </summary>
		[InheritedChatAgentSetting(DefaultLevel = ChatSettingsInheritanceLevel.Profile)]
		public ToolPolicyMask Policy
		{
			get => _policy;
			set => SetProperty(ref _policy, value);
		}

		private ToolsetSettings _toolset = new();
		/// <summary>
		/// Gets or sets the toolset configuration for this agent: a custom toolset or a
		/// reference to a shared toolset configuration.
		/// </summary>
		[InheritedChatAgentSetting]
		public ToolsetSettings Toolset
		{
			get => _toolset;
			set => SetProperty(ref _toolset, value);
		}
	}
}
