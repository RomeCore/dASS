using LLMDesktopAssistant.Addons.MVVM;
using LLMDesktopAssistant.Addons.MVVM.Elements;
using LLMDesktopAssistant.Localization;
using LLMDesktopAssistant.Services;
using LLMDesktopAssistant.Services.Instances;
using Material.Icons;

namespace LLMDesktopAssistant.Scripting.Lua
{
	/// <summary>
	/// Builds the addon cards of the Lua script addon type ('scripts/lua').
	/// </summary>
	/// <remarks>
	/// The Lua scripts have no hidden mode: they are not injected into prompts, they are loaded into
	/// the Lua environment of the chat. The factory therefore replaces the default enabled/hidden
	/// editor of <see cref="AddonCardFactoryBase{TAddon, TChange}"/> with the enabled-only one and
	/// lets the group cards aggregate the enabled state only. Everything else (source, category and
	/// diagnostic chips, tags, path, file actions, parameter editor and metadata block) is produced
	/// by the base factory.
	/// </remarks>
	[Service(typeof(IAddonCardFactory<LuaScriptInfo, LuaScriptChange>))]
	public class LuaScriptAddonCardFactory : AddonCardFactoryBase<LuaScriptInfo, LuaScriptChange>
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="LuaScriptAddonCardFactory"/> class.
		/// </summary>
		/// <param name="explorerOpener">The service used by the "show in explorer" action.</param>
		/// <param name="toastService">The service used to report failures of the file actions.</param>
		public LuaScriptAddonCardFactory(IExplorerOpener explorerOpener, IToastService toastService)
			: base(explorerOpener, toastService)
		{
		}

		/// <inheritdoc/>
		protected override VisualIconKind TypeIcon => MaterialIconKind.ScriptTextOutline;

		/// <inheritdoc/>
		protected override void AddHeaderChanges(AddonCardContext<LuaScriptInfo, LuaScriptChange> context, List<IAddonCardElement> elements)
		{
			elements.Add(new AddonCardEnabledChange<LuaScriptInfo, LuaScriptChange>(context) { Order = HeaderOrder });
		}

		/// <inheritdoc/>
		protected override void AddGroupHeaderChanges(AddonGroupCardContext<LuaScriptInfo, LuaScriptChange> context, List<IAddonCardElement> elements)
		{
			elements.Add(new AddonCardGroupEnabledChange(context.Children) { Order = HeaderOrder });
		}

		/// <inheritdoc/>
		protected override void AddTypeChips(AddonCardContext<LuaScriptInfo, LuaScriptChange> context, List<IAddonCardElement> elements, int order)
		{
			var addon = context.Addon;

			if (addon.IsNative)
			{
				elements.Add(new AddonCardChip
				{
					Order = order++,
					Icon = MaterialIconKind.Cog,
					Label = Locale.GetKey("card.scripts.native")
				});
			}

			if (addon.Namespace is not null)
			{
				elements.Add(new AddonCardChip
				{
					Order = order,
					Icon = MaterialIconKind.CodeBraces,
					Label = addon.Namespace.Length == 0
						? Locale.GetKey("card.scripts.namespace.global")
						: Locale.GetConstKey(addon.Namespace),
					ToolTip = Locale.GetKey("card.scripts.namespace")
				});
			}
		}

		/// <inheritdoc/>
		protected override void AddBlocks(AddonCardContext<LuaScriptInfo, LuaScriptChange> context, List<IAddonCardElement> elements)
		{
			AddParametersBlock(context, elements);
			AddTextBlock(elements, Locale.GetKey("card.scripts.manuals"), context.Addon.Manuals, BlockOrder + 10);
			AddMetadataBlock(context, elements, order: BlockOrder + 30);
		}
	}
}
