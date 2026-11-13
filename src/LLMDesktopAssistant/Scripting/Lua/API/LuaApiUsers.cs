using AsyncLua;
using AsyncLua.Values;
using LLMDesktopAssistant.Users;

namespace LLMDesktopAssistant.Scripting.Lua.API
{
	/// <summary>
	/// Lua API for the users of the current chat: <c>dass.users.*</c>.
	/// Read-only view over <see cref="IUserManagementService"/>.
	/// </summary>
	/// <remarks>
	/// Local users and WebUI-registered users share one shape, because a sender login can name either of them, but the
	/// two axes stay separate: availability is a configuration flag of a local user, presence is a WebUI session of a
	/// remote one. The profile image is deliberately absent - a base64 blob per user would dominate the table for no
	/// scripting use.
	/// </remarks>
	[LuaApi(chatScoped: true)]
	public class LuaApiUsers : LuaApiBase
	{
		public override string? Namespace => "dass.users";

		public override string? Manuals => """
			--- dass.users - the users of the current chat

			FUNCTIONS:

			--- dass.users.list([scope]) -> table
			  Returns an array of user tables (see USER TABLE below).

			  Parameters:
			    - scope: string - which users to return, default "all":
			        "all"             - the users defined in the chat settings plus the users
			                            registered through the WebUI
			        "local"           - only the users defined in the chat settings
			        "remote"          - only the users registered through the WebUI
			        "local-available" - local users that may send messages (is_available)
			        "remote-online"   - remote users that are currently online in this chat
			        "online"          - "local-available" plus "remote-online": everyone who can
			                            actually take part in this chat right now

			--- dass.users.get(login) -> table or nil
			  Finds one user by login, local or remote. Returns nil when there is no such login.

			USER TABLE:

			  Properties (read-only):
			    - login: string - the login written into a message as its sender
			    - name: string - the display name
			    - agent_shown_name: string - the name agents are told to use: the shown name,
			      then the name, then the login
			    - description: string - what agents are told about this user
			    - is_local: boolean - defined in the chat settings rather than registered
			      through the WebUI
			    - is_available: boolean - for a local user: whether it may send messages. An
			      unavailable user is not offered as a sender, but the messages it already sent
			      stay signed. A remote user is always true here.
			    - is_online: boolean - for a remote user: whether it holds an open WebUI
			      connection to THIS chat. A local user has no sessions, so it is always false.

			EXAMPLES:

			  -- Everyone who is here right now
			  for _, user in ipairs(dass.users.list("online")) do
			    print(user.login, user.name, user.is_local and "local" or "online")
			  end

			  -- Local users that may send, ignoring the WebUI
			  for _, user in ipairs(dass.users.list("local-available")) do
			    print(user.login, user.agent_shown_name)
			  end

			  local user = dass.users.get("master")
			  if user then print(user.description) end

			NOTES:
			  - The list is a snapshot: it does not react to later user changes.
			  - "Online" means the chat's WebUI is running and that login holds a connection to it.
			    A desktop-only chat has no sessions, so remote-online and is_online stay empty there.
			""";

		private readonly IUserManagementService _users;

		public LuaApiUsers(IUserManagementService users)
		{
			_users = users;
		}

		public override Action? Populate(LuaTable globals, LuaTable ns, LuaService luaService)
		{
			ns["list"] = new LuaCallbackFunction(List);
			ns["get"] = new LuaCallbackFunction(Get);
			return null;
		}

		private LuaTuple List(LuaCallingContext ctx, LuaValue[] args)
		{
			var scope = args.Length > 0 && args[0] is LuaString value
				? value.Value.Trim().ToLowerInvariant()
				: "all";

			IEnumerable<UserInformation> users = scope switch
			{
				"all" or "" => _users.GetAllUsers(),
				"local" => _users.GetLocalUsers(),
				"remote" => _users.GetRemoteUsers(),
				"local-available" => _users.GetAvailableLocalUsers(),
				"remote-online" => _users.GetOnlineRemoteUsers(),
				"online" => _users.GetAvailableLocalUsers()
					.Concat(_users.GetOnlineRemoteUsers())
					.DistinctBy(user => user.Login),
				_ => throw new LuaRuntimeException(
					"dass.users.list([scope]): unknown scope '" + scope + "'. Expected one of: " +
					"all, local, remote, local-available, remote-online, online.")
			};

			var localLogins = _users.GetLocalUsers().Select(user => user.Login).ToHashSet(StringComparer.Ordinal);

			var table = new LuaTable();
			var index = 1;
			foreach (var user in users)
			{
				var isLocal = localLogins.Contains(user.Login);
				table.Set(index++, ToTable(user, isLocal));
			}

			return new LuaTuple(table);
		}

		private LuaTuple Get(LuaCallingContext ctx, LuaValue[] args)
		{
			if (args.Length == 0 || args[0] is not LuaString login)
				throw new LuaRuntimeException("dass.users.get(login): a login string is expected.");

			var user = _users.FindByLogin(login.Value);
			return new LuaTuple(user is null
				? LuaNil.Instance
				: ToTable(user, _users.IsLocalUser(user.Login)));
		}

		private LuaTable ToTable(UserInformation user, bool isLocal)
		{
			var table = new LuaTable();
			table["login"] = new LuaString(user.Login);
			table["name"] = new LuaString(user.Name);
			table["agent_shown_name"] = new LuaString(user.GetAgentShownName());
			table["description"] = new LuaString(user.Description);
			table["is_local"] = LuaBoolean.FromBoolean(isLocal);
			table["is_available"] = LuaBoolean.FromBoolean(isLocal ? user.IsAvailable : true);
			table["is_online"] = LuaBoolean.FromBoolean(!isLocal && _users.IsOnline(user.Login));
			return table;
		}
	}
}
