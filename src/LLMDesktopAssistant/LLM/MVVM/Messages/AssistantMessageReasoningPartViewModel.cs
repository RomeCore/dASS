using System.Collections.Specialized;
using System.ComponentModel;
using Avalonia.Threading;
using LLMDesktopAssistant.LLM.Domain;

namespace LLMDesktopAssistant.LLM.MVVM.Messages
{
	[ViewModelFor(typeof(AssistantMessageReasoningPartView))]
	public class AssistantMessageReasoningPartViewModel : MessagePartViewModel
	{
		private DispatcherOperation? _currentUpdateOperation;

		private string _reasoningText = string.Empty;
		public string ReasoningText
		{
			get => _reasoningText;
			set => SetProperty(ref _reasoningText, value);
		}

		private bool _completed = true;
		public bool Completed
		{
			get => _completed;
			set => SetProperty(ref _completed, value);
		}

		private bool _renderMarkdown = true;
		/// <summary>
		/// Gets or sets a value indicating whether the reasoning is rendered as Markdown
		/// (otherwise it is rendered as plaintext).
		/// </summary>
		public bool RenderMarkdown
		{
			get => _renderMarkdown;
			set => SetProperty(ref _renderMarkdown, value);
		}

		public AssistantMessageReasoningPartViewModel(AssistantMessage message)
		{
			ReasoningText = message.ReasoningContent ?? string.Empty;
			UpdateVisibility();

			Completed = message.IsCompleted;
			if (message.IsCompleted) return;

			void ToolCallsChanged(object? s, NotifyCollectionChangedEventArgs e)
			{
				Completed = true;
			}
			void PropertyChangedHandler(object? s, PropertyChangedEventArgs e)
			{
				Completed = e.PropertyName != nameof(message.ReasoningContent);
				if (Completed)
					return;

				_currentUpdateOperation?.Abort();
				_currentUpdateOperation = InvokeUIAsync(() =>
				{
					ReasoningText = message.ReasoningContent ?? string.Empty;
					UpdateVisibility();
				});
			}

			message.ToolCalls.CollectionChanged += ToolCallsChanged;
			message.PropertyChanged += PropertyChangedHandler;
			message.CompletionToken.OnCompleted(() =>
			{
				InvokeUIAsync(() =>
				{
					_currentUpdateOperation?.Abort();
					_currentUpdateOperation = null;
					Completed = true;
					UpdateVisibility();
				});

				message.ToolCalls.CollectionChanged -= ToolCallsChanged;
				message.PropertyChanged -= PropertyChangedHandler;
			});
		}

		private void UpdateVisibility() => IsVisible = !string.IsNullOrEmpty(ReasoningText);
	}
}
