using LLMDesktopAssistant.Addons;
using LLMDesktopAssistant.Agents;
using LLMDesktopAssistant.Agents.SubAgents;
using LLMDesktopAssistant.LLM.Services;
using LLMDesktopAssistant.LLM.Services.Prompting;
using LLMDesktopAssistant.Prompting.Context.AddonItems;

namespace LLMDesktopAssistant.Prompting.Context.Providers.SubAgents
{
	/// <summary>
	/// The serializable snapshot of a sub-agent addon tracked by the sub-agents section.
	/// </summary>
	public class SubAgentItem : AddonSectionItem
	{
	}

	/// <summary>
	/// The field-level description of a sub-agent change (only the common description field
	/// currently participates).
	/// </summary>
	public class SubAgentChange : AddonItemChange<SubAgentItem>
	{
	}

	/// <summary>
	/// The state of the sub-agents section: the visible sub-agents and the names of the hidden sub-agents.
	/// </summary>
	public class SubAgentsSectionState : AddonSectionState<SubAgentItem>
	{
	}

	/// <summary>
	/// The delta of the sub-agents section (see <see cref="AddonSectionDelta{TItem, TChange}"/>).
	/// </summary>
	public class SubAgentsSectionDelta : AddonSectionDelta<SubAgentItem, SubAgentChange>
	{
	}

	/// <summary>
	/// Captures the sub-agents section state by collecting the sub-agents of the agent.
	/// The hidden names are captured in the hybrid mode only.
	/// </summary>
	[ChatService(typeof(IPromptSectionStateProvider<SubAgentsSectionState>))]
	public class SubAgentsStateProvider(
		IAddonSetCollector<SubAgentInfo> subAgentsetCollector
		) : IPromptSectionStateProvider<SubAgentsSectionState>
	{
		/// <inheritdoc/>
		public SubAgentsSectionState CaptureState(ChatAgentDescriptor agent)
		{
			var items = new List<SubAgentItem>();
			var hiddenNames = new List<string>();

			bool captureHiddenSubAgents = agent.Context.PromptMode == PromptContextMode.Hybrid;

			foreach (var subAgent in subAgentsetCollector.GetAddonsForAgent(agent))
			{
				if (subAgent.Hidden ?? false)
				{
					if (captureHiddenSubAgents)
						hiddenNames.Add(subAgent.Name);
				}
				else
				{
					items.Add(new SubAgentItem
					{
						Name = subAgent.Name,
						Description = subAgent.Description
					});
				}
			}

			return new SubAgentsSectionState
			{
				Items = [.. items.OrderBy(s => s.Name, StringComparer.Ordinal)],
				HiddenNames = [.. hiddenNames.OrderBy(n => n, StringComparer.Ordinal)]
			};
		}
	}

	/// <summary>
	/// Renders the sub-agents section state into a snapshot (text only).
	/// </summary>
	[ChatService(typeof(IPromptSectionStateRenderer<SubAgentsSectionState>))]
	public class SubAgentsStateRenderer(
		ITemplateLibraryAccessor templates
	) : IPromptSectionStateRenderer<SubAgentsSectionState>
	{
		/// <inheritdoc/>
		public SystemPromptSnapshot Render(SubAgentsSectionState state)
		{
			return templates.GetTextTemplate("sub_agents_system_section").Render(new
			{
				sub_agents = state.Items.Count > 0
					? state.Items.Select(s => new
					{
						name = s.Name,
						description = s.Description
					}).ToArray()
					: null,
				hidden_sub_agents = state.HiddenNames.Count > 0 ? state.HiddenNames.ToArray() : null
			});
		}
	}

	/// <summary>
	/// Delta provider of the sub-agents section: the common addon delta engine plus the
	/// description comparison.
	/// </summary>
	[ChatService(typeof(IPromptSectionDeltaProvider<SubAgentsSectionState, SubAgentsSectionDelta>))]
	public class SubAgentsDeltaProvider(
		IPromptSectionStateProvider<SubAgentsSectionState> stateProvider
	) : AddonSectionDeltaProvider<SubAgentsSectionState, SubAgentItem, SubAgentChange, SubAgentsSectionDelta>(stateProvider)
	{
		/// <inheritdoc/>
		protected override SubAgentChange? Diff(SubAgentItem known, SubAgentItem current)
		{
			bool descriptionChanged = known.Description != current.Description;
			if (!descriptionChanged)
				return null;

			return new SubAgentChange
			{
				DescriptionChanged = true,
				NewDescription = current.Description
			};
		}
	}

	/// <summary>
	/// Delta renderer of the sub-agents section: projects the transitions into the
	/// 'sub_agents_system_section_delta' template.
	/// </summary>
	[ChatService(typeof(IPromptSectionDeltaRenderer<SubAgentsSectionDelta>))]
	public class SubAgentsDeltaRenderer(
		ITemplateLibraryAccessor templates
	) : AddonSectionDeltaRenderer<SubAgentItem, SubAgentChange, SubAgentsSectionDelta>(templates)
	{
		/// <inheritdoc/>
		protected override string TemplateId => "sub_agents_system_section_delta";

		/// <inheritdoc/>
		protected override object ProjectAddition(AddonItemAddition<SubAgentItem, SubAgentChange> addition) => new
		{
			name = addition.Name,
			hidden = addition.Hidden,
			has_definition = addition.Definition is not null,
			without_definition = !addition.Hidden && addition.Definition is null,
			definition_name = addition.Definition?.Name,
			definition_description = addition.Definition?.Description,
			description_changed = addition.Changes?.DescriptionChanged ?? false,
			new_description = addition.Changes?.NewDescription
		};

		/// <inheritdoc/>
		protected override object ProjectRemoval(AddonItemRemoval removal) => new
		{
			name = removal.Name
		};

		/// <inheritdoc/>
		protected override object ProjectBecameVisible(AddonItemBecameVisible<SubAgentItem, SubAgentChange> becameVisible) => new
		{
			name = becameVisible.Name,
			has_definition = becameVisible.Definition is not null,
			without_definition = becameVisible.Definition is null,
			definition_name = becameVisible.Definition?.Name,
			definition_description = becameVisible.Definition?.Description,
			description_changed = becameVisible.Changes?.DescriptionChanged ?? false,
			new_description = becameVisible.Changes?.NewDescription
		};

		/// <inheritdoc/>
		protected override object ProjectUpdate(AddonItemUpdate<SubAgentChange> update) => new
		{
			name = update.Name,
			description_changed = update.Changes?.DescriptionChanged ?? false,
			new_description = update.Changes?.NewDescription
		};
	}

	/// <summary>
	/// The sub-agents section: provides the available sub-agents for the system prompt.
	/// </summary>
	public class SubAgentsSection(IServiceProvider services)
		: PromptAnchoredSectionBase<SubAgentsSectionState, SubAgentsSectionDelta>(services)
	{
		public override string Discriminator => "sub-agents";
	}

	[ChatService(typeof(PromptContextNativeProvider))]
	public class SubAgentsSectionProvider : PromptContextNativeProvider
	{
		public SubAgentsSectionProvider(IServiceProvider services)
		{
			AddContext(new PromptContextInfo
			{
				Name = "sub-agents",
				Order = 30,
				Description = string.Empty,
				IsFixed = true,
				Provider = new SubAgentsSection(services)
			});
		}
	}
}
