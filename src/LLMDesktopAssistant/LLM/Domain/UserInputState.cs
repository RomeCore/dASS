using System.Text.Json.Serialization;
using LiteDB;
using LLMDesktopAssistant.LLM.MVVM.Additional;
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
		private string _text = string.Empty;
		/// <summary>
		/// Gets or sets the text that the user is currently composing.
		/// </summary>
		public string Text
		{
			get => _text;
			set => SetProperty(ref _text, value);
		}

		private string _senderLogin = "user";
		/// <summary>
		/// Gets or sets the login of the user that sends the next message.
		/// </summary>
		public string SenderLogin
		{
			get => _senderLogin;
			set => SetProperty(ref _senderLogin, value);
		}

		private MessageVisibility _visibility = MessageVisibility.Always;
		/// <summary>
		/// Gets or sets the visibility of the next user message.
		/// </summary>
		public MessageVisibility Visibility
		{
			get => _visibility;
			set => SetProperty(ref _visibility, value);
		}

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
		/// Gets a value indicating whether the input state is empty (nothing to send).
		/// </summary>
		[BsonIgnore]
		[JsonIgnore]
		[YamlIgnore]
		public bool IsEmpty => string.IsNullOrWhiteSpace(Text) && Parts.Count == 0;

		/// <summary>
		/// Clears the text and parts, resetting the state to default values.
		/// </summary>
		public void Clear()
		{
			Text = string.Empty;
			Parts.Clear();
			Visibility = MessageVisibility.Always;
		}
	}
}
