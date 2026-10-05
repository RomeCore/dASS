using LLMDesktopAssistant.Addons.MVVM;
using LLMDesktopAssistant.Addons.MVVM.Elements;
using LLMDesktopAssistant.Localization;
using LLMDesktopAssistant.Services;
using LLMDesktopAssistant.Services.Instances;
using LLMDesktopAssistant.Tools;
using Material.Icons;

namespace LLMDesktopAssistant.Prompting.Skills
{
	/// <summary>
	/// Builds the addon cards of the skill addon type ('skills').
	/// </summary>
	/// <remarks>
	/// Everything that is not specific to skills (enable/hide switches, source and diagnostic chips,
	/// tags, path, file actions, parameter editor, body and metadata blocks) is produced by
	/// <see cref="AddonCardFactoryBase{TAddon, TChange}"/>. This factory only declares what a skill
	/// card needs on top of that: the injection mode selector, the tool count chip and the tool lists.
	/// </remarks>
	[Service(typeof(IAddonCardFactory<SkillInfo, SkillChange>))]
	public class SkillAddonCardFactory : AddonCardFactoryBase<SkillInfo, SkillChange>
	{
		private static readonly ImmutableList<AddonCardSelectorOption<SkillInjectionMode?>> InjectionModes =
			[.. Enum.GetValues<SkillInjectionMode>()
				.Select(mode => new AddonCardSelectorOption<SkillInjectionMode?>(
					mode,
					Locale.GetKey($"card.skills.injection_mode.{mode.ToString().ToLowerInvariant()}")))];

		/// <summary>
		/// Initializes a new instance of the <see cref="SkillAddonCardFactory"/> class.
		/// </summary>
		/// <param name="explorerOpener">The service used by the "show in explorer" action.</param>
		/// <param name="toastService">The service used to report failures of the file actions.</param>
		public SkillAddonCardFactory(IExplorerOpener explorerOpener, IToastService toastService)
			: base(explorerOpener, toastService)
		{
		}

		/// <inheritdoc/>
		protected override VisualIconKind TypeIcon => MaterialIconKind.Cards;

		/// <inheritdoc/>
		protected override void AddHeaderElements(AddonCardContext<SkillInfo, SkillChange> context, List<IAddonCardElement> elements)
		{
			elements.Add(new AddonCardSelectorChange<SkillInfo, SkillChange, SkillInjectionMode?>(
				context,
				InjectionModes,
				getReference: addon => addon.InjectionMode,
				getOverride: change => change.InjectionMode,
				setOverride: (change, value) => change.InjectionMode = value)
			{
				Order = HeaderOrder + 1,
				IsShownLeft = false
			});
		}

		/// <inheritdoc/>
		protected override void AddTypeChips(AddonCardContext<SkillInfo, SkillChange> context, List<IAddonCardElement> elements, int order)
		{
			var toolCount = CountTools(context.Addon);

			if (toolCount == 0)
				return;

			elements.Add(new AddonCardChip
			{
				Order = order,
				Icon = MaterialIconKind.Wrench,
				Label = Locale.GetConstKey(Locale.Format("card.skills.tools.badge", toolCount))
			});
		}

		/// <inheritdoc/>
		protected override void AddBlocks(AddonCardContext<SkillInfo, SkillChange> context, List<IAddonCardElement> elements)
		{
			var addon = context.Addon;

			AddParametersBlock(context, elements);

			var order = BlockOrder + 10;
			AddChipsBlock(elements, Locale.GetKey("card.skills.tools.allowed"), MaterialIconKind.Check,
				FormatTools(addon.AllowedTools), order++);
			AddChipsBlock(elements, Locale.GetKey("card.skills.tools.available"), MaterialIconKind.Wrench,
				FormatTools(addon.AvailableTools), order++);
			AddChipsBlock(elements, Locale.GetKey("card.skills.tools.disallowed"), MaterialIconKind.Close,
				FormatTools(addon.DisallowedTools), order);

			AddTextBlock(elements, Locale.GetKey("card.body"), addon.BodyGetter(addon), BlockOrder + 20);
			AddMetadataBlock(context, elements, order: BlockOrder + 30);
		}

		private static int CountTools(SkillInfo addon) =>
			addon.AllowedTools.Count + addon.AvailableTools.Count + addon.DisallowedTools.Count;

		private static IEnumerable<string> FormatTools(ImmutableList<ToolNameWithSpecifier> tools) =>
			tools.Select(tool => tool.Specifier is null ? tool.ToolName : $"{tool.ToolName}({tool.Specifier})");
	}
}
