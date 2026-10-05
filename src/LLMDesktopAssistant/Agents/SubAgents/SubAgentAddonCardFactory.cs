using Avalonia.Media;
using LLMDesktopAssistant.Addons;
using LLMDesktopAssistant.Addons.MVVM;
using LLMDesktopAssistant.Addons.MVVM.Elements;
using LLMDesktopAssistant.Agents.Memory;
using LLMDesktopAssistant.Localization;
using LLMDesktopAssistant.Prompting.Skills;
using LLMDesktopAssistant.Services;
using LLMDesktopAssistant.Services.Instances;
using LLMDesktopAssistant.Settings;
using LLMDesktopAssistant.Tools;
using Material.Icons;

namespace LLMDesktopAssistant.Agents.SubAgents
{
	/// <summary>
	/// Builds the addon cards of the sub-agent addon type ('agents').
	/// </summary>
	/// <remarks>
	/// On top of the elements produced by <see cref="AddonCardFactoryBase{TAddon, TChange}"/> a sub-agent card
	/// shows the model override, the counts of the resources the sub-agent uses, the broken references of the
	/// sub-agent and the lists of those resources.
	/// </remarks>
	[Service(typeof(IAddonCardFactory<SubAgentInfo, SubAgentChange>))]
	public class SubAgentAddonCardFactory : AddonCardFactoryBase<SubAgentInfo, SubAgentChange>
	{
		private readonly IAddonAccessor<SkillInfo> _skills;
		private readonly IAddonAccessor<SubAgentInfo> _subAgents;

		/// <summary>
		/// Initializes a new instance of the <see cref="SubAgentAddonCardFactory"/> class.
		/// </summary>
		/// <param name="explorerOpener">The service used by the "show in explorer" action.</param>
		/// <param name="toastService">The service used to report failures of the file actions.</param>
		/// <param name="skills">The accessor used to check the skills referenced by a sub-agent.</param>
		/// <param name="subAgents">The accessor used to check the sub-agents referenced by a sub-agent.</param>
		public SubAgentAddonCardFactory(IExplorerOpener explorerOpener, IToastService toastService,
			IAddonAccessor<SkillInfo> skills, IAddonAccessor<SubAgentInfo> subAgents)
			: base(explorerOpener, toastService)
		{
			_skills = skills;
			_subAgents = subAgents;
		}

		/// <inheritdoc/>
		protected override VisualIconKind TypeIcon => MaterialIconKind.RobotHappy;

		/// <inheritdoc/>
		protected override void AddHeaderElements(AddonCardContext<SubAgentInfo, SubAgentChange> context, List<IAddonCardElement> elements)
		{
			elements.Add(new AddonCardModelChange<SubAgentInfo, SubAgentChange>(
				context,
				getDefinitionModel: addon => addon.Model,
				getModelOverride: change => change.Model,
				setModelOverride: (change, model) => change.Model = model)
			{
				Order = HeaderOrder + 1,
				IsShownLeft = false
			});
		}

		/// <inheritdoc/>
		protected override void AddTypeChips(AddonCardContext<SubAgentInfo, SubAgentChange> context, List<IAddonCardElement> elements, int order)
		{
			var addon = context.Addon;

			AddCountChip(elements, order++, MaterialIconKind.Wrench, "card.sub_agents.tools.badge",
				addon.AllowedTools.Count + addon.AvailableTools.Count + addon.DisallowedTools.Count);
			AddCountChip(elements, order++, MaterialIconKind.Cards, "card.sub_agents.skills.badge", addon.Skills.Count);
			AddCountChip(elements, order++, MaterialIconKind.RobotHappy, "card.sub_agents.sub_agents.badge", addon.SubAgents.Count);
			AddCountChip(elements, order, MaterialIconKind.Database, "card.sub_agents.memory_blocks.badge", addon.MemoryBlocks.Count);

			AddLinkIssueChips(elements, addon, order);
		}

		/// <inheritdoc/>
		protected override void AddBlocks(AddonCardContext<SubAgentInfo, SubAgentChange> context, List<IAddonCardElement> elements)
		{
			var addon = context.Addon;

			AddParametersBlock(context, elements);

			var order = BlockOrder + 10;
			AddChipsBlock(elements, Locale.GetKey("card.sub_agents.tools.allowed"), MaterialIconKind.Check,
				FormatTools(addon.AllowedTools), order++);
			AddChipsBlock(elements, Locale.GetKey("card.sub_agents.tools.available"), MaterialIconKind.Wrench,
				FormatTools(addon.AvailableTools), order++);
			AddChipsBlock(elements, Locale.GetKey("card.sub_agents.tools.disallowed"), MaterialIconKind.Close,
				FormatTools(addon.DisallowedTools), order++);
			AddChipsBlock(elements, Locale.GetKey("card.sub_agents.skills"), MaterialIconKind.Cards, addon.Skills, order++);
			AddChipsBlock(elements, Locale.GetKey("card.sub_agents.sub_agents"), MaterialIconKind.RobotHappy, addon.SubAgents, order++);
			AddChipsBlock(elements, Locale.GetKey("card.sub_agents.memory_blocks"), MaterialIconKind.Database,
				addon.MemoryBlocks.Keys, order);

			AddTextBlock(elements, Locale.GetKey("card.body"), addon.BodyGetter(addon), BlockOrder + 20);
			AddMetadataBlock(context, elements, order: BlockOrder + 30);
		}

		/// <summary>
		/// Adds one chip per reference of the sub-agent that cannot be resolved in the current session.
		/// </summary>
		private void AddLinkIssueChips(List<IAddonCardElement> elements, SubAgentInfo addon, int order)
		{
			var skillNames = _skills.Addons.Select(skill => skill.Name).ToHashSet(StringComparer.Ordinal);
			var subAgentNames = _subAgents.Addons.Select(subAgent => subAgent.Name).ToHashSet(StringComparer.Ordinal);
			var memoryBlockNames = SettingsManager.GetCategory<MemoryBlock>().GetAll()
				.Select(block => block.Value.Name)
				.ToHashSet(StringComparer.Ordinal);

			foreach (var issue in SubAgentLinkChecker.Check(addon, skillNames, subAgentNames, memoryBlockNames))
			{
				var key = issue.Kind switch
				{
					SubAgentLinkIssueKind.Skill => "card.sub_agents.links.skill",
					SubAgentLinkIssueKind.SubAgent => "card.sub_agents.links.sub_agent",
					_ => "card.sub_agents.links.memory_block"
				};
				var label = Locale.GetConstKey(Locale.Format(key, issue.Name));

				elements.Add(new AddonCardChip
				{
					Order = order++,
					Brush = Brushes.OrangeRed,
					Icon = MaterialIconKind.AlertCircle,
					Label = label,
					ToolTip = label
				});
			}
		}

		private static void AddCountChip(List<IAddonCardElement> elements, int order, VisualIconKind icon, string key, int count)
		{
			if (count == 0)
				return;

			elements.Add(new AddonCardChip
			{
				Order = order,
				Icon = icon,
				Label = Locale.GetConstKey(Locale.Format(key, count))
			});
		}

		private static IEnumerable<string> FormatTools(ImmutableList<ToolNameWithSpecifier> tools) =>
			tools.Select(tool => tool.Specifier is null ? tool.ToolName : $"{tool.ToolName}({tool.Specifier})");
	}
}
