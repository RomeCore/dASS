using LLMDesktopAssistant.LLM.Services;
using LLMDesktopAssistant.Utils;
using Serilog;

namespace LLMDesktopAssistant.Users
{
	/// <inheritdoc cref="IRemoteOnlineUsersService"/>
	[ChatService(typeof(IRemoteOnlineUsersService))]
	public class RemoteOnlineUsersService : IRemoteOnlineUsersService
	{
		private readonly Lock _lock = new();
		private readonly Dictionary<string, int> _sessions = [];

		private readonly RangeObservableCollection<string> _onlineUsers = [];

		public RemoteOnlineUsersService()
		{
			OnlineUsers = new(_onlineUsers);
		}

		/// <inheritdoc/>
		public ReadOnlyObservableCollection<string> OnlineUsers { get; }

		/// <inheritdoc/>
		public bool IsOnline(string login)
		{
			lock (_lock)
				return _sessions.ContainsKey(login);
		}

		/// <inheritdoc/>
		public void EnterSession(string login)
		{
			lock (_lock)
			{
				// The first session of a login is what puts it on the list; the rest only keep it there.
				if (!_sessions.TryGetValue(login, out var count))
					_onlineUsers.Add(login);

				_sessions[login] = count + 1;
			}
		}

		/// <inheritdoc/>
		public void LeaveSession(string login)
		{
			lock (_lock)
			{
				if (!_sessions.TryGetValue(login, out var count))
				{
					Log.Warning("User {Login} is trying to leave a session but was not in one.", login);
					return;
				}

				if (count > 1)
				{
					_sessions[login] = count - 1;
					return;
				}

				_sessions.Remove(login);
				_onlineUsers.Remove(login);
			}
		}
	}
}
