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
	public class PromptContextAddonCardFactory : AddonCardFactoryBase<PromptContextInfo, PromptContextChange>
	{
		/// <inheritdoc/>
		protected override MaterialIconKind TypeIcon => MaterialIconKind.Layers;

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
			elements.Add(new AddonCardChip
			{
				Order = order,
				Label = Locale.GetKey($"prompt.context.variability.{context.Addon.Variability.ToString().ToLowerInvariant()}"),
				ToolTip = Locale.GetKey("prompt.context.variability.hint")
			});
		}
	}
}
