using System.Collections.Specialized;
using LLMDesktopAssistant.LLM.Domain;
using LLMDesktopAssistant.LLM.Services;
using LLMDesktopAssistant.Localization;
using LLMDesktopAssistant.Users;
using LLMDesktopAssistant.Utils;
using Material.Icons;

namespace LLMDesktopAssistant.LLM.MVVM
{
	/// <summary>
	/// The right chat toolbar: sender and visibility selectors, plus the send/stop/cancel-edit
	/// controls (delegated to the user input).
	/// </summary>
	[ViewModelFor(typeof(ChatRightToolBarView))]
	public class ChatRightToolBarViewModel : ViewModelBase
	{
		private readonly IChatSettingsService _settingsService;

		/// <summary>
		/// Gets the user input view model (owns the send/stop/cancel-edit commands).
		/// </summary>
		public UserInputViewModel UserInput { get; }

		private UserInputState UserInputState => UserInput.UserInputState;

		/// <summary>
		/// Gets the list of local users that can send messages in this chat.
		/// </summary>
		public RangeObservableCollection<UserInformation> Users { get; } = [];

		private UserInformation? _selectedUser;
		/// <summary>
		/// Gets or sets the user that sends the next message.
		/// </summary>
		public UserInformation? SelectedUser
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

		private EventHandler? _usersSettingsChangedHandler;

		public ChatRightToolBarViewModel(UserInputViewModel userInput)
		{
			UserInput = userInput;
			_settingsService = userInput.Chat.Services.GetRequiredService<IChatSettingsService>();

			_selectedVisibility = Visibilities.FirstOrDefault(v => v.Visibility == UserInputState.Visibility) ?? Visibilities[0];

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
			Users.Reset(_settingsService.Settings.Users.Users);

			SelectedUser = Users.FirstOrDefault(u => u.Login == currentLogin) ?? Users.FirstOrDefault();
			RaisePropertyChanged(nameof(HasMultipleUsers));
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
