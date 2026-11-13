using LLMDesktopAssistant.Utils;

namespace LLMDesktopAssistant.Users
{
	/// <summary>
	/// Holds the logins of remote (WebUI) users that currently hold an open connection.
	/// </summary>
	/// <remarks>
	/// Chat-scoped, because the WebUI hosts exactly one chat: each chat carries its own web application and its own
	/// circuits, so a login is online in that chat and nowhere else. A desktop-only chat leaves the set empty, since
	/// it has no user sessions at all.
	/// </remarks>
	public interface IRemoteOnlineUsersService
	{
		/// <summary>
		/// Gets the logins that are online right now, one entry per login no matter how many sessions it holds.
		/// </summary>
		ReadOnlyObservableCollection<string> OnlineUsers { get; }

		/// <summary>
		/// Determines whether the login is online.
		/// </summary>
		/// <param name="login">The login to check.</param>
		/// <returns><see langword="true"/> when at least one session of that login is open.</returns>
		bool IsOnline(string login);

		/// <summary>
		/// Registers an opened session of the login.
		/// </summary>
		/// <param name="login">The login that connected. Counted per session, so one login may enter several times.</param>
		void EnterSession(string login);

		/// <summary>
		/// Registers a closed session of the login; the login leaves <see cref="OnlineUsers"/> with its last session.
		/// </summary>
		/// <param name="login">The login that disconnected.</param>
		void LeaveSession(string login);
	}
}
