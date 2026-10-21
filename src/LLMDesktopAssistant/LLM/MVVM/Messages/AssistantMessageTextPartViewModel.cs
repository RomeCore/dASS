using System.ComponentModel;
using Avalonia.Threading;
using LLMDesktopAssistant.LLM.Domain;

namespace LLMDesktopAssistant.LLM.MVVM.Messages
{
	[ViewModelFor(typeof(AssistantMessageTextPartView))]
	public class AssistantMessageTextPartViewModel : MessagePartViewModel
	{
		private readonly AssistantMessage _assistantMessage;
		private DispatcherOperation? _currentUpdateOperation;

		public AssistantMessageViewModel Owner { get; }

		private string _text = string.Empty;
		public string Text
		{
			get => _text;
			set => SetProperty(ref _text, value);
		}

		private bool _completed = true;
		public bool Completed
		{
			get => _completed;
			set => SetProperty(ref _completed, value);
		}
		public bool NotCompleted => !_completed;

		public AssistantMessageTextPartViewModel(AssistantMessageViewModel owner)
		{
			Owner = owner;
			_assistantMessage = Owner.AssistantMessage;
			_assistantMessage.PropertyChanged += PropertyChangedHandler;
			Text = _assistantMessage.Content ?? string.Empty;
			UpdateVisibility();

			Completed = _assistantMessage.IsCompleted;
		}

		protected override void Dispose(bool disposing)
		{
			base.Dispose(disposing);

			if (disposing)
			{
				_assistantMessage.PropertyChanged -= PropertyChangedHandler;
			}
		}

		void PropertyChangedHandler(object? s, PropertyChangedEventArgs e)
		{
			if (e.PropertyName == nameof(AssistantMessage.Status) && _assistantMessage.Status.IsTerminal())
			{
				_currentUpdateOperation?.Abort();
				_currentUpdateOperation = null;
				Completed = true;
				RaisePropertyChanged(nameof(NotCompleted));
				UpdateVisibility();
				return;
			}

			_currentUpdateOperation = InvokeUIAsync(() =>
			{
				_currentUpdateOperation?.Abort();
				Text = _assistantMessage.Content ?? string.Empty;
				UpdateVisibility();
			});
		}

		private void UpdateVisibility() => IsVisible = !string.IsNullOrEmpty(Text);
	}
}
