using CommunityToolkit.Mvvm.Input;
using LLMDesktopAssistant.LLM.MVVM;
using LLMDesktopAssistant.LLM.MVVM.Additional;

namespace LLMDesktopAssistant.LLM.MVVM.Attachments
{
	[ViewModelFor(typeof(AttachmentView))]
	public class AttachmentViewModel : ViewModelBase
	{
		public UserInputViewModel Parent { get; }
		public AttachmentMessagePart Attachment { get; }

		public ICommand RemoveAttachmentCommand { get; }

		public AttachmentViewModel(UserInputViewModel parent, AttachmentMessagePart attachment)
		{
			Parent = parent;
			Attachment = attachment;

			RemoveAttachmentCommand = new RelayCommand(() =>
			{
				Parent.Attachments.Remove(this);
			});
		}
	}
}