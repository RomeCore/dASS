using LLMDesktopAssistant.Addons.MVVM;
using LLMDesktopAssistant.Addons.MVVM.Elements;
using LLMDesktopAssistant.Localization;
using LLMDesktopAssistant.Services;
using LLMDesktopAssistant.Services.Instances;
using Material.Icons;

namespace LLMDesktopAssistant.SlashCommands
{
	/// <summary>
	/// Builds the addon cards of the slash command addon type ('commands').
	/// </summary>
	/// <remarks>
	/// A command has no hidden mode (it is never injected into a prompt and never exposed to agents), so the factory
	/// replaces the default enabled/hidden editor of <see cref="AddonCardFactoryBase{TAddon, TChange}"/> with the
	/// enabled-only one and lets the group cards aggregate the enabled state only. On top of the base elements it
	/// adds the source-kind chip that says what the command was derived from (a skill, a sub-agent, ...). Everything
	/// else (source, category and diagnostic chips, tags, path, file actions, parameter editor and metadata block) is
	/// produced by the base factory.
	/// </remarks>
	[Service(typeof(IAddonCardFactory<SlashCommandInfo, SlashCommandChange>))]
	public class SlashCommandAddonCardFactory : AddonCardFactoryBase<SlashCommandInfo, SlashCommandChange>
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="SlashCommandAddonCardFactory"/> class.
		/// </summary>
		/// <param name="explorerOpener">The service used by the "show in explorer" action.</param>
		/// <param name="toastService">The service used to report failures of the file actions.</param>
		public SlashCommandAddonCardFactory(IExplorerOpener explorerOpener, IToastService toastService)
			: base(explorerOpener, toastService)
		{
		}

		/// <inheritdoc/>
		protected override VisualIconKind TypeIcon => MaterialIconKind.Console;

		/// <inheritdoc/>
		protected override void AddHeaderChanges(AddonCardContext<SlashCommandInfo, SlashCommandChange> context, List<IAddonCardElement> elements)
		{
			// A command has no hidden state: only the enabled override is editable.
			elements.Add(new AddonCardEnabledChange<SlashCommandInfo, SlashCommandChange>(context) { Order = HeaderOrder });
		}

		/// <inheritdoc/>
		protected override void AddGroupHeaderChanges(AddonGroupCardContext<SlashCommandInfo, SlashCommandChange> context, List<IAddonCardElement> elements)
		{
			elements.Add(new AddonCardGroupEnabledChange(context.Children) { Order = HeaderOrder });
		}

		/// <inheritdoc/>
		protected override void AddTypeChips(AddonCardContext<SlashCommandInfo, SlashCommandChange> context, List<IAddonCardElement> elements, int order)
		{
			elements.Add(new AddonCardChip
			{
				Order = order,
				Icon = GetSourceKindIcon(context.Addon.SourceKind),
				Label = Locale.GetKey($"card.commands.source.{context.Addon.SourceKind.ToString().ToLowerInvariant()}"),
				ToolTip = Locale.GetKey("card.commands.source")
			});
		}

		private static VisualIconKind GetSourceKindIcon(SlashCommandSource source) => source switch
		{
			SlashCommandSource.Skill => MaterialIconKind.Cards,
			SlashCommandSource.SubAgent => MaterialIconKind.RobotHappy,
			SlashCommandSource.Tool => MaterialIconKind.Wrench,
			SlashCommandSource.Native => MaterialIconKind.Cog,
			SlashCommandSource.Script => MaterialIconKind.ScriptTextOutline,
			_ => MaterialIconKind.HelpCircle
		};
	}
}
