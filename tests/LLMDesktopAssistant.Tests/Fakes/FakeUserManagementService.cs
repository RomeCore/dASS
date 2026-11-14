using LLMDesktopAssistant.Users;

namespace LLMDesktopAssistant.Tests.Fakes
{
	/// <summary>
	/// A user directory with no users: every listing is empty, every lookup misses.
	/// </summary>
	/// <remarks>
	/// Shared rather than re-declared per test file: the interface grew three members once already, and every
	/// private copy of it broke the build at the same time.
	/// </remarks>
	public sealed class FakeUserManagementService : IUserManagementService
	{
		public IEnumerable<UserInformation> GetAllUsers() => [];

		public IEnumerable<UserInformation> GetLocalUsers() => [];

		public IEnumerable<UserInformation> GetRemoteUsers() => [];

		public bool IsLocalUser(string userLogin) => false;

		public IEnumerable<UserInformation> GetAvailableLocalUsers() => [];

		public IEnumerable<UserInformation> GetOnlineRemoteUsers() => [];

		public bool IsOnline(string userLogin) => false;

		public UserInformation? FindByLogin(string login) => null;

		public UserInformation? RegisterUser(string login, string password, string? name, string? description) => null;
	}
}
