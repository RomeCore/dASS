using LLMDesktopAssistant.Agents.Tasks;
using LLMDesktopAssistant.LLM.MVVM.Additional;
using LLMDesktopAssistant.Localization;
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

		private LocaleKeyBase? _error;
		/// <summary>
		/// Gets or sets the error message associated with the message, if any.
		/// Runtime failures (generation, tools, commands) are attached here.
		/// </summary>
		/// <remarks>
		/// The error is a locale-backed key (or a const key for raw text) rather than a pre-rendered string, so it is
		/// localized at the point of display and stays correct after a language change.
		/// </remarks>
		public LocaleKeyBase? Error
		{
			get => _error;
			set => SetProperty(ref _error, value);
		}

		/// <summary>
		/// Gets or sets a value indicating whether the message is disabled for every agent: it stays in the
		/// transcript and in the UI but is never shown to an agent — not even to its own sender. Defaults to
		/// <see langword="false"/>.
		/// </summary>
		public bool IsDisabledForAgents
		{
			get;
			set => SetProperty(ref field, value);
		} = false;

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