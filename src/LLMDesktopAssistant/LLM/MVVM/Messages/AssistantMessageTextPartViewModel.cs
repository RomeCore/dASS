using System.ComponentModel;
using Avalonia.Threading;
using LLMDesktopAssistant.LLM.Domain;

namespace LLMDesktopAssistant.LLM.MVVM.Messages
{
	[ViewModelFor(typeof(AssistantMessageTextPartView))]
	public class AssistantMessageTextPartViewModel : AssistantMessagePartViewModel
	{
		private DispatcherOperation? _currentUpdateOperation;

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

		public AssistantMessageTextPartViewModel()
		{
		}

		public AssistantMessageTextPartViewModel(AssistantMessage message)
		{
			Text = message.Content ?? string.Empty;

			if (!message.IsCompleted)
			{
				Completed = false;

				void PropertyChangedHandler(object? s, PropertyChangedEventArgs e)
				{
					_currentUpdateOperation = InvokeUIAsync(() =>
					{
						_currentUpdateOperation?.Abort();
						Text = message.Content ?? string.Empty;
					});
				}

				message.PropertyChanged += PropertyChangedHandler;
				message.CompletionToken.OnCompleted(() =>
				{
					InvokeUIAsync(() =>
					{
						_currentUpdateOperation?.Abort();
						_currentUpdateOperation = null;
						Completed = true;
						RaisePropertyChanged(nameof(NotCompleted));
					});

					message.PropertyChanged -= PropertyChangedHandler;
				});
			}
			else
			{
				Completed = true;
			}
		}
	}
}