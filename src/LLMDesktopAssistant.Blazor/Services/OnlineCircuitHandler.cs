using LLMDesktopAssistant.Users;
using Microsoft.AspNetCore.Components.Server.Circuits;

namespace LLMDesktopAssistant.Blazor.Services
{
	/// <summary>
	/// Reports the Blazor circuits of this chat to its presence set, so a login counts as online while any of its
	/// connections is open.
	/// </summary>
	[WebUIService(typeof(CircuitHandler), IsScoped = true)]
	public class OnlineCircuitHandler(IRemoteOnlineUsersService onlineUsers, IUserStateService userState)
		: CircuitHandler
	{
		/// <inheritdoc/>
		public override Task OnCircuitOpenedAsync(Circuit circuit, CancellationToken cancellationToken)
		{
			var currentUser = userState.GetCurrentUser();
			if (currentUser != null)
				onlineUsers.EnterSession(currentUser.Login);
			return base.OnCircuitOpenedAsync(circuit, cancellationToken);
		}

		/// <inheritdoc/>
		public override Task OnCircuitClosedAsync(Circuit circuit, CancellationToken cancellationToken)
		{
			var currentUser = userState.GetCurrentUser();
			if (currentUser != null)
				onlineUsers.LeaveSession(currentUser.Login);
			return base.OnCircuitClosedAsync(circuit, cancellationToken);
		}
	}
}
