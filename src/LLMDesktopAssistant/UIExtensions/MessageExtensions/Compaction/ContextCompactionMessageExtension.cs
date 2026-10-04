using LLMDesktopAssistant.LLM.MVVM.Messages;

namespace LLMDesktopAssistant.UIExtensions.MessageExtensions.Compaction
{
	/// <summary>
	/// Adds a single segmented panel to the message toolbar that controls
	/// the context checkpoint (compaction) kinds: context shield, summary,
	/// tool results compaction, forced tool results compaction and reasoning compaction.
	/// </summary>
	[MessageExtension(Targets = MessageExtensionTargets.Both)]
	public class ContextCompactionMessageExtension : MessageExtension
	{
		public override int Order => 50;

		private readonly ContextCompactionViewModel _viewModel;

		public ContextCompactionMessageExtension(MessageViewModelBase viewModel)
		{
			_viewModel = new ContextCompactionViewModel(viewModel);
			ViewModel = _viewModel;
		}

		protected override void Dispose(bool disposing)
		{
			base.Dispose(disposing);

			if (disposing)
				_viewModel.Dispose();
		}
	}
}
