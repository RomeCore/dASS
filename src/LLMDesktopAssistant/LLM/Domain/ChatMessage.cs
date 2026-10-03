using LLMDesktopAssistant.Agents.Tasks;
using LLMDesktopAssistant.LLM.MVVM.Additional;
using LLMDesktopAssistant.Utils;
using LLTSharp;
using RCLargeLanguageModels.Messages.Attachments;

namespace LLMDesktopAssistant.LLM.Domain
{
	/// <summary>
	/// Represents a base class for chat messages.
	/// </summary>
	public abstract class ChatMessage : ChatObjectBase
	{
		/// <summary>
		/// Gets or sets the content of the message.
		/// </summary>
		public string Content
		{
			get => field;
			set => SetProperty(ref field, value);
		} = string.Empty;

		// Yeah, even the user can call tools!
		/// <summary>
		/// The collection of tool calls associated with this message.
		/// </summary>
		public RangeObservableCollection<ToolCall> ToolCalls
		{
			get => field ??= [];
			set => (field ??= []).Reset(value);
		}

		/// <summary>
		/// Gets the collection of agent tasks associated with this message.
		/// </summary>
		public RangeObservableCollection<AgentTask> AgentTasks
		{
			get => field ??= [];
			set => (field ??= []).Reset(value);
		}

		protected override void Dispose(bool disposing)
		{
			base.Dispose(disposing);

			if (disposing)
			{
				foreach (var toolCall in ToolCalls)
					toolCall.Dispose();
			}
		}
	}
}