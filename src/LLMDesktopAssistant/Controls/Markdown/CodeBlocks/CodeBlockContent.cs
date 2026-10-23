using Markdig.Syntax;

namespace LLMDesktopAssistant.Controls.Markdown.CodeBlocks
{
	/// <summary>
	/// An immutable snapshot of one fenced code block: what a preview provider or a code block action gets
	/// instead of the live Markdig object.
	/// </summary>
	/// <remarks>
	/// A Markdig block is parser state: it only lives for the duration of one parse, it cannot be built outside
	/// the pipeline, and handing it to a provider would tie every consumer to Markdig internals. The snapshot can
	/// also be produced by a non-markdown caller (a tool result card, a command preview).
	/// </remarks>
	public readonly record struct CodeBlockContent(string Language, string Text, string RawInfo, SourceSpan Span)
	{
		/// <summary>
		/// Gets an empty snapshot: no language, no text. Used before the block has any content.
		/// </summary>
		public static CodeBlockContent Empty { get; } = new(string.Empty, string.Empty, string.Empty, default);
	}
}
