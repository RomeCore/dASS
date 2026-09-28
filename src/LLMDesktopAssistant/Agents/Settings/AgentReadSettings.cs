using LLMDesktopAssistant.Agents.Settings;
using LLMDesktopAssistant.LLM.Services.Prompting;
using LLMDesktopAssistant.SourceGenerators;
using LLMDesktopAssistant.Utils;

namespace LLMDesktopAssistant.Agents
{
	/// <summary>
	/// Describes an agent's reading settings: the inheritable read permissions, exposure mode
	/// and context group, and the local agent ID filter.
	/// </summary>
	[SettingsRoute(nameof(ChatAgentDescriptor.Read))]
	public partial class AgentReadSettings : AgentSettingsCategoryBase
	{
		/// <summary>
		/// The final read permissions that determine what the agent can read.
		/// Contains the facets of the messages and their parts and author's identity.
		/// Applies with the AND operator.
		/// </summary>
		[InheritedChatAgentSetting]
		public AgentReadDefaultRows ReadFilters
		{
			get;
			set => SetProperty(ref field, value);
		} = new()
		{
			User = new()
			{
				VisibleParts = MessagePartsFacet.Content | MessagePartsFacet.NativeAttachments
					| MessagePartsFacet.Attachments | MessagePartsFacet.ToolCallFacts
			}
		};

		/// <summary>
		/// The permissions that determine what the agent can read.
		/// </summary>
		[InheritedChatAgentSetting]
		public AgentReadDefaultRows DefaultShareFilters
		{
			get;
			set => SetProperty(ref field, value);
		} = new();

		/// <summary>
		/// The per-participant share filters, where key is the user login or agent ID.
		/// </summary>
		public ObservableDictionary<Guid, AgentReadOverrideRow> ParticipantsShareFilters
		{
			get => field ??= [];
			set => (field ??= []).Reset(value);
		}
	}
}
