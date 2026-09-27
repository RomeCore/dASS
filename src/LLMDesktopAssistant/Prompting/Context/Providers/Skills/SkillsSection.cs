using LLMDesktopAssistant.Addons;
using LLMDesktopAssistant.Agents;
using LLMDesktopAssistant.LLM.Services;
using LLMDesktopAssistant.LLM.Services.Prompting;
using LLMDesktopAssistant.Prompting.Context.AddonItems;
using LLMDesktopAssistant.Prompting.Skills;

namespace LLMDesktopAssistant.Prompting.Context.Providers.Skills
{
	/// <summary>
	/// The serializable snapshot of a skill addon tracked by the skills section.
	/// </summary>
	public class SkillItem : AddonSectionItem
	{
		/// <summary>
		/// The path to the skill file (the location hint).
		/// </summary>
		public string? Path { get; set; }
	}

	/// <summary>
	/// The field-level description of a skill change: the common description/body fields live
	/// in the base, the path is skill-specific.
	/// </summary>
	public class SkillChange : AddonItemChange<SkillItem>
	{
		/// <summary>
		/// Whether the skill path has changed.
		/// </summary>
		public bool PathChanged { get; set; }

		/// <summary>
		/// The new skill path (when <see cref="PathChanged"/> is true).
		/// </summary>
		public string? NewPath { get; set; }

		protected override void ApplyExtra(SkillItem item)
		{
			if (PathChanged)
				item.Path = NewPath;
		}
	}

	/// <summary>
	/// The state of the skills section: the visible skills and the names of the hidden skills.
	/// </summary>
	public class SkillsSectionState : AddonSectionState<SkillItem>
	{
	}

	/// <summary>
	/// The delta of the skills section (see <see cref="AddonSectionDelta{TItem, TChange}"/>).
	/// </summary>
	public class SkillsSectionDelta : AddonSectionDelta<SkillItem, SkillChange>
	{
	}

	/// <summary>
	/// Captures the skills section state by collecting the skills of the agent.
	/// The hidden names are captured in the hybrid mode only.
	/// </summary>
	[ChatService(typeof(IPromptSectionStateProvider<SkillsSectionState>))]
	public class SkillsStateProvider(
		IAddonSetCollector<SkillInfo> skillsetBuilder
		) : IPromptSectionStateProvider<SkillsSectionState>
	{
		/// <inheritdoc/>
		public SkillsSectionState CaptureState(ChatAgentDescriptor agent)
		{
			var items = new List<SkillItem>();
			var hiddenNames = new List<string>();

			bool captureHiddenSkills = agent.Context.PromptMode == PromptContextMode.Hybrid;

			foreach (var skill in skillsetBuilder.GetAddonsForAgent(agent))
			{
				if (skill.Hidden ?? false)
				{
					if (captureHiddenSkills)
						hiddenNames.Add(skill.Name);
				}
				else
				{
					items.Add(new SkillItem
					{
						Name = skill.Name,
						Description = skill.Description,
						Path = skill.Path,
						Body = skill.InjectionMode is SkillInjectionMode.Full ? skill.BodyGetter(skill) : null
					});
				}
			}

			return new SkillsSectionState
			{
				Items = [.. items.OrderBy(s => s.Name, StringComparer.Ordinal)],
				HiddenNames = [.. hiddenNames.OrderBy(n => n, StringComparer.Ordinal)]
			};
		}
	}

	/// <summary>
	/// Renders the skills section state into a snapshot (text only).
	/// </summary>
	[ChatService(typeof(IPromptSectionStateRenderer<SkillsSectionState>))]
	public class SkillsStateRenderer(
		ITemplateLibraryAccessor templates
	) : IPromptSectionStateRenderer<SkillsSectionState>
	{
		/// <inheritdoc/>
		public SystemPromptSnapshot Render(SkillsSectionState state)
		{
			return templates.GetTextTemplate("skills_system_section").Render(new
			{
				skills = state.Items.Count > 0
					? state.Items.Select(s => new
					{
						name = s.Name,
						description = s.Description,
						path = s.Path,
						body = s.Body
					}).ToArray()
					: null,
				hidden_skills = state.HiddenNames.Count > 0 ? state.HiddenNames.ToArray() : null
			});
		}
	}

