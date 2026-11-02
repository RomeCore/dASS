using System.Collections.Specialized;
using Avalonia.Media.Imaging;
using LLMDesktopAssistant.LLM.Domain;
using LLMDesktopAssistant.LLM.Services;
using LLMDesktopAssistant.Localization;
using LLMDesktopAssistant.Users;
using LLMDesktopAssistant.Utils;
using Material.Icons;

namespace LLMDesktopAssistant.LLM.MVVM
{
	/// <summary>
	/// One choice in the follow-up level selector: the level a queued message is composed with.
	/// </summary>
	public class UserInputLevelItemModel
	{
		/// <summary>
		/// Gets the level this choice selects.
		/// </summary>
		public required FollowUpInjectionLevel Level { get; init; }

		/// <summary>
		/// Gets the localized caption of the level, shown in the dropdown list only.
		/// </summary>
		public required LocaleKeyBase Title { get; init; }

		/// <summary>
		/// Gets the icon of the level, the only thing the collapsed combo shows.
		/// </summary>
		public required VisualIconKind Icon { get; init; }
	}

	/// <summary>
	/// One local user as the sender selector shows it: the login, the display name and the avatar.
	/// </summary>
	public class UserToolbarItemModel
	{
		/// <summary>
		/// Gets the login that is written into <see cref="UserInputState.SenderLogin"/>.
		/// </summary>
		public required string Login { get; init; }

		/// <summary>
		/// Gets the display name, shown in the dropdown list only.
		/// </summary>
		public required string Name { get; init; }

		/// <summary>
		/// Gets the avatar decoded from the user's profile image, or <see langword="null"/> when there is none.
		/// </summary>
		public Bitmap? Avatar { get; init; }

		/// <summary>
		/// Gets a value indicating whether the avatar should be shown instead of the fallback icon.
		/// </summary>
		public bool HasAvatar => Avatar is not null;
	}

	/// <summary>
	/// The right chat toolbar: sender, visibility and follow-up level selectors, plus the send/stop
	/// controls (delegated to the user input).
	/// <para/>
	/// The selectors follow one pattern: the collapsed combo shows an icon (or an avatar), and the dropdown list
	/// shows the icon together with the localized text.
	/// </summary>
	[ViewModelFor(typeof(ChatRightToolBarView))]
	public class ChatRightToolBarViewModel : ViewModelBase
	{
		private readonly IChatSettingsService _settingsService;

		/// <summary>
		/// Gets the user input view model (owns the send/stop commands).
		/// </summary>
		public UserInputViewModel UserInput { get; }

		private UserInputState UserInputState => UserInput.UserInputState;

		/// <summary>
		/// Gets the local users that can send messages in this chat.
		/// </summary>
		public RangeObservableCollection<UserToolbarItemModel> Users { get; } = [];

		private UserToolbarItemModel? _selectedUser;
		/// <summary>
		/// Gets or sets the user that sends the next message.
		/// </summary>
		public UserToolbarItemModel? SelectedUser
		{
			get => _selectedUser;
			set
			{
				if (SetProperty(ref _selectedUser, value) && value != null)
					UserInputState.SenderLogin = value.Login;
			}
		}

		/// <summary>
		/// Gets a value indicating whether more than one local user is available.
		/// </summary>
		public bool HasMultipleUsers => Users.Count > 1;

		/// <summary>
		/// Gets the selectable message visibilities.
		/// </summary>
		public ImmutableList<UserMessageVisibilityItemModel> Visibilities { get; } = [
			new UserMessageVisibilityItemModel { Visibility = MessageVisibility.Always, Title = Locale.GetKey("message.visibility.always"), Icon = MaterialIconKind.Eye },
			new UserMessageVisibilityItemModel { Visibility = MessageVisibility.RevealAfterSend, Title = Locale.GetKey("message.visibility.reveal_after_send"), Icon = MaterialIconKind.Clock },
			new UserMessageVisibilityItemModel { Visibility = MessageVisibility.OnlyUsers, Title = Locale.GetKey("message.visibility.only_users"), Icon = MaterialIconKind.Account },
			new UserMessageVisibilityItemModel { Visibility = MessageVisibility.OnlyAgents, Title = Locale.GetKey("message.visibility.only_agents"), Icon = MaterialIconKind.Robot }
		];

		private UserMessageVisibilityItemModel _selectedVisibility;
		/// <summary>
		/// Gets or sets the visibility of the next user message.
		/// </summary>
		public UserMessageVisibilityItemModel SelectedVisibility
		{
			get => _selectedVisibility;
			set
			{
				if (SetProperty(ref _selectedVisibility, value) && value != null)
					UserInputState.Visibility = value.Visibility;
			}
		}

