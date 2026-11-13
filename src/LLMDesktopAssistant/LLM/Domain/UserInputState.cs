using System.Text.Json.Serialization;
using LiteDB;
using LLMDesktopAssistant.LLM.MVVM.Additional;
using LLMDesktopAssistant.Utils;
using YamlDotNet.Serialization;

namespace LLMDesktopAssistant.LLM.Domain
{
	/// <summary>
	/// Represents the persisted input state of the chat: the text that the user is composing
	/// and the message parts attached to it. Lives inside <see cref="Chat"/> (domain) and is
	/// persisted directly inside <see cref="Data.ChatModels.ChatModel"/>.
	/// </summary>
	public class UserInputState : NotifyPropertyChanged
	{
		/// <summary>
		/// Gets or sets the login of the user that sends the next message.
		/// </summary>
		public string SenderLogin
		{
			get;
			set => SetProperty(ref field, value);
		} = string.Empty;

		/// <summary>
		/// Gets or sets the text that the user is currently composing.
		/// </summary>
		public string Content
		{
			get;
			set => SetProperty(ref field, value);
		} = string.Empty;

		private readonly AdditionalChatDataCollection _parts = [];
		/// <summary>
		/// Gets or sets the reactive collection of message parts attached to the composed message.
		/// </summary>
		public AdditionalChatDataCollection Parts
		{
			get => _parts;
			set => _parts.Reset(value);
		}

		/// <summary>
		/// Gets or sets the visibility of the next user message.
		/// </summary>
		public MessageVisibility Visibility
		{
			get;
			set => SetProperty(ref field, value);
		} = MessageVisibility.Always;

		private readonly RangeObservableCollection<string> _visibleTo = [];
		/// <summary>
		/// Gets or sets the list of users (logins) or agents (guids) to whom the message is visible.
		/// If empty, it means that the message is visible to all.
		/// </summary>
		public RangeObservableCollection<string> VisibleTo
		{
			get => _visibleTo;
			set => _visibleTo.Reset(value);
		}

		/// <summary>
		/// Gets or sets a value indicating whether a <see cref="VisibleTo"/> is a whitelist filter.
		/// </summary>
		public bool IsVisibleToWhiteList
		{
			get;
			set => SetProperty(ref field, value);
		} = false;

		/// <summary>
		/// Gets or sets a value indicating whether this input may run a slash command. When <see langword="false"/> the content
		/// is inserted verbatim: no token extraction, no execution and no escape removal. Every producer sets it; the
		/// desktop input and the WebUI pass <see langword="true"/> until a participant-rights model exists.
		/// </summary>
		public bool CanExecuteCommand
		{
			get;
			set => SetProperty(ref field, value);
		} = true;

		/// <summary>
		/// Gets a value indicating whether the input state is empty (nothing to send).
		/// </summary>
		[BsonIgnore]
		[JsonIgnore]
		[YamlIgnore]
		public bool IsEmpty => string.IsNullOrWhiteSpace(Content) && Parts.Count == 0;

		/// <summary>
		/// Clears the text and parts.
		/// </summary>
		public void Clear()
		{
			Content = string.Empty;
			Parts.Clear();
		}

		/// <summary>
		/// Fills the current state from a message.
		/// </summary>
		public void FillFromMessage(UserMessage message)
		{
			SenderLogin = message.SenderLogin;
			Content = message.Content;
			Parts = [.. message.AdditionalData.GetRestorableParts()];
			Visibility = message.Visibility;
			VisibleTo = [.. message.VisibleTo];
			IsVisibleToWhiteList = message.IsVisibleToWhiteList;
			CanExecuteCommand = true;
		}

		/// <summary>
		/// Clones the current state to a new instance.
		/// </summary>
		public UserInputState Clone()
		{
			return new UserInputState
			{
				SenderLogin = SenderLogin,
				Content = Content,
				Parts = Parts,
				Visibility = Visibility,
				VisibleTo = VisibleTo,
				IsVisibleToWhiteList = IsVisibleToWhiteList,
				CanExecuteCommand = CanExecuteCommand
			};
		}
	}
}
