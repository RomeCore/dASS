namespace LLMDesktopAssistant.Users
{
	public class UserInformation : NotifyPropertyChanged
	{
		private string _login = string.Empty;
		/// <summary>
		/// The login of the user.
		/// </summary>
		public string Login
		{
			get => _login;
			set => SetProperty(ref _login, value);
		}

		private bool _isAvailable = true;
		/// <summary>
		/// Indicates whether the user may send messages in the chat.
		/// </summary>
		/// <remarks>
		/// Not a presence flag: an unavailable user is simply not offered as a sender, while the messages it already
		/// sent keep their signature, name and description. Remote users are governed by
		/// <see cref="IRemoteOnlineUsersService"/> instead.
		/// </remarks>
		public bool IsAvailable
		{
			get => _isAvailable;
			set => SetProperty(ref _isAvailable, value);
		}

		private string? _passwordHash;
		/// <summary>
		/// The hash of the user's password. This is used for authentication purposes.
		/// Not usable when user is local.
		/// </summary>
		public string? PasswordHash
		{
			get => _passwordHash;
			set => SetProperty(ref _passwordHash, value);
		}

		private string _name = string.Empty;
		/// <summary>
		/// The name of the user.
		/// </summary>
		public string Name
		{
			get => _name;
			set => SetProperty(ref _name, value);
		}

		private string? _agentShownName;
		/// <summary>
		/// The name of the user that is shown to other users. This can be different from the actual name.
		/// </summary>
		public string? AgentShownName
		{
			get => _agentShownName;
			set => SetProperty(ref _agentShownName, value);
		}

		public string GetAgentShownName()
		{
			if (!string.IsNullOrEmpty(AgentShownName))
				return AgentShownName;
			if (!string.IsNullOrEmpty(Name))
				return Name;
			return Login;
		}

		private string _description = string.Empty;
		/// <summary>
		/// The description of the user, contains additional information for agents.
		/// </summary>
		public string Description
		{
			get => _description;
			set => SetProperty(ref _description, value);
		}

		private string _base64ProfileImage = string.Empty;
		/// <summary>
		/// The base64 encoded profile image of the user. If empty, no image is displayed.
		/// </summary>
		public string Base64ProfileImage
		{
			get => _base64ProfileImage;
			set => SetProperty(ref _base64ProfileImage, value);
		}
	}
}