using System.ComponentModel;
using Avalonia.Input.Platform;
using CommunityToolkit.Mvvm.Input;
using LLMDesktopAssistant.LLM.Domain;
using LLMDesktopAssistant.Localization;
using LLMDesktopAssistant.Utils;

namespace LLMDesktopAssistant.LLM.MVVM.Messages
{
	/// <summary>
	/// The view model backing the tool call flyout (arguments, result and copy commands).
	/// Created lazily by <see cref="ToolCallViewModel.Flyout"/>.
	/// </summary>
	[ViewModelFor(typeof(ToolCallFlyoutView))]
	public class ToolCallFlyoutViewModel : ViewModelBase
	{
		private readonly ToolCall _toolCall;

		public LocaleKeyBase ToolTitle { get; }
		public string ToolName { get; }
		public string ToolCallId { get; }

		private string _arguments = string.Empty;
		public string Arguments
		{
			get => _arguments;
			set => SetProperty(ref _arguments, value);
		}

		private string? _result = string.Empty;
		public string? Result
		{
			get => _result;
			set => SetProperty(ref _result, value);
		}

		private bool _useMarkdown = false;
		public bool UseMarkdown
		{
			get => _useMarkdown;
			set => SetProperty(ref _useMarkdown, value);
		}

		public ICommand CopyArgumentsCommand { get; }
		public ICommand CopyResultCommand { get; }

		public ToolCallFlyoutViewModel(ToolCallViewModel parent)
		{
			_toolCall = parent.ToolCall;
			ToolTitle = parent.ToolTitle;
			ToolName = parent.ToolName;
			ToolCallId = parent.ToolCallId;

			Arguments = FormatArguments(_toolCall.Arguments);
			Result = _toolCall.ResultContent;
			UseMarkdown = _toolCall.UseMarkdown;

			CopyArgumentsCommand = new RelayCommand(CopyArguments);
			CopyResultCommand = new RelayCommand(CopyResult);

			if (!_toolCall.IsCompleted)
			{
				void OnToolCallPropertyChanged(object? s, PropertyChangedEventArgs e)
				{
					InvokeUI(() =>
					{
						switch (e.PropertyName)
						{
							case nameof(ToolCall.Arguments): Arguments = FormatArguments(_toolCall.Arguments); break;
							case nameof(ToolCall.ResultContent): Result = _toolCall.ResultContent; break;
							case nameof(ToolCall.UseMarkdown): UseMarkdown = _toolCall.UseMarkdown; break;
						}
					});
				}

				_toolCall.PropertyChanged += OnToolCallPropertyChanged;
				_toolCall.CompletionToken.OnCompleted(() =>
				{
					_toolCall.PropertyChanged -= OnToolCallPropertyChanged;
				});
			}
		}

		private static string FormatArguments(string raw)
		{
			try
			{
				return ToolCallArgumentFormatter.FormatToMarkdown(TolerantJsonParser.Parse(raw));
			}
			catch
			{
				return "```json\n" + raw + "\n```";
			}
		}

		private void CopyArguments()
		{
			if (!string.IsNullOrEmpty(_toolCall.Arguments))
				App.MainTopLevel.Clipboard?.SetTextAsync(_toolCall.Arguments);
		}

		private void CopyResult()
		{
			if (!string.IsNullOrEmpty(Result))
				App.MainTopLevel.Clipboard?.SetTextAsync(Result);
		}
	}
}
