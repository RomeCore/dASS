using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using Avalonia.Media;
using LLMDesktopAssistant.Addons.MVVM;
using LLMDesktopAssistant.Addons.MVVM.Elements;
using LLMDesktopAssistant.LLM.MVVM.Settings;
using LLMDesktopAssistant.Localization;
using LLMDesktopAssistant.Services;
using LLMDesktopAssistant.Services.Instances;
using LLMDesktopAssistant.Tools.MVVM.Elements;
using Material.Icons;

namespace LLMDesktopAssistant.Tools.MVVM
{
	/// <summary>
	/// Builds the addon cards of the tool addon type ('tools'): the enabled/hidden overrides of the header,
	/// the approval level selector, the policy mask and the specifier editor blocks. The group cards
	/// aggregate the approved level of their visible children with a "Mixed" selector.
	/// </summary>
	[Service(typeof(IAddonCardFactory<ToolInfo, ToolChange>))]
	public class ToolAddonCardFactory : AddonCardFactoryBase<ToolInfo, ToolChange>
	{
		private static readonly JsonSerializerOptions _argumentSchemaSerializationOptions = new()
		{
			Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
			TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
			WriteIndented = true
		};

		private static readonly ImmutableList<AddonCardSelectorOption<ToolApprovalLevel?>> _approvalOptions =
			[.. ToolApprovalLevelItem.All.Select(item =>
				new AddonCardSelectorOption<ToolApprovalLevel?>(item.Value, Locale.GetConstKey(item.DisplayName)))];

		/// <summary>
		/// Initializes a new instance of the <see cref="ToolAddonCardFactory"/> class.
		/// </summary>
		/// <param name="explorerOpener">The service used by the "show in explorer" action.</param>
		/// <param name="toastService">The service used to report failures of the file actions.</param>
		public ToolAddonCardFactory(IExplorerOpener explorerOpener, IToastService toastService)
			: base(explorerOpener, toastService)
		{
		}

		/// <inheritdoc/>
		protected override VisualIconKind TypeIcon => MaterialIconKind.Wrench;

		/// <inheritdoc/>
		protected override (LocaleKeyBase? Prefix, IBrush? Brush) GetNamePrefix(AddonCardContext<ToolInfo, ToolChange> context) =>
			context.Addon.ToolSource switch
			{
				ToolSource.MCP => (Locale.GetKey("tool.source.mcp"), Brushes.LightGreen),
				ToolSource.Meta => (Locale.GetKey("tool.source.meta"), Brushes.Magenta),
				_ => (null, null)
			};

		/// <inheritdoc/>
		protected override void AddHeaderElements(AddonCardContext<ToolInfo, ToolChange> context, List<IAddonCardElement> elements)
		{
			elements.Add(CreateApprovalChange(context));
		}

		/// <inheritdoc/>
		protected override void AddBlocks(AddonCardContext<ToolInfo, ToolChange> context, List<IAddonCardElement> elements)
		{
			AddParametersBlock(context, elements);

			elements.Add(new AddonCardToolPolicyMaskBlock(context) { Order = BlockOrder + 10 });

			elements.Add(new AddonCardBlock
			{
				Order = BlockOrder + 15,
				Title = Locale.GetKey("card.tools.arguments"),
				Visibility = AddonCardBlockVisibility.Collapsible,
				ToggleIcon = MaterialIconKind.CodeBraces,
				ToggleToolTip = Locale.GetKey("card.tools.arguments.toggle"),
				Content = new AddonCardBodyTextViewModel(context.Addon.ArgumentSchema.ToJsonString(_argumentSchemaSerializationOptions))
			});

			if (context.Addon.SpecifierAnalyzer is not null)
				elements.Add(new AddonCardToolSpecifiersBlock(context) { Order = BlockOrder + 20 });
		}

		/// <inheritdoc/>
		protected override void AddGroupHeaderChanges(AddonGroupCardContext<ToolInfo, ToolChange> context, List<IAddonCardElement> elements)
		{
			base.AddGroupHeaderChanges(context, elements);

			elements.Add(new AddonCardGroupSelectorChange<ToolApprovalLevel?>(context.Children, _approvalOptions,
				Locale.GetKey("card.group.mixed"))
			{
				Order = HeaderOrder + 1
			});
		}

		private static AddonCardSelectorChange<ToolInfo, ToolChange, ToolApprovalLevel?> CreateApprovalChange(
			AddonCardContext<ToolInfo, ToolChange> context)
		{
			return new AddonCardSelectorChange<ToolInfo, ToolChange, ToolApprovalLevel?>(
				context,
				_approvalOptions,
				getReference: tool => tool.ApprovalLevel
					?? (context.SetConfig as ToolsetConfiguration)?.DefaultApprovalLevel
					?? ToolApprovalLevel.PolicyBased,
				getOverride: change => change.ApprovalLevel,
				setOverride: (change, value) => change.ApprovalLevel = value)
			{
				Order = HeaderOrder + 1,
				IsShownLeft = false
			};
		}
	}
}
