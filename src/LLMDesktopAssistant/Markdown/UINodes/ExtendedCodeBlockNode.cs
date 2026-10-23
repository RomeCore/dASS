using Avalonia.Controls;
using LiveMarkdown.Avalonia;
using LLMDesktopAssistant.Controls;
using LLMDesktopAssistant.Controls.Markdown;
using LLMDesktopAssistant.Markdown.Controls;
using LLMDesktopAssistant.Markdown.Nodes;
using Markdig.Syntax;
using Serilog;

namespace LLMDesktopAssistant.Markdown.UINodes
{
	/// <summary>
	/// Renders a fenced code block: a <see cref="PreviewCodeBlock"/> that owns the source, the preview tabs, the
	/// split view and the action buttons.
	/// </summary>
	/// <remarks>
	/// A node from scratch, like the alert and quick action nodes. Extensions are built once the opening fence line
	/// is complete, because the info string of a streaming block can still grow; the block is final when its fence
	/// is closed or the stream has stopped.
	/// </remarks>
	public sealed class ExtendedCodeBlockNode : BlockNode<FencedCodeBlock>
	{
		private bool _initialized = false;
		private readonly ExtendedCodeBlock _control = new();

		/// <inheritdoc />
		public override Control Control => _control;

		/// <inheritdoc />
		/// <remarks>
		/// The exact-type check is not enough here: the nested fence parser of the app replaces every fenced block
		/// with <see cref="NestedFencedCodeBlock"/>.
		/// </remarks>
		protected override bool MatchesBlock(FencedCodeBlock block)
			=> block is NestedFencedCodeBlock || block.GetType() == typeof(FencedCodeBlock);

		/// <inheritdoc />
		protected override bool UpdateCore(
			DocumentNode documentNode,
			FencedCodeBlock block,
			in ObservableStringBuilderChangedEventArgs change,
			CancellationToken cancellationToken)
		{
			var renderer = documentNode.Owner;
			var documentLength = renderer.MarkdownBuilder?.Length ?? 0;

			// An unterminated fence would have swallowed the rest of the document, so a block that is not last is
			// closed by its own fence; a block that is last is final only when the stream has stopped.
			// Span.End is the INCLUSIVE index of the block's last character, so a block that reaches the end of the
			// document ends at documentLength - 1 - comparing it against the length itself is true for every block.
			var fenceClosed = block.Span.End < documentLength - 1;
			var isFinal = fenceClosed || !MarkdownControl.GetIsStreaming(renderer);

			var infoComplete = block is NestedFencedCodeBlock { IsInfoComplete: true };

			if (!_initialized)
			{
				_control.Initialize(
					block,
					renderer,
					renderer.GetValue(MarkdownControl.OwnerProperty),
					renderer.GetValue(MarkdownControl.CompactCodeBlocksProperty));
				_initialized = true;
			}

			var ensureCreated = infoComplete || isFinal;
			_control.Update(
				block,
				ensureCreated,
				isFinal);

			return true;
		}
	}
}
