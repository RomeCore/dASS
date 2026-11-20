using System.ComponentModel;
using System.Runtime.CompilerServices;
using CommunityToolkit.Mvvm.Input;
using LLMDesktopAssistant.Controls.Dialogs;
using LLMDesktopAssistant.LLM.Domain;
using LLMDesktopAssistant.LLM.MVVM.Additional;
using LLMDesktopAssistant.LLM.Services;
using LLMDesktopAssistant.Localization;
using LLMDesktopAssistant.Services.Instances;
using LLMDesktopAssistant.Utils;
using Material.Icons;
using Serilog;

namespace LLMDesktopAssistant.LLM.MVVM
{
	public class AvailableChatViewModel : NotifyPropertyChanged
	{
		public required int Id { get; init; }
		public bool IsTemporary => TemporaryScope != null;
		public IServiceScope? TemporaryScope { get; init; }
		public required ICommand DeleteCommand { get; init; }

		/// <summary>
		/// Gets the command that opens the edit dialog for a chat that is not open in any place.
		/// </summary>
		public required ICommand RenameCommand { get; init; }

		/// <summary>
		/// Gets the chips rendered under the title and the topic of this chat in the list.
		/// </summary>
		public required AdditionalMessagePartsViewModel Parts { get; init; }

		public string? Title
		{
			get;
			set => SetProperty(ref field, value);
		} = string.Empty;

		public string? Topic
		{
			get;
			set
			{
				if (SetProperty(ref field, value))
				{
					RaisePropertyChanged(nameof(CombinedTopic));
					RaisePropertyChanged(nameof(TopicColorHex));
					if (string.IsNullOrWhiteSpace(SubTopic))
						RaisePropertyChanged(nameof(SubTopicColorHex));
				}
			}
		} = string.Empty;
		public string TopicColorHex => ChatTopicHashColor.GetHex(Topic);

		public string? SubTopic
		{
			get;
			set
			{
				if (SetProperty(ref field, value))
				{
					RaisePropertyChanged(nameof(CombinedTopic));
					RaisePropertyChanged(nameof(SubTopicColorHex));
				}
			}
		} = string.Empty;
		public string SubTopicColorHex => string.IsNullOrWhiteSpace(SubTopic) ? TopicColorHex : ChatTopicHashColor.GetHex(SubTopic);

		public string? CombinedTopic => string.IsNullOrWhiteSpace(SubTopic) ? Topic : $"{Topic} · {SubTopic}";

		public bool Favorite
		{
			get;
			set => SetProperty(ref field, value);
		} = false;

		public bool HasUserInputDraft
		{
			get;
			set => SetProperty(ref field, value);
		} = false;

		public DateTime LastModifiedAt
		{
			get;
			set
			{
				if (SetProperty(ref field, value))
				{
					RaisePropertyChanged(nameof(LastModifiedLabel));
					RaisePropertyChanged(nameof(LastModifiedDetailedLabel));
				}
			}
		} = DateTime.Now;

		public string LastModifiedLabel
		{
			get
			{
				var today = DateTime.Today;

				if (LastModifiedAt.Date >= today.AddDays(-1))
					return LastModifiedAt.ToString("HH:mm");

				return LastModifiedAt.Year == today.Year
					? LastModifiedAt.ToString("d MMM")
					: LastModifiedAt.ToString("d MMM yyyy");
			}
		}

		public string LastModifiedDetailedLabel => LastModifiedAt.ToString("F");

		public bool IsOpened
		{
			get;
			set => SetProperty(ref field, value);
		} = false;
	}

	public class OpenedChatViewModel : NotifyPropertyChanged
	{
		private readonly IChatManagementService _manager;
		private readonly AvailableChatViewModel _available;
		private readonly IServiceScope _scope;
		private readonly IExecutionInfoStatusService? _statusService;
		private UserInputState? _subscribedUserInput;

		public AvailableChatViewModel Available => _available;

		public ChatViewModel CurrentChat { get; init; }

