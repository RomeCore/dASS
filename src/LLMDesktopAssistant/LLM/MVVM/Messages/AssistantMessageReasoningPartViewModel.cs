using System.Collections.Specialized;
using System.ComponentModel;
using Avalonia.Threading;
using LLMDesktopAssistant.LLM.Domain;

namespace LLMDesktopAssistant.LLM.MVVM.Messages
{
	[ViewModelFor(typeof(AssistantMessageReasoningPartView))]
	public class AssistantMessageReasoningPartViewModel : MessagePartViewModel
	{
		private readonly AssistantMessage _assistantMessage;
		private DispatcherOperation? _currentUpdateOperation;

		public AssistantMessageViewModel Owner { get; }

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

		public AssistantMessageReasoningPartViewModel(AssistantMessageViewModel owner)
		{
			Owner = owner;
			_assistantMessage = Owner.AssistantMessage;
			_assistantMessage.ToolCalls.CollectionChanged += ToolCallsChanged;
			_assistantMessage.PropertyChanged += PropertyChangedHandler;
			ReasoningText = _assistantMessage.ReasoningContent ?? string.Empty;
			UpdateVisibility();

			Completed = _assistantMessage.IsCompleted;
		}

		protected override void Dispose(bool disposing)
		{
			base.Dispose(disposing);

			if (disposing)
			{
				_assistantMessage.ToolCalls.CollectionChanged -= ToolCallsChanged;
				_assistantMessage.PropertyChanged -= PropertyChangedHandler;
			}
		}

		void ToolCallsChanged(object? s, NotifyCollectionChangedEventArgs e)
		{
			Completed = true;
		}
		void PropertyChangedHandler(object? s, PropertyChangedEventArgs e)
		{
			if (e.PropertyName == nameof(AssistantMessage.Status) && _assistantMessage.Status.IsTerminal())
			{
				_currentUpdateOperation?.Abort();
				_currentUpdateOperation = null;
				Completed = true;
				InvokeUIAsync(UpdateVisibility);
				return;
			}

			Completed = e.PropertyName != nameof(_assistantMessage.ReasoningContent);

			_currentUpdateOperation?.Abort();
			_currentUpdateOperation = InvokeUIAsync(() =>
			{
				ReasoningText = _assistantMessage.ReasoningContent ?? string.Empty;
				UpdateVisibility();
			});
		}

		private void UpdateVisibility() => IsVisible = !string.IsNullOrEmpty(ReasoningText);
	}
}
