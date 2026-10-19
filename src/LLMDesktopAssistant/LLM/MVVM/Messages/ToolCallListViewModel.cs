using System.Collections.Specialized;
using LLMDesktopAssistant.LLM.Domain;
using LLMDesktopAssistant.Utils;

namespace LLMDesktopAssistant.LLM.MVVM.Messages
{
	/// <summary>
	/// A message part that renders the message tool calls. Created eagerly for every message;
	/// its visibility is managed internally (visible only when there is at least one tool call).
	/// </summary>
	[ViewModelFor(typeof(ToolCallListView))]
	public class ToolCallListViewModel : MessagePartViewModel
	{
		private readonly ChatMessage _message;
		private readonly Chat _chat;

		private readonly RangeObservableCollection<ToolCallViewModel> _toolCalls = [];
		public RangeObservableCollection<ToolCallViewModel> ToolCalls => _toolCalls;

		public bool HasToolCalls => _toolCalls.Count > 0;

		private MessageAlignment _alignment = MessageAlignment.Left;
		/// <summary>
		/// Gets the alignment of the tool calls, derived from the role of the message that contains them.
		/// Set by the owning message view model; forwarded to every tool call, including the ones added later.
		/// </summary>
		public MessageAlignment Alignment
		{
			get => _alignment;
			init
			{
				_alignment = value;

				foreach (var toolCall in _toolCalls)
					toolCall.Alignment = value;
			}
		}

		public ToolCallListViewModel(ChatMessage message, Chat chat)
		{
			_message = message;
			_chat = chat;

			Sync();
			UpdateVisibility();

			message.ToolCalls.CollectionChanged += OnCollectionChanged;
		}

		private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
		{
			InvokeUI(() =>
			{
				Sync();
				UpdateVisibility();
				RaisePropertyChanged(nameof(HasToolCalls));
			});
		}

		/// <summary>
		/// Reconciles the view-model list with the domain tool calls (adds new, removes gone).
		/// </summary>
		private void Sync()
		{
			for (int i = _toolCalls.Count - 1; i >= 0; i--)
			{
				if (!_message.ToolCalls.Contains(_toolCalls[i].ToolCall))
				{
					_toolCalls[i].Dispose();
					_toolCalls.RemoveAt(i);
				}
			}

			foreach (var toolCall in _message.ToolCalls)
			{
				if (!_toolCalls.Any(vm => ReferenceEquals(vm.ToolCall, toolCall)))
					_toolCalls.Add(new ToolCallViewModel(toolCall, _message, _chat) { Alignment = _alignment });
			}
		}

		private void UpdateVisibility() => IsVisible = _toolCalls.Count > 0;

		protected override void Dispose(bool disposing)
		{
			base.Dispose(disposing);

			if (disposing)
			{
				_message.ToolCalls.CollectionChanged -= OnCollectionChanged;

				foreach (var toolCall in _toolCalls)
					toolCall.Dispose();
				_toolCalls.Clear();
			}
		}
	}
}
