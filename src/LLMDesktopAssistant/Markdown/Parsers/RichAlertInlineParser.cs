using Markdig.Extensions.Alerts;
using Markdig.Helpers;
using Markdig.Parsers;
using Markdig.Renderers.Html;
using Markdig.Syntax;

namespace LLMDesktopAssistant.Markdown.Parsers;

/// <summary>
/// Extended alert inline parser (based on <see cref="AlertInlineParser"/> from Markdig)
/// that supports GitHub-style alerts plus custom titles and colors:
/// <code>
/// &gt; [!NOTE]                         - standard GFM kind
/// &gt; [!SUCCESS]                      - extended kind (any ASCII word works)
/// &gt; [!SUCCESS:Done]                 - kind + custom title
/// &gt; [!Important message:#FF98D8]    - custom title + color
/// &gt; [!WARNING:Be careful:#FFA500]   - kind + custom title + color
/// </code>
/// The whole raw text between "[!" and "]" is stored in <see cref="AlertBlock.Kind"/>
/// and parsed later by the UI node (title/color extraction).
/// </summary>
public class RichAlertInlineParser : InlineParser
{
	/// <summary>
	/// Gets or sets a value indicating whether alerts can be nested inside other blocks
	/// (e.g. inside a blockquote or a list item).
	/// Alerts are never allowed inside another alert block regardless of this setting.
	/// </summary>
	public bool AllowNestedAlerts { get; set; }

	public RichAlertInlineParser()
	{
		AllowNestedAlerts = false;
		OpeningCharacters = ['['];
	}

	/// <inheritdoc/>
	public override bool Match(InlineProcessor processor, ref StringSlice slice)
	{
		if (slice.PeekChar() != '!')
			return false;

		// The alert must be the first child of a quote block. Example:
		// > [!NOTE]
		// > This is a note
		if (processor.Block is not ParagraphBlock paragraphBlock ||
			paragraphBlock.Parent is not QuoteBlock quoteBlock ||
			paragraphBlock.Inline?.FirstChild != null ||
			quoteBlock is AlertBlock ||
			IsInsideAlertBlock(quoteBlock) ||
			(!AllowNestedAlerts && quoteBlock.Parent is not MarkdownDocument))
		{
			return false;
		}

		StringSlice saved = slice;

		slice.SkipChar(); // Skip [
		char c = slice.NextChar(); // Skip !

		// Parse the kind/content until ']'. Any non-empty content is allowed here
		// (unlike the stock parser which only accepts ASCII letters):
		// standard kinds, custom titles with spaces/Unicode, colors like ":#FF98D8".
		int contentStart = slice.Start;
		int contentEnd = contentStart - 1;

		while (c != ']' && c != '\0' && c != '\n' && c != '\r')
		{
			contentEnd = slice.Start;
			c = slice.NextChar();
		}

		// Content must be non-empty and the bracket must be closed on the same line
		if (c != ']' || contentEnd < contentStart)
		{
			slice = saved;
			return false;
		}

		var alertContent = new StringSlice(slice.Text, contentStart, contentEnd);
		c = slice.NextChar(); // Skip ]

		// After the closing bracket only spaces/tabs are allowed until the end of line
		// (same rule as the stock GFM parser).
		int triviaStart = slice.Start;
		int triviaEnd = triviaStart;
		while (true)
		{
			if (c == '\0' || c == '\n' || c == '\r')
			{
				triviaEnd = slice.Start;
				if (c == '\r')
				{
					c = slice.NextChar(); // Skip \r
					if (c == '\0' || c == '\n')
					{
						triviaEnd = slice.Start;
						if (c == '\n')
						{
							slice.SkipChar(); // Skip \n
						}
					}
				}
				else if (c == '\n')
				{
					slice.SkipChar(); // Skip \n
				}
				break;
			}
			else if (!c.IsSpaceOrTab())
			{
				slice = saved;
				return false;
			}

			c = slice.NextChar();
		}

		var alertBlock = new AlertBlock(alertContent)
		{
			Span = quoteBlock.Span,
			TriviaSpaceAfterKind = new StringSlice(slice.Text, triviaStart, triviaEnd),
			Line = quoteBlock.Line,
			Column = quoteBlock.Column,
		};

		HtmlAttributes attributes = alertBlock.GetAttributes();
		attributes.AddClass("markdown-alert");
		attributes.AddClass("markdown-alert-" + ToHtmlClass(alertContent.AsSpan()));

		quoteBlock.ReplaceBy(alertBlock);
		processor.ReplaceParentContainer(quoteBlock, alertBlock);

		return true;
	}

	/// <summary>
	/// Converts arbitrary alert content into a safe HTML class name
	/// (the stock parser could rely on ASCII kinds, we cannot).
	/// </summary>
	private static string ToHtmlClass(ReadOnlySpan<char> content)
	{
		Span<char> buffer = content.Length <= 64 ? stackalloc char[64] : new char[Math.Min(content.Length, 64)];
		int count = 0;
		foreach (var ch in content)
		{
			if (count >= buffer.Length)
				break;
			buffer[count++] = char.IsAsciiLetterOrDigit(ch) ? char.ToLowerInvariant(ch) : '-';
		}

		return new string(buffer[..count]);
	}

	private static bool IsInsideAlertBlock(Block block)
	{
		var parent = block.Parent;
		while (parent != null)
		{
			if (parent is AlertBlock)
				return true;
			parent = parent.Parent;
		}

		return false;
	}
}
