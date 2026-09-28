using System.Collections.ObjectModel;

namespace LLMDesktopAssistant.LLM.MVVM.Messages
{
	[ViewModelFor(typeof(AssistantMessageToolPartView))]
	public class AssistantMessageToolPartViewModel : AssistantMessagePartViewModel
	{
		private ObservableCollection<ToolCallViewModel> _toolCalls = [];
		public ObservableCollection<ToolCallViewModel> ToolCalls
		{
			get => _toolCalls;
			set => SetProperty(ref _toolCalls, value);
		}

		public AssistantMessageToolPartViewModel()
		{
		}

		protected override void Dispose(bool disposing)
		{
			base.Dispose(disposing);

			if (disposing)
			{
				foreach (var toolCall in ToolCalls)
					toolCall.Dispose();
				ToolCalls.Clear();
			}
		}
	}
}