using LLMDesktopAssistant.SourceGenerators;
using LLMDesktopAssistant.Tools;

namespace LLMDesktopAssistant.LLM.Settings
{
	/// <summary>
	/// Chat-level sub-agent settings.
	/// The sub-agent addon sources are configured by the shared addons settings
	/// (<see cref="ChatAddonSettings"/>, the 'agents' addon type).
	/// </summary>
	[SettingsRoute(nameof(ChatSettings.SubAgents))]
	public partial class ChatSubAgentSettings : ChatSettingsCategoryBase
	{
		private ToolPolicyMask _policy;
		/// <summary>
		/// Gets or sets the tool behaviour policy applied when the chat calls sub-agents
		/// (both the <c>agent-callsub</c> tool and slash commands).
		/// Inherits the application policy by default.
		/// </summary>
		[InheritedChatSetting]
		public ToolPolicyMask Policy
		{
			get => _policy;
			set => SetProperty(ref _policy, value);
		}
	}
}