		public bool IsSelected
		{
			get;
			set
			{
				SetProperty(ref field, value);
			}
		} = false;

		public ICommand RenameCommand { get; }
		public ICommand RenameWithAICommand { get; }
		public required ICommand CloseCommand { get; init; }

		public OpenedChatViewModel(IChatManagementService manager, AvailableChatViewModel available)
		{
			_manager = manager;
			_available = available;
			_scope = available.TemporaryScope ?? _manager.OpenChatScope(_available.Id);

			var chatServices = _scope.ServiceProvider;
			var chat = chatServices.GetRequiredService<Chat>();
			CurrentChat = new ChatViewModel(chat);
			_statusService = chatServices.GetService<IExecutionInfoStatusService>();
			_statusService?.PropertyChanged += OnExecutionStatusServicePropertyChanged;

			_available.Parts.SetSource(chat.AdditionalData);
			_available.IsOpened = true;
			_subscribedUserInput = chat.UserInputState;
			_subscribedUserInput.DeepChanged += OnUserInputStateChanged;
			chat.PropertyChanged += OnChatPropertyChanged;

			RenameCommand = new AsyncRelayCommand(async () =>
			{
				var chat = CurrentChat.Chat;
				if (chat is null)
					return;

				var dialog = new EditChatDialogViewModel
				{
					ChatTitle = chat.Title,
					ChatTopic = chat.Topic,
					ChatSubTopic = chat.SubTopic
				};

				if (await DialogManager.ShowDialogAsync(dialog) is not ChatEditResult result)
					return;

				chat.Title = result.Title;
				chat.Topic = result.Topic;
				chat.SubTopic = result.SubTopic;
			});

			RenameWithAICommand = new AsyncRelayCommand(async () =>
			{
				try
				{
					if (CurrentChat.Chat.Messages.Count == 0)
					{
						var toastService = CurrentChat.Chat.Services.GetService<IToastService>();
						toastService?.ShowWarning(Locale.Get("chat.manager.no_messages_to_rename.title"),
							Locale.Get("chat.manager.no_messages_to_rename.description"));
						return;
					}

					var namingService = CurrentChat.Chat.Services.GetService<IChatNamingService>();
					if (namingService != null)
						await namingService.NameChatAsync();
				}
				catch (Exception ex)
				{
					Log.Error(ex, "Failed to rename chat with AI: {Error}", ex.Message);
				}
			});
		}

		protected override void Dispose(bool disposing)
		{
			base.Dispose(disposing);

			if (disposing)
			{
				_subscribedUserInput?.DeepChanged -= OnUserInputStateChanged;
				CurrentChat.Chat.PropertyChanged -= OnChatPropertyChanged;
				_statusService?.PropertyChanged -= OnExecutionStatusServicePropertyChanged;

				_available.Parts.SetSource(_available.IsTemporary ? [] : [.. _manager.GetChatParts(_available.Id)]);
				_available.IsOpened = false;

				foreach (var chatClose in _scope.ServiceProvider.GetServices<IChatClosingHandler>() ?? [])
					try
					{
						chatClose.OnChatClosing();
					}
					catch { }
				CurrentChat.Dispose();
				_scope.Dispose();
			}
		}

		private void OnChatPropertyChanged(object? sender, PropertyChangedEventArgs e)
		{
			var chat = (Chat)sender!;
			_available.Title = chat.Title;
			_available.Topic = chat.Topic;
			_available.SubTopic = chat.SubTopic;
			_available.Favorite = chat.Favorite;
			_available.HasUserInputDraft = chat.UserInputState?.IsEmpty == false;

			if (e.PropertyName is nameof(chat.UserInputState))
			{
				_subscribedUserInput?.DeepChanged -= OnUserInputStateChanged;
				_subscribedUserInput = chat.UserInputState;
				_subscribedUserInput!.DeepChanged += OnUserInputStateChanged;
			}
		}

		private void OnUserInputStateChanged(object? sender, EventArgs e)
		{
			var userInput = (UserInputState)sender!;
			_available.HasUserInputDraft = userInput.IsEmpty == false;
		}

		private void OnExecutionStatusServicePropertyChanged(object? sender, PropertyChangedEventArgs e)
		{
			if (e.PropertyName == nameof(IExecutionInfoStatusService.Status))
			{
				RaisePropertyChanged(nameof(StatusIcon));
				RaisePropertyChanged(nameof(HasStatus));
				RaisePropertyChanged(nameof(StatusColorHex));
			}
		}

		/// <summary>
		/// The status icon to display on the chat tab based on the current chat status.
		/// </summary>
		public VisualIconKind StatusIcon => _statusService?.Status switch
		{
			ChatStatus.Executing => MaterialIconKind.TimerSand,
			ChatStatus.Confirming => MaterialIconKind.QuestionMarkCircle,
			_ => MaterialIconKind.ChatProcessing
		};

		/// <summary>
		/// Whether the chat tab should display a status icon.
		/// </summary>
		public bool HasStatus => _statusService?.Status != ChatStatus.Idle;

		/// <summary>
		/// The background color hex of the chat tab based on the current chat status.
		/// </summary>
		public string StatusColorHex => _statusService?.Status switch
		{
			ChatStatus.Executing => "#3342A5F5",
			ChatStatus.Confirming => "#33FFC107",
			_ => "#00000000"
		};

		// -TO-DO-: Удалить это говно внизу когда автор LiveMarkdown.Avalonia соизволит рассмотреть мой Pull Request
		// Утечки памяти вызваны Link.TagToLinkMap, который нихуя не чистится, я его там заменил на ConcurrentDict<WeakReference>
		// Ждем
		// PS: ПОЧИНЕНО! (на самом деле я не делал никаких PR по этим утечкам, я просто сделал PR,
		// который добавляет возможность удалять built-in ноды, а утечки сам автор починил, приколист ебаный)
	}

	public class ChatListEntryCommand
	{
		public required VisualIconKind Icon { get; init; }
		public required LocaleKeyBase ToolTip { get; init; }
		public required ICommand Command { get; init; }
	}

	public class ChatListBaseEntry : NotifyPropertyChanged
	{
		public virtual AvailableChatViewModel? Available => null;
		public virtual OpenedChatViewModel? Opened => null;
		public virtual ImmutableList<ChatListEntryCommand> Commands { get; } = [];
		public virtual bool CanBeSelected => true;
	}

	public class ChatListCaptionEntry : ChatListBaseEntry
	{
		public override bool CanBeSelected => false;
		public required LocaleKeyBase Caption { get; init; }
	}

	public class ChatListAvailableEntry : ChatListBaseEntry
	{
		public override AvailableChatViewModel Available => Chat;
		public required AvailableChatViewModel Chat { get; init; }

		public override ImmutableList<ChatListEntryCommand> Commands => field ??= [
			new ChatListEntryCommand
			{
				Icon = MaterialIconKind.Pencil,
				Command = Chat.RenameCommand,
				ToolTip = Locale.GetKey("common.rename")
			},
			new ChatListEntryCommand
			{
				Icon = MaterialIconKind.Delete,
				Command = Chat.DeleteCommand,
				ToolTip = Locale.GetKey("common.delete")
			}
		];
	}

	public class ChatListOpenedEntry : ChatListBaseEntry
	{
		public override AvailableChatViewModel Available => Chat.Available;
		public override OpenedChatViewModel? Opened => Chat;
		public required OpenedChatViewModel Chat { get; init; }

		public override ImmutableList<ChatListEntryCommand> Commands => field ??= [
			new ChatListEntryCommand
			{
				Icon = MaterialIconKind.Pencil,
				Command = Chat.RenameCommand,
				ToolTip = Locale.GetKey("common.rename")
			},
			new ChatListEntryCommand
			{
				Icon = MaterialIconKind.AutoFix,
				Command = Chat.RenameWithAICommand,
				ToolTip = Locale.GetKey("chat.rename_with_ai")
			},
			new ChatListEntryCommand
			{
				Icon = MaterialIconKind.Close,
				Command = Chat.CloseCommand,
				ToolTip = Locale.GetKey("common.close")
			}
		];
	}

	[ViewModelFor(typeof(ChatManagerView))]
	public class ChatManagerViewModel : ViewModelBase
	{
		public IChatManagementService ChatManager { get; }

		private readonly ConditionalWeakTable<AvailableChatViewModel, ChatListBaseEntry> _availableEntries = [];
		private static ChatListBaseEntry AvailableFactory(AvailableChatViewModel chat) => new ChatListAvailableEntry { Chat = chat };
		private readonly ConditionalWeakTable<OpenedChatViewModel, ChatListBaseEntry> _openedEntries = [];
		private static ChatListBaseEntry OpenedFactory(OpenedChatViewModel chat) => new ChatListOpenedEntry { Chat = chat };

		public RangeObservableCollection<AvailableChatViewModel> AvailableChats { get; } = [];
		public RangeObservableCollection<OpenedChatViewModel> OpenedChats { get; } = [];
		public RangeObservableCollection<ChatListBaseEntry> ChatListEntries { get; } = [];

		/// <summary>
		/// Gets or sets the chat list filter, matched against the title, the topic and the sub-topic.
		/// </summary>
		public string SearchText
		{
			get;
			set
			{
				if (SetProperty(ref field, value))
					RebuildChatList();
			}
		} = string.Empty;

		/// <summary>
		/// Gets or sets the flag indicating whether only favorite chats should be shown in the chat list.
		/// </summary>
		public bool FilterFavoritesOnly
		{
			get;
			set
			{
				if (SetProperty(ref field, value))
					RebuildChatList();
			}
		} = false;

		/// <summary>
		/// Gets or sets a value indicating whether the chat list column is shown.
		/// </summary>
		public bool IsChatListVisible
		{
			get;
			set
			{
				SetProperty(ref field, value);
			}
		} = false;

		/// <summary>
		/// Gets the command that collapses or expands the chat list.
		/// </summary>
		public ICommand ToggleChatListCommand { get; }

		public ChatListBaseEntry? SelectedChat
		{
			get;
			set
			{
				var prevChat = field;
				if (SetProperty(ref field, value))
				{
					if (prevChat is ChatListOpenedEntry opPrev)
						opPrev.Chat.IsSelected = false;
					if (field is ChatListOpenedEntry opNew)
						opNew.Chat.IsSelected = true;
					if (field is ChatListAvailableEntry avNew)
						SelectChat(avNew.Chat);
				}
			}
		}

		public ICommand CreateChatCommand { get; }

		public ICommand CreateTemporaryChatCommand { get; }

		public ChatManagerViewModel(IChatManagementService chatManager)
		{
			ChatManager = chatManager;
			CreateChatCommand = new RelayCommand(CreateChat);
			CreateTemporaryChatCommand = new RelayCommand(CreateTemporaryChat);
			ToggleChatListCommand = new RelayCommand(() => IsChatListVisible = !IsChatListVisible);

			ChatManager.ClearEmptyChats();
			LoadChats();
			CreateChat();
		}

		protected override void Dispose(bool disposing)
		{
			base.Dispose(disposing);

			if (disposing)
			{
				ChatListEntries.Clear();
				AvailableChats.Clear();
				foreach (var chat in OpenedChats)
				{
					try
					{
						chat.Dispose();
					} catch { }
				}
				OpenedChats.Clear();
			}
		}

		private AvailableChatViewModel CreateAvailableChatViewModel(ChatInfo info)
		{
			AvailableChatViewModel newAvailableChatViewModel = null!;

			var renameCommand = new AsyncRelayCommand(async () =>
			{
				var dialog = new EditChatDialogViewModel
				{
					ChatTitle = newAvailableChatViewModel.Title,
					ChatTopic = newAvailableChatViewModel.Topic,
					ChatSubTopic = newAvailableChatViewModel.SubTopic
				};

				if (await DialogManager.ShowDialogAsync(dialog) is not ChatEditResult result)
					return;

				ChatManager.UpdateChat(newAvailableChatViewModel.Id, result.Title, result.Topic, result.SubTopic);

				newAvailableChatViewModel.Title = result.Title;
				newAvailableChatViewModel.Topic = result.Topic;
				newAvailableChatViewModel.SubTopic = result.SubTopic;
			});

			var deleteCommand = new AsyncRelayCommand(async () =>
			{
				var confirm = new ConfirmDialogViewModel
				{
					Title = LocalizationManager.LocalizeStatic("chat.delete.title"),
					Description = LocalizationManager.LocalizeStatic("chat.delete.description"),
					ConfirmText = LocalizationManager.LocalizeStatic("common.delete"),
					CancelText = LocalizationManager.LocalizeStatic("common.cancel"),
					IsDanger = true
				};

				if (await DialogManager.ShowDialogAsync(confirm) is not true)
					return;

				var openedChats = OpenedChats.Where(o => o.Available.Id == info.Id).ToList();
				foreach (var openedChat in openedChats)
				{
					OpenedChats.Remove(openedChat);
					openedChat.Dispose();
				}
				ChatManager.DeleteChat(info.Id);
				newAvailableChatViewModel.PropertyChanged -= OnAvailableChatPropertyChanged;
				AvailableChats.Remove(newAvailableChatViewModel);
				RebuildChatList();

				if (OpenedChats.Count == 0)
					CreateChat();
			});

			newAvailableChatViewModel = new AvailableChatViewModel
			{
				Id = info.Id,
				DeleteCommand = deleteCommand,
				RenameCommand = renameCommand,
				Title = info.Title,
				Topic = info.Topic,
				SubTopic = info.SubTopic,
				Favorite = info.Favorite,
				HasUserInputDraft = info.HasUserInputDraft,
				LastModifiedAt = info.LastModifiedAt,
				Parts = new AdditionalMessagePartsViewModel([.. info.Parts])
			};

			newAvailableChatViewModel.PropertyChanged += OnAvailableChatPropertyChanged;

			return newAvailableChatViewModel;
		}

		private void LoadChats()
		{
			AvailableChats.Clear();
			foreach (var chat in ChatManager.GetChats().OrderByDescending(c => c.LastModifiedAt))
				AvailableChats.Add(CreateAvailableChatViewModel(chat));

			RebuildChatList();
		}

		private IEnumerable<AvailableChatViewModel> FilterChats(IEnumerable<AvailableChatViewModel> chats)
		{
			if (FilterFavoritesOnly)
				chats = chats.Where(chat => chat.Favorite);

			if (!string.IsNullOrEmpty(SearchText))
				chats = chats.Where(chat => Matches(chat, SearchText));

			return chats;
		}

		private static string ClassifyDate(DateTime chatDate, DateTime today)
		{
			if (chatDate == today)
				return "chat.list.today";
			if (chatDate == today.AddDays(-1))
				return "chat.list.yesterday";
			if (chatDate >= today.AddDays(-7))
				return "chat.list.last_7_days";
			if (chatDate >= today.AddDays(-30))
				return "chat.list.last_30_days";
			if (chatDate >= today.AddDays(-365))
				return "chat.list.last_year";
			return "chat.list.earlier";
		}

		private void RebuildChatList()
		{
			var prevSelected = SelectedChat;
			bool prevFound = false;

			var newEntries = new List<ChatListBaseEntry>();
			var chats = FilterChats(AvailableChats
				.Where(chat => !chat.IsOpened && !chat.IsTemporary))
				.OrderByDescending(chat => chat.LastModifiedAt)
				.ToList();

			var today = DateTime.Today;
			string? currentGroup = null;

			if (OpenedChats.Count > 0)
			{
				newEntries.Add(new ChatListCaptionEntry { Caption = Locale.GetKey("chat.list.open") });

				foreach (var chat in OpenedChats)
				{
					var entry = _openedEntries.GetOrAdd(chat, OpenedFactory);
					if (entry == prevSelected)
						prevFound = true;
					newEntries.Add(entry);
				}
			}

			foreach (var chat in chats)
			{
				var group = ClassifyDate(chat.LastModifiedAt.Date, today);

				if (group != currentGroup)
				{
					currentGroup = group;
					newEntries.Add(new ChatListCaptionEntry { Caption = Locale.GetKey(group) });
				}

				var entry = _availableEntries.GetOrAdd(chat, AvailableFactory);
				if (entry == prevSelected)
					prevFound = true;
				newEntries.Add(entry);
			}

			ChatListEntries.Reset(newEntries);
			if (prevFound)
				SelectedChat = prevSelected;
		}

		private static bool Matches(AvailableChatViewModel chat, string search)
			=> (chat.Title?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false)
			|| (chat.Topic?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false)
			|| (chat.SubTopic?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false);

		private void OnAvailableChatPropertyChanged(object? sender, PropertyChangedEventArgs e)
		{
			var chat = (AvailableChatViewModel)sender!;
			if (e.PropertyName is nameof(AvailableChatViewModel.LastModifiedAt))
			{
				RebuildChatList();
			}
			if (e.PropertyName is nameof(AvailableChatViewModel.Favorite))
			{
				ChatManager.UpdateChat(chat.Id, favorite: chat.Favorite);
			}
		}

		/// <summary>
		/// Opens a chat from the list: an already opened one is focused, a closed one opens in a new
		/// place instead of replacing the conversation that is on screen.
		/// </summary>
		private void SelectChat(AvailableChatViewModel chat)
		{
			var opened = OpenedChats.FirstOrDefault(o => o.Available == chat);
			if (opened is not null && _openedEntries.TryGetValue(opened, out var openedVm))
			{
				SelectedChat = openedVm;
				IsChatListVisible = false;
				return;
			}

			OpenChat(chat);
		}

		private void OpenChat(AvailableChatViewModel chat)
		{
			OpenedChatViewModel newOpenedChat = null!;
			newOpenedChat = new OpenedChatViewModel(ChatManager, chat)
			{
				CloseCommand = new RelayCommand(() =>
				{
					OpenedChats.Remove(newOpenedChat);
					newOpenedChat.Dispose();

					if (OpenedChats.Count == 0)
						CreateChat();
					else
					{
						RebuildChatList();
						if (SelectedChat is ChatListOpenedEntry selectedOpenedChat && selectedOpenedChat.Chat == newOpenedChat)
							SelectedChat = _openedEntries.GetOrAdd(OpenedChats[0], OpenedFactory);
					}
				})
			};
			OpenedChats.Add(newOpenedChat);
			RebuildChatList();
			SelectedChat = _openedEntries.GetOrAdd(newOpenedChat, OpenedFactory);
		}

		private void CreateChat()
		{
			var newChat = ChatManager.CreateChat(LocalizationManager.LocalizeStatic("chat.new"));
			var newAvailableChat = CreateAvailableChatViewModel(newChat);
			AvailableChats.Insert(0, newAvailableChat);
			OpenChat(newAvailableChat);
			IsChatListVisible = false;
		}

		private void CreateTemporaryChat()
		{
			var chatScope = ChatManager.OpenMemoryChat(LocalizationManager.LocalizeStatic("chat.new_temporary"));
			var chat = chatScope.ServiceProvider.GetRequiredService<Chat>();
			var newAvailableChat = new AvailableChatViewModel
			{
				Id = chat.Id,
				TemporaryScope = chatScope,
				DeleteCommand = new RelayCommand(() => { }, () => false),
				RenameCommand = new RelayCommand(() => { }, () => false),
				Title = chat.Title,
				Topic = chat.Topic,
				LastModifiedAt = DateTime.Now,
				Parts = new AdditionalMessagePartsViewModel(chat.AdditionalData)
			};
			AvailableChats.Insert(0, newAvailableChat);
			OpenChat(newAvailableChat);
			IsChatListVisible = false;
		}
	}
}
