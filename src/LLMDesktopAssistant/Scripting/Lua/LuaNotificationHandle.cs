using AsyncLua;
using AsyncLua.Values;
using LLMDesktopAssistant.Prompting.Context.Notifications;

namespace LLMDesktopAssistant.Scripting.Lua
{
	/// <summary>
	/// The <c>notification_handle</c> a pushed prompt notification returns to Lua, wrapping the pool item it created.
	/// </summary>
	/// <remarks>
	/// The item is held by reference, not by id: cancelling flips the flag on the very object the pool and the
	/// delivery pass read, so no lookup is involved. The handle cannot outlive its chat - the Lua state dies with
	/// the chat scope - so a stale reference is unreachable rather than dangerous.
	/// </remarks>
	public class LuaNotificationHandle(PromptNotificationItem item)
	{
		/// <summary>
		/// Whether the notification is cancelled and will not be delivered to agents that have not seen it yet.
		/// </summary>
		public bool Cancelled => item.Cancelled;

		/// <summary>
		/// Cancels the notification. It cannot be retracted from an agent that has already read it.
		/// </summary>
		public void Cancel()
		{
			item.Cancelled = true;
		}

		/// <summary>
		/// Wraps the handle into a <c>notification_handle</c> userdata.
		/// </summary>
		[LuaHidden]
		public static LuaUserData Wrap(LuaNotificationHandle handle)
			=> new(handle, "notification_handle")
			{
				Metatable = UserDataMetatableGenerator.GetOrCreate(typeof(LuaNotificationHandle))
			};
	}
}
