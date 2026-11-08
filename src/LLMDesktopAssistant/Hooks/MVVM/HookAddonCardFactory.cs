using LLMDesktopAssistant.Addons.MVVM;
using LLMDesktopAssistant.Addons.MVVM.Elements;
using LLMDesktopAssistant.Localization;
using LLMDesktopAssistant.Services;
using Material.Icons;

namespace LLMDesktopAssistant.Hooks.MVVM
{
	/// <summary>
	/// Builds the addon cards of the hook addon type ('hooks').
	/// </summary>
	/// <remarks>
	/// A hook is dispatched by the chat, never injected into a prompt, so it has no hidden mode: the factory
	/// replaces the default enabled/hidden editor of <see cref="AddonCardFactoryBase{TAddon, TChange}"/> with the
	/// enabled-only one and lets the group cards aggregate the enabled state only. On top of the base elements it
	/// adds the chips that answer the questions the list can answer - which events the hook listens to
	/// ('hook-type'), which targets of those events ('hook-target') and how it is dispatched ('execution-flow'). The
	/// body and the metadata are produced by the base helpers.
	/// </remarks>
	[Service(typeof(IAddonCardFactory<HookInfo, HookChange>))]
	public class HookAddonCardFactory(IServiceProvider services)
		: AddonCardFactoryBase<HookInfo, HookChange>(services)
	{
		/// <inheritdoc/>
		protected override VisualIconKind TypeIcon => MaterialIconKind.Webhook;

		/// <inheritdoc/>
		protected override void AddHeaderChanges(AddonCardContext<HookInfo, HookChange> context,
			List<IAddonCardElement> elements)
		{
			// A hook has no hidden state: only the enabled override is editable.
			elements.Add(new AddonCardEnabledChange<HookInfo, HookChange>(context) { Order = HeaderOrder });
		}

		/// <inheritdoc/>
		protected override void AddGroupHeaderChanges(AddonGroupCardContext<HookInfo, HookChange> context,
			List<IAddonCardElement> elements)
		{
			elements.Add(new AddonCardGroupEnabledChange(context.Children) { Order = HeaderOrder });
		}

		/// <inheritdoc/>
		protected override void AddTypeChips(AddonCardContext<HookInfo, HookChange> context,
			List<IAddonCardElement> elements, int order)
		{
			var addon = context.Addon;

			elements.Add(new AddonCardChip
			{
				Order = order++,
				Icon = MaterialIconKind.FilterVariant,
				Label = Locale.GetConstKey(addon.HookType),
				ToolTip = Locale.GetKey("card.hooks.type")
			});

			if (!string.IsNullOrEmpty(addon.HookTarget))
			{
				elements.Add(new AddonCardChip
				{
					Order = order++,
					Icon = MaterialIconKind.TagMultiple,
					Label = Locale.GetConstKey(addon.HookTarget),
					ToolTip = Locale.GetKey("card.hooks.target")
				});
			}

			elements.Add(new AddonCardChip
			{
				Order = order,
				Icon = GetExecutionFlowIcon(addon.ExecutionFlow),
				Label = Locale.GetKey($"card.hooks.flow.{(addon.ExecutionFlow?.ToString() ?? "default").ToLowerInvariant()}"),
				ToolTip = Locale.GetKey("card.hooks.flow")
			});
		}

		/// <inheritdoc/>
		protected override void AddBlocks(AddonCardContext<HookInfo, HookChange> context,
			List<IAddonCardElement> elements)
		{
			AddTextBlock(elements, Locale.GetKey("card.body"), context.Addon.Body, BlockOrder + 10);
			AddMetadataBlock(context, elements, order: BlockOrder + 30);
		}

		/// <summary>
		/// Gets the icon that hints at how the hook is dispatched relative to the other hooks of the same event.
		/// </summary>
		private static VisualIconKind GetExecutionFlowIcon(HookExecutionFlow? flow) => flow switch
		{
			HookExecutionFlow.FireAndForget => MaterialIconKind.RocketLaunchOutline,
			HookExecutionFlow.Parallel => MaterialIconKind.CallSplit,
			HookExecutionFlow.Sequential => MaterialIconKind.FormatListNumbered,
			null => MaterialIconKind.ArrowLeftTop,
			_ => MaterialIconKind.HelpCircle
		};
	}
}
