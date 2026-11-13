using LLMDesktopAssistant.LLM.Domain;
using LLMDesktopAssistant.LLM.Services;
using LLMDesktopAssistant.Settings;
using Serilog;

namespace LLMDesktopAssistant.Users
{
	[ChatService(typeof(IUserManagementService))]
	public class UserManagementService(
		IChatSettingsService chatSettings,
		IPasswordHashingService hasher,
		IRemoteOnlineUsersService onlineUsers
	) : IUserManagementService
	{
		public IEnumerable<UserInformation> GetAllUsers()
		{
			return GetLocalUsers().Concat(GetRemoteUsers());
		}

		public IEnumerable<UserInformation> GetLocalUsers()
		{
			return chatSettings.Settings.Users.Users;
		}

		public IEnumerable<UserInformation> GetRemoteUsers()
		{
			return SettingsManager.Get<RemoteUsersConfiguration>().Users;
		}

		public bool IsLocalUser(string userLogin)
		{
			return chatSettings.Settings.Users.Users.Any(u => u.Login == userLogin);
		}

		public IEnumerable<UserInformation> GetAvailableLocalUsers()
		{
			return GetLocalUsers().Where(u => u.IsAvailable);
		}

		public IEnumerable<UserInformation> GetOnlineRemoteUsers()
		{
			return GetRemoteUsers().Where(u => onlineUsers.IsOnline(u.Login));
		}

		public bool IsOnline(string userLogin)
		{
			return onlineUsers.IsOnline(userLogin);
		}

		public UserInformation? FindByLogin(string login)
		{
			return GetAllUsers().FirstOrDefault(u => u.Login == login);
		}

		public UserInformation? RegisterUser(string login, string password, string? name, string? description)
		{
			if (string.IsNullOrWhiteSpace(login) || string.IsNullOrWhiteSpace(password))
				return null;

			if (FindByLogin(login) != null)
			{
				Log.Warning("Registration attempt with existing login: {Login}", login);
				return null;
			}

			var hash = hasher.HashPassword(password);
			var newUser = new UserInformation
			{
				Login = login,
				Name = name ?? login,
				PasswordHash = hash,
				Description = description ?? string.Empty
			};

			SettingsManager.Get<RemoteUsersConfiguration>().Users.Add(newUser);

			Log.Information("New user registered: {Login} ({Name})", login, newUser.Name);
			return newUser;
		}
	}
}