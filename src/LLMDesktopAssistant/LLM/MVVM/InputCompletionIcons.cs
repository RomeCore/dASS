using LLMDesktopAssistant.Controls.Icons;
using LLMDesktopAssistant.InputCompletion;
using Material.Icons;

namespace LLMDesktopAssistant.LLM.MVVM
{
	/// <summary>
	/// The icon the input-completion popup draws for a completion kind.
	/// </summary>
	public static class InputCompletionIcons
	{
		/// <summary>The icon of a state or a continuation of the given kind.</summary>
		public static VisualIconKind For(InputCompletionKind kind) => kind switch
		{
			InputCompletionKind.Command => MaterialIconKind.ConsoleLine,
			InputCompletionKind.Argument => MaterialIconKind.Tune,
			InputCompletionKind.Mention => MaterialIconKind.At,
			_ => VisualIconKind.None
		};
	}
}
