using System.Collections.Specialized;
using Avalonia.Media.Imaging;
using CommunityToolkit.Mvvm.Input;
using LLMDesktopAssistant.LLM.Domain;
using LLMDesktopAssistant.LLM.MVVM.Additional;
using LLMDesktopAssistant.UIExtensions.MessageExtensions;
using LLMDesktopAssistant.Users;
using LLMDesktopAssistant.Utils;

namespace LLMDesktopAssistant.LLM.MVVM.Messages
{
	[ViewModelFor(typeof(UserMessageView))]
	public class UserMessageViewModel : MessageViewModelBase
	{
		private readonly UserMessage _userMessage;
		public UserMessage UserMessage => _userMessage;

		public bool ShowAvatar { get; }
		public Bitmap? SenderAvatar { get; }
		public string? SenderName { get; }

		private string _text = string.Empty;
		public string Text
		{
			get => _text;
			set => SetProperty(ref _text, value);
		}

		private readonly RangeObservableCollection<AttachmentMessagePart> _attachments = [];
		/// <summary>
		/// The attachment parts of the user message, taken from <see cref="UserMessage.AdditionalViewModels"/>.
		/// </summary>
		public ICollection<AttachmentMessagePart> Attachments => _attachments;

		private void RefreshAttachments()
		{
			_attachments.Reset(UserMessage.AdditionalData.GetAll<AttachmentMessagePart>());
		}

		public ImmutableList<MessageExtension> Extensions { get; }

		public ICommand EditCommand { get; }

		public UserMessageViewModel(BranchedMessage branchedMessage, ChatViewModel chatVM) : base(branchedMessage, chatVM)
		{
			if (branchedMessage.Message is not UserMessage userMessage)
				throw new InvalidOperationException("Invalid message type. Expected IUserMessage.");
			_userMessage = userMessage;

			// Determine that we can apply avatar
			var prevMessage = branchedMessage.MessageIndex - 1 >= 0
				? chatVM.Chat.Messages[branchedMessage.MessageIndex - 1].Message as UserMessage
				: null;
			if (prevMessage == null || userMessage.SenderLogin != prevMessage.SenderLogin)
			{
				var userManager = chatVM.Chat.Services.GetRequiredService<IUserManagementService>();
				var user = userManager.FindByLogin(userMessage.SenderLogin);

				try
				{
					if (!string.IsNullOrWhiteSpace(user?.Base64ProfileImage))
					{
						var bytes = Convert.FromBase64String(user.Base64ProfileImage);
						using var ms = new MemoryStream(bytes);
						SenderAvatar = new Bitmap(ms);
					}
				}
				catch
				{
					SenderAvatar = null;
				}
				SenderName = user?.Name ?? userMessage.SenderLogin;
				ShowAvatar = true;
			}

			Text = userMessage.Content ?? string.Empty;
			RefreshAttachments();
			userMessage.AdditionalData.CollectionChanged += AdditionalViewModels_CollectionChanged;
			Extensions = MessageExtensionManager.CreateExtensions(this, chatVM.Chat);

			EditCommand = new RelayCommand(() =>
			{
				chatVM.UserInput.EditMessage(branchedMessage);
			});
		}

		private void AdditionalViewModels_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
		{
			RefreshAttachments();
		}

		protected override void Dispose(bool disposing)
		{
			base.Dispose(disposing);

			if (disposing)
			{
				_userMessage.AdditionalData.CollectionChanged -= AdditionalViewModels_CollectionChanged;
				foreach (var extension in Extensions)
					extension.Dispose();
			}
		}
	}
}