using LLMDesktopAssistant.Addons;
using LLMDesktopAssistant.Scripting;
using LLMDesktopAssistant.Prompting.Context.Scripting;
using LLMDesktopAssistant.Utils;

namespace LLMDesktopAssistant.Prompting.Context
{
	public class PromptContextInfo : ScriptableAddonBase<PromptContextInfo, PromptContextChange>
	{
		/// <summary>
		/// The variability of this prompt context.
		/// </summary>
		public PromptSectionVariability Variability
		{
			get;
			set => SetProperty(ref field, value);
		}

		/// <summary>
		/// Gets the provider implementation for this prompt context.
		/// </summary>
		public IPromptContextProvider Provider
		{
			get;
			set => SetProperty(ref field, value);
		} = null!;

		/// <summary>
		/// The kind of the context: how it is executed and where its text lands. A scriptable context parses it
		/// from its frontmatter; a native section declares it when it builds its info. It is never derived from
		/// the provider type — the engine that creates the provider reads this value, so deriving it from the
		/// provider would be circular.
		/// </summary>
		public PromptContextProviderKind Kind
		{
			get;
			set => SetProperty(ref field, value);
		} = PromptContextProviderKind.Unknown;

		/// <summary>
		/// The timeout of a scriptable provider, in milliseconds.
		/// </summary>
		public int TimeoutMs
		{
			get;
			set => SetProperty(ref field, value);
		} = ScriptPromptContextHelper.DefaultTimeoutMs;

		protected override void ValidatePropertiesCore(AppendOnlyList<string> errors)
		{
			base.ValidatePropertiesCore(errors);

			if (Provider == null)
				errors.Add("Provider is required.");
		}
	}
}
