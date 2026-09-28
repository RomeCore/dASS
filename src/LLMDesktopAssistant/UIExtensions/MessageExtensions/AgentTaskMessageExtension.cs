using Avalonia.Layout;
using LLMDesktopAssistant.Agents.Tasks.MVVM;
using LLMDesktopAssistant.LLM.Domain;
using LLMDesktopAssistant.LLM.MVVM.Additional;
using LLMDesktopAssistant.LLM.MVVM.Messages;

namespace LLMDesktopAssistant.UIExtensions.MessageExtensions
{
	/// <summary>
	/// A message extension that synchronizes <see cref="AssistantMessage.AgentTasks"/>
	/// with <see cref="ChatMessage.AdditionalViewModels"/>, displaying inline
	/// <see cref="AgentTaskViewModel"/> cards below the assistant message.
	/// </summary>
	/// <remarks>
	/// This extension is invisible (it does not produce a toolbar button) —
	/// its sole purpose is to subscribe to task changes and manage the
	/// additional view models automatically.
	/// </remarks>
	[MessageExtension(Targets = MessageExtensionTargets.Assistant)]
	public class AgentTaskMessageExtension : MessageExtension
	{
		private readonly AssistantMessage _message;

		/// <summary>
		/// Initializes a new instance of the <see cref="AgentTaskMessageExtension"/> class.
		/// </summary>
		/// <param name="viewModel">The assistant message view model.</param>
		public AgentTaskMessageExtension(MessageViewModelBase viewModel)
		{
			IsVisible = false; // No toolbar button — inline cards only

			_message = ((AssistantMessageViewModel)viewModel).AssistantMessage;
			var taskListVm = new AgentTaskListViewModel
			{
				Filtering = AgentTaskListFiltering.Message,
				Orientation = Orientation.Horizontal
			};
			taskListVm.BindToSource(_message.AgentTasks);
			var additional = new AgentTaskListInlineViewModel
			{
				TaskListViewModel = taskListVm
			};
			_message.AdditionalData.Add(additional);
		}
	}
}
