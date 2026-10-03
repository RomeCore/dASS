using System.ComponentModel;
using Avalonia.Threading;
using LLMDesktopAssistant.LLM.Domain;

namespace LLMDesktopAssistant.LLM.MVVM.Messages
{
	[ViewModelFor(typeof(AssistantMessageTextPartView))]
	public class AssistantMessageTextPartViewModel : MessagePartViewModel
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

		private bool _renderMarkdown = true;
		/// <summary>
		/// Gets or sets a value indicating whether the text is rendered as Markdown
		/// (otherwise it is rendered as plaintext).
		/// </summary>
		public bool RenderMarkdown
		{
			get => _renderMarkdown;
			set => SetProperty(ref _renderMarkdown, value);
		}

		public AssistantMessageTextPartViewModel(AssistantMessage message)
		{
			Text = message.Content ?? string.Empty;
			UpdateVisibility();

			if (!message.IsCompleted)
			{
				Completed = false;

				void PropertyChangedHandler(object? s, PropertyChangedEventArgs e)
				{
					_currentUpdateOperation = InvokeUIAsync(() =>
					{
						_currentUpdateOperation?.Abort();
						Text = message.Content ?? string.Empty;
						UpdateVisibility();
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
						UpdateVisibility();
					});

					message.PropertyChanged -= PropertyChangedHandler;
				});
			}
			else
			{
				Completed = true;
			}
		}

		private void UpdateVisibility() => IsVisible = !string.IsNullOrEmpty(Text);
	}
}
