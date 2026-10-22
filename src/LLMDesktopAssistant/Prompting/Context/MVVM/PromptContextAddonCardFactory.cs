using LLMDesktopAssistant.Addons.MVVM;
using LLMDesktopAssistant.Addons.MVVM.Elements;
using LLMDesktopAssistant.Localization;
using LLMDesktopAssistant.Services;
using Material.Icons;

namespace LLMDesktopAssistant.Prompting.Context.MVVM
{
	/// <summary>
	/// Builds the addon cards of the prompt context addon type ('context'): the enabled override
	/// (a prompt context has no hidden state) and the variability chip. The toggle of a fixed
	/// context is shown disabled: fixed contexts are always enabled.
	/// </summary>
	[Service(typeof(IAddonCardFactory<PromptContextInfo, PromptContextChange>))]
	public class PromptContextAddonCardFactory(
		IServiceProvider services
		) : AddonCardFactoryBase<PromptContextInfo, PromptContextChange>(services)
	{
		/// <inheritdoc/>
		protected override VisualIconKind TypeIcon => MaterialIconKind.Layers;

		/// <inheritdoc/>
		protected override void AddHeaderChanges(AddonCardContext<PromptContextInfo, PromptContextChange> context,
			List<IAddonCardElement> elements)
		{
			// A prompt context has no hidden state: only the enabled override is editable.
			elements.Add(new AddonCardEnabledChange<PromptContextInfo, PromptContextChange>(context) { Order = HeaderOrder });
		}

		/// <inheritdoc/>
		protected override void AddTypeChips(AddonCardContext<PromptContextInfo, PromptContextChange> context,
			List<IAddonCardElement> elements, int order)
		{
			// What produces the text and where it lands: anchored into the frozen header, the current request
			// only, or a persisted stamp.
			elements.Add(new AddonCardChip
			{
				Order = order,
				Label = Locale.GetKey($"prompt.context.kind.{context.Addon.Kind.ToString().ToLowerInvariant()}"),
				ToolTip = Locale.GetKey("prompt.context.kind.hint")
			});

			// How often the content itself changes - orthogonal to the kind.
			elements.Add(new AddonCardChip
			{
				Order = order + 1,
				Label = Locale.GetKey($"prompt.context.variability.{context.Addon.Variability.ToString().ToLowerInvariant()}"),
				ToolTip = Locale.GetKey("prompt.context.variability.hint")
			});
		}
	}
}
