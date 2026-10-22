using System.Globalization;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using LLMDesktopAssistant.Controls.Icons;
using LLMDesktopAssistant.LLM.Domain;
using LLMDesktopAssistant.LLM.MVVM;
using LLMDesktopAssistant.LLM.MVVM.Additional;
using LLMDesktopAssistant.Localization;
using LLMDesktopAssistant.Services;
using Material.Icons;

namespace LLMDesktopAssistant.Controls.Markdown.Selection
{
	/// <summary>
	/// Copies the selected text to the clipboard as plain text - the same text the renderer's own
	/// <c>Ctrl+C</c> produces, taken from the snapshot instead of the live selection.
	/// </summary>
	public sealed class CopySelectionProcessor : IMarkdownSelectionProcessor
	{
		/// <summary>
		/// The single stateless instance, safe to share between every message.
		/// </summary>
		public static CopySelectionProcessor Instance { get; } = new();

		private CopySelectionProcessor()
		{
		}

		/// <inheritdoc />
		public LocaleKeyBase Title => Locale.GetKey("message.selection.copy");

		/// <inheritdoc />
		public VisualIconKind Icon => MaterialIconKind.ContentCopy;

		/// <inheritdoc />
		public int Order => 0;

		/// <inheritdoc />
		public async Task ExecuteAsync(MarkdownSelectionContext context, CancellationToken cancellationToken = default)
		{
			if (string.IsNullOrEmpty(context.Text))
				return;

			if (TopLevel.GetTopLevel(context.Renderer)?.Clipboard is { } clipboard)
				await clipboard.SetTextAsync(context.Text);
		}
	}

	/// <summary>
	/// Registers the shared, context-free processors: the ones that make sense for any selection, on any surface.
	/// </summary>
	[Service(typeof(IMarkdownSelectionProcessorsProvider))]
	public sealed class DefaultSelectionProcessorsProvider : IMarkdownSelectionProcessorsProvider
	{
		/// <inheritdoc />
		public MarkdownSelectionProcessorScope Scope => MarkdownSelectionProcessorScope.Default;

		/// <inheritdoc />
		public IReadOnlyList<IMarkdownSelectionProcessor> GetProcessors(MarkdownSelectionContext context)
			=> [CopySelectionProcessor.Instance];
	}

	/// <summary>
	/// Quotes the selection into the chat input as an <see cref="AdditionalMessageContentPart"/>: a chip above the
	/// input whose content is contributed to the model-facing message when the draft is sent.
	/// </summary>
	/// <remarks>
	/// The processor captures the input and the source message rather than reading them from the selection
	/// context, so the context stays a pure selection snapshot and the control stays ignorant of chats. That is
	/// also why it is composed by the message view model instead of being registered as a service: it is bound to
	/// one message in one chat.
	/// </remarks>
	public sealed class QuoteSelectionProcessor(UserInputViewModel userInput, Func<ChatMessage?> message)
		: IMarkdownSelectionProcessor
	{
		/// <inheritdoc />
		public LocaleKeyBase Title => Locale.GetKey("message.selection.quote");

		/// <inheritdoc />
		public VisualIconKind Icon => MaterialIconKind.FormatQuoteClose;

		/// <inheritdoc />
		public int Order => 10;

		/// <inheritdoc />
		public Task ExecuteAsync(MarkdownSelectionContext context, CancellationToken cancellationToken = default)
		{
			var quote = BuildQuote(context.Text, message());
			if (string.IsNullOrEmpty(quote))
				return Task.CompletedTask;

			userInput.AddPart(new AdditionalMessageContentPart
			{
				Content = quote,
				ChipIcon = MaterialIconKind.FormatQuoteClose,
				ChipTitle = Locale.GetKey("message.selection.quote_chip"),
			});

			return Task.CompletedTask;
		}

		/// <summary>
		/// Builds the quote text: an attribution line (author + localized short time) followed by the selected text
		/// as a markdown blockquote. The author is the sender's login for user messages and a localized role name
		/// for agent messages.
		/// </summary>
		private static string BuildQuote(string text, ChatMessage? message)
		{
			if (string.IsNullOrWhiteSpace(text) && message is null)
				return string.Empty;

			var builder = new StringBuilder();

			if (message is not null)
			{
				var author = message switch
				{
					UserMessage userMessage => userMessage.SenderLogin,
					AssistantMessage => Locale.Get("message.selection.author.assistant"),
					_ => Locale.Get("message.selection.author.unknown"),
				};

				builder.Append("> **").Append(author).Append("**, ")
					.Append(message.CreatedAt.ToString("t", CultureInfo.CurrentCulture))
					.AppendLine(":");
			}

			foreach (var line in text.Replace("\r\n", "\n").Split('\n'))
				builder.Append("> ").AppendLine(line);

			return builder.ToString().TrimEnd();
		}
	}
}
