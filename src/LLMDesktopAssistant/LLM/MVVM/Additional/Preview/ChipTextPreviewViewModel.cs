using LLMDesktopAssistant.LLM.MVVM.Additional;
using LLMDesktopAssistant.Localization;
using LLMDesktopAssistant.MVVM;

namespace LLMDesktopAssistant.LLM.MVVM.Additional.Preview
{
	/// <summary>
	/// The preview of a content part: the model-facing text the chip hides, peeked in the tooltip and shown whole
	/// in the flyout.
	/// </summary>
	/// <remarks>
	/// The peek is cut by lines and characters, and an unbalanced code fence is closed before rendering - an open
	/// fence would swallow the rest of the poke into one grey block. The preview follows the part: a part updated in
	/// place under a <c>replacement_key</c> re-raises through <see cref="AdditionalMessageContentPart.Content"/> and
	/// the peek is built again.
	/// </remarks>
	[ViewModelFor(typeof(ChipTextPreviewView))]
	public sealed class ChipTextPreviewViewModel : ViewModelBase
	{
		/// <summary>
		/// The number of lines the peek keeps before it truncates.
		/// </summary>
		private const int peekLines = 8;

		/// <summary>
		/// The number of characters the peek keeps before it truncates.
		/// </summary>
		private const int peekCharacters = 600;

		private readonly AdditionalMessageContentPart _part;
		private Peek? _peek;

		/// <summary>
		/// Initialises the preview of a content part.
		/// </summary>
		/// <param name="part">The part whose text is previewed.</param>
		public ChipTextPreviewViewModel(AdditionalMessageContentPart part)
		{
			_part = part;
			_part.PropertyChanged += OnPartPropertyChanged;
		}

		/// <summary>
		/// Gets the text the part contributes to the model, untruncated.
		/// </summary>
		public string FullMarkdown => _part.Content;

		/// <summary>
		/// Gets the markdown shown in the tooltip: the beginning of <see cref="FullMarkdown"/>.
		/// </summary>
		public string Markdown => GetPeek().Text;

		/// <summary>
		/// Gets the number of characters the peek leaves out.
		/// </summary>
		public int HiddenCharacters => GetPeek().Hidden;

		/// <summary>
		/// Gets a value indicating whether the peek shows only a part of the text.
		/// </summary>
		public bool IsTruncated => HiddenCharacters > 0;

		/// <summary>
		/// Gets the line under the peek that says how much is left out, or <see langword="null"/> when nothing is.
		/// </summary>
		public LocaleKeyBase? HiddenSummary => IsTruncated
			? Locale.GetFormattedKey("message.chip.preview.truncated", HiddenCharacters.ToString("N0"))
			: null;

		private Peek GetPeek() => _peek ??= CreatePeek(FullMarkdown);

		private void OnPartPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
		{
			if (e.PropertyName is not null and not nameof(AdditionalMessageContentPart.Content))
				return;

			_peek = null;
			RaisePropertyChanged(nameof(FullMarkdown));
			RaisePropertyChanged(nameof(Markdown));
			RaisePropertyChanged(nameof(HiddenCharacters));
			RaisePropertyChanged(nameof(IsTruncated));
			RaisePropertyChanged(nameof(HiddenSummary));
		}

		private static Peek CreatePeek(string markdown)
		{
			var lines = markdown.Split('\n');
			var kept = new List<string>();
			int keptCharacters = 0;

			foreach (var line in lines)
			{
				if (kept.Count >= peekLines || keptCharacters + line.Length > peekCharacters)
					break;

				kept.Add(line);
				keptCharacters += line.Length + 1;
			}

			if (kept.Count == lines.Length)
				return new Peek(markdown, 0);

			var text = string.Join('\n', kept);
			var hidden = markdown.Length - text.Length;

			if (CountFences(text) % 2 != 0)
				text += "\n```";

			return new Peek(text, hidden);
		}

		/// <summary>
		/// Counts the fence lines of the peek, so an odd number can be closed before the renderer sees it.
		/// </summary>
		private static int CountFences(string text)
		{
			var count = 0;

			foreach (var line in text.Split('\n'))
				if (line.TrimStart().StartsWith("```", StringComparison.Ordinal))
					count++;

			return count;
		}

		protected override void Dispose(bool disposing)
		{
			base.Dispose(disposing);

			if (disposing)
				_part.PropertyChanged -= OnPartPropertyChanged;
		}

		/// <summary>
		/// The peek and how much it leaves out.
		/// </summary>
		/// <param name="Text">The markdown to render.</param>
		/// <param name="Hidden">The number of characters left out.</param>
		private readonly record struct Peek(string Text, int Hidden);
	}
}
