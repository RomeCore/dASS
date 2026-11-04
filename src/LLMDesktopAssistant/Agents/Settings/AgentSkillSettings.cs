using LLMDesktopAssistant.Addons;
using LLMDesktopAssistant.Agents.Settings;
using LLMDesktopAssistant.SourceGenerators;

namespace LLMDesktopAssistant.Agents
{
	/// <summary>
	/// Describes the agent-level skills settings: the local enable flag and the inheritable
	/// list of per-skill changes.
	/// </summary>
	[SettingsRoute(nameof(ChatAgentDescriptor.Skills))]
	public partial class AgentSkillSettings : AgentSettingsCategoryBase, IAddonCategorySettings
	{
		/// <summary>
		/// Whether this category contributes skills at all.
		/// </summary>
		public bool AddonsCategoryEnabled
		{
			get;
			set => SetProperty(ref field, value);
		} = true;

		/// <summary>
		/// Gets or sets the skillset settings for the agent.
		/// </summary>
		[InheritedChatAgentSetting]
		public SkillsetConfiguration Skillset
		{
			get => field ??= new();
			set => SetProperty(ref field, value);
		}
	}
}
