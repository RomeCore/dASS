using Avalonia.Media;
using LLMDesktopAssistant.Controls.Text;
using LLMDesktopAssistant.Tools.Execution.Domain;
using LLMDesktopAssistant.Tools.Specifiers;

namespace LLMDesktopAssistant.Tools.MVVM.Elements
{
	/// <summary>
	/// Turns a specifier rule into the coloured spans the card renders: the constructs a reader has to
	/// tell apart from prose, plus the place a broken rule failed.
	/// </summary>
	public sealed class SpecifierPatternHighlighter
	{
		private static readonly IBrush SeparatorBrush = Brush("#C586C0");
		private static readonly IBrush ParameterBrush = Brush("#9CDCFE");
		private static readonly IBrush WildcardBrush = Brush("#DCDCAA");
		private static readonly IBrush GroupBrush = Brush("#4EC9B0");
		private static readonly IBrush ErrorBrush = Brush("#F44747");

		private readonly ToolArgumentsDescriptor _arguments;
		private readonly HashSet<string> _names;

		/// <summary>
		/// Initializes a new instance of the <see cref="SpecifierPatternHighlighter"/> class.
		/// </summary>
		/// <param name="arguments">The arguments of the tool the rule belongs to.</param>
		public SpecifierPatternHighlighter(ToolArgumentsDescriptor arguments)
		{
			ArgumentNullException.ThrowIfNull(arguments);

			_arguments = arguments;
			_names = [.. arguments.SpecifierParameters.Select(argument => argument.Name)];
		}

		/// <summary>
		/// Builds the highlight spans of a rule.
		/// </summary>
		/// <param name="text">The rule text.</param>
		public IReadOnlyList<TextHighlightSpan> BuildSpans(string text)
		{
			var spans = new List<TextHighlightSpan>();

			foreach (var token in SpecifierPatternTokenizer.Tokenize(text ?? string.Empty, _names))
			{
				var brush = token.Kind switch
				{
					SpecifierTokenKind.Separator => SeparatorBrush,
					SpecifierTokenKind.Parameter => ParameterBrush,
					SpecifierTokenKind.Wildcard => WildcardBrush,
					SpecifierTokenKind.Class or SpecifierTokenKind.Brace => GroupBrush,
					_ => null
				};

				if (brush is not null)
					spans.Add(new TextHighlightSpan(token.Start, token.Length, brush));
			}

			var diagnostic = SpecifierPatternValidator.Validate(text ?? string.Empty, _arguments).FirstOrDefault();
			if (diagnostic is { Length: > 0 })
				spans.Add(new TextHighlightSpan(diagnostic.Position, diagnostic.Length, ErrorBrush, TextDecorations.Underline));

			return spans;
		}

		/// <summary>
		/// Creates the provider the text box renders through.
		/// </summary>
		public HighlightTransformProvider CreateProvider() =>
			new((text, _) => new HighlightTransformResult(BuildSpans(text)));

		private static IBrush Brush(string hex) => new SolidColorBrush(Color.Parse(hex));
	}
}