	/// <summary>
	/// Delta provider of the skills section: the common addon delta engine plus the skill
	/// field-level comparison (description, path and body).
	/// </summary>
	[ChatService(typeof(IPromptSectionDeltaProvider<SkillsSectionState, SkillsSectionDelta>))]
	public class SkillsDeltaProvider(
		IPromptSectionStateProvider<SkillsSectionState> stateProvider
	) : AddonSectionDeltaProvider<SkillsSectionState, SkillItem, SkillChange, SkillsSectionDelta>(stateProvider)
	{
		/// <inheritdoc/>
		protected override SkillChange? Diff(SkillItem known, SkillItem current)
		{
			bool descriptionChanged = known.Description != current.Description;
			bool pathChanged = known.Path != current.Path;
			bool bodyChanged = known.Body != current.Body;
			if (!descriptionChanged && !pathChanged && !bodyChanged)
				return null;

			return new SkillChange
			{
				DescriptionChanged = descriptionChanged,
				NewDescription = descriptionChanged ? current.Description : null,
				PathChanged = pathChanged,
				NewPath = pathChanged ? current.Path : null,
				BodyChanged = bodyChanged,
				NewBody = bodyChanged ? current.Body : null
			};
		}
	}

	/// <summary>
	/// Delta renderer of the skills section: projects the transitions into the
	/// 'skills_system_section_delta' template.
	/// </summary>
	[ChatService(typeof(IPromptSectionDeltaRenderer<SkillsSectionDelta>))]
	public class SkillsDeltaRenderer(
		ITemplateLibraryAccessor templates
	) : AddonSectionDeltaRenderer<SkillItem, SkillChange, SkillsSectionDelta>(templates)
	{
		/// <inheritdoc/>
		protected override string TemplateId => "skills_system_section_delta";

		/// <inheritdoc/>
		protected override object ProjectAddition(AddonItemAddition<SkillItem, SkillChange> addition) => new
		{
			name = addition.Name,
			hidden = addition.Hidden,
			has_definition = addition.Definition is not null,
			without_definition = !addition.Hidden && addition.Definition is null,
			definition_name = addition.Definition?.Name,
			definition_description = addition.Definition?.Description,
			definition_path = addition.Definition?.Path,
			definition_body = addition.Definition?.Body,
			description_changed = addition.Changes?.DescriptionChanged ?? false,
			new_description = addition.Changes?.NewDescription,
			path_changed = addition.Changes?.PathChanged ?? false,
			new_path = addition.Changes?.NewPath,
			body_changed = addition.Changes?.BodyChanged ?? false,
			new_body = addition.Changes?.NewBody
		};

		/// <inheritdoc/>
		protected override object ProjectRemoval(AddonItemRemoval removal) => new
		{
			name = removal.Name
		};

		/// <inheritdoc/>
		protected override object ProjectBecameVisible(AddonItemBecameVisible<SkillItem, SkillChange> becameVisible) => new
		{
			name = becameVisible.Name,
			has_definition = becameVisible.Definition is not null,
			without_definition = becameVisible.Definition is null,
			definition_name = becameVisible.Definition?.Name,
			definition_description = becameVisible.Definition?.Description,
			definition_path = becameVisible.Definition?.Path,
			definition_body = becameVisible.Definition?.Body,
			description_changed = becameVisible.Changes?.DescriptionChanged ?? false,
			new_description = becameVisible.Changes?.NewDescription,
			path_changed = becameVisible.Changes?.PathChanged ?? false,
			new_path = becameVisible.Changes?.NewPath,
			body_changed = becameVisible.Changes?.BodyChanged ?? false,
			new_body = becameVisible.Changes?.NewBody
		};

		/// <inheritdoc/>
		protected override object ProjectUpdate(AddonItemUpdate<SkillChange> update) => new
		{
			name = update.Name,
			description_changed = update.Changes?.DescriptionChanged ?? false,
			new_description = update.Changes?.NewDescription,
			path_changed = update.Changes?.PathChanged ?? false,
			new_path = update.Changes?.NewPath,
			body_changed = update.Changes?.BodyChanged ?? false,
			new_body = update.Changes?.NewBody
		};
	}

	/// <summary>
	/// The skills section: provides the available skills for the system prompt.
	/// </summary>
	public class SkillsSection(IServiceProvider services)
		: PromptAnchoredSectionBase<SkillsSectionState, SkillsSectionDelta>(services)
	{
		public override string Discriminator => "skills";
	}

	[ChatService(typeof(PromptContextNativeProvider))]
	public class SkillsSectionProvider : PromptContextNativeProvider
	{
		public SkillsSectionProvider(IServiceProvider services)
		{
			AddContext(new PromptContextInfo
			{
				Name = "skills",
				Order = 20,
				Description = string.Empty,
				IsFixed = true,
				Provider = new SkillsSection(services)
			});
		}
	}
}