		/// <summary>
		/// Gets the injection levels a queued follow-up can be composed with, earliest gate first.
		/// </summary>
		public ImmutableList<UserInputLevelItemModel> Levels { get; } = [
			new UserInputLevelItemModel { Level = FollowUpInjectionLevel.Immediate, Title = Locale.GetKey("chat.follow_up.level.immediate"), Icon = MaterialIconKind.Flash },
			new UserInputLevelItemModel { Level = FollowUpInjectionLevel.Message, Title = Locale.GetKey("chat.follow_up.level.message"), Icon = MaterialIconKind.ChatProcessing },
			new UserInputLevelItemModel { Level = FollowUpInjectionLevel.Agent, Title = Locale.GetKey("chat.follow_up.level.agent"), Icon = MaterialIconKind.Robot },
			new UserInputLevelItemModel { Level = FollowUpInjectionLevel.AgentSequence, Title = Locale.GetKey("chat.follow_up.level.agent_sequence"), Icon = MaterialIconKind.TimerSandComplete }
		];

		private UserInputLevelItemModel _selectedLevel;
		/// <summary>
		/// Gets or sets the level the next queued message is composed with. Persisted with the queue itself.
		/// </summary>
		public UserInputLevelItemModel SelectedLevel
		{
			get => _selectedLevel;
			set
			{
				if (SetProperty(ref _selectedLevel, value) && value != null)
					UserInput.SelectedLevel = value.Level;
			}
		}

		private EventHandler? _usersSettingsChangedHandler;

		public ChatRightToolBarViewModel(UserInputViewModel userInput)
		{
			UserInput = userInput;
			_settingsService = userInput.Chat.Services.GetRequiredService<IChatSettingsService>();

			_selectedVisibility = Visibilities.FirstOrDefault(v => v.Visibility == UserInputState.Visibility) ?? Visibilities[0];
			_selectedLevel = Levels.FirstOrDefault(l => l.Level == UserInput.SelectedLevel) ?? Levels[0];

			_usersSettingsChangedHandler = (_, _) => TrackUsers();
			_settingsService.SettingsChanged += _usersSettingsChangedHandler;
			TrackUsers();
		}

		/// <summary>
		/// Subscribes to the local users of the current chat and refreshes
		/// <see cref="Users"/> when the settings or the user list change.
		/// </summary>
		private void TrackUsers()
		{
			_settingsService.Settings.Users.Users.CollectionChanged -= Users_CollectionChanged;
			_settingsService.Settings.Users.Users.CollectionChanged += Users_CollectionChanged;
			RefreshUsers();
		}

		private void Users_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
		{
			RefreshUsers();
		}

		private void RefreshUsers()
		{
			var currentLogin = SelectedUser?.Login ?? UserInputState.SenderLogin;
			Users.Reset(_settingsService.Settings.Users.Users.Select(CreateUserItem));

			SelectedUser = Users.FirstOrDefault(u => u.Login == currentLogin) ?? Users.FirstOrDefault();
			RaisePropertyChanged(nameof(HasMultipleUsers));
		}

		/// <summary>
		/// Projects a configured user onto the selector item, decoding the avatar once per refresh.
		/// </summary>
		private static UserToolbarItemModel CreateUserItem(UserInformation user)
		{
			Bitmap? avatar = null;

			try
			{
				if (!string.IsNullOrWhiteSpace(user.Base64ProfileImage))
				{
					var bytes = Convert.FromBase64String(user.Base64ProfileImage);
					using var stream = new MemoryStream(bytes);
					avatar = new Bitmap(stream);
				}
			}
			catch
			{
				// A broken image is not worth breaking the toolbar over: the fallback icon takes over.
				avatar = null;
			}

			return new UserToolbarItemModel
			{
				Login = user.Login,
				Name = string.IsNullOrWhiteSpace(user.Name) ? user.Login : user.Name,
				Avatar = avatar
			};
		}

		protected override void Dispose(bool disposing)
		{
			base.Dispose(disposing);

			if (disposing)
			{
				if (_usersSettingsChangedHandler is not null)
					_settingsService.SettingsChanged -= _usersSettingsChangedHandler;
				_usersSettingsChangedHandler = null;

				_settingsService.Settings.Users.Users.CollectionChanged -= Users_CollectionChanged;
			}
		}
	}
}
