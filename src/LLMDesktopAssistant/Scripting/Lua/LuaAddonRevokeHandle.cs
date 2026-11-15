using AsyncLua;
using AsyncLua.Values;

namespace LLMDesktopAssistant.Scripting.Lua
{
	/// <summary>
	/// The handle a runtime registration returns to Lua as an <c>addon_revoke_handle</c>.
	/// </summary>
	/// <remarks>
	/// Deliberately not generic: the same type backs every dynamically registered addon kind, so a script sees one
	/// shape - <c>handle.revoked</c> and <c>handle:revoke()</c> - whatever it registered.
	/// </remarks>
	public class LuaAddonRevokeHandle(Func<bool> isRegistered, Func<bool> revoke)
	{
		/// <summary>
		/// Whether the registration is gone: revoked, or replaced by another registration under the same name.
		/// </summary>
		public bool Revoked => !isRegistered();

		/// <summary>
		/// Removes the registration.
		/// </summary>
		/// <returns><see langword="true"/> when it was still registered.</returns>
		public bool Revoke() => revoke();

		/// <summary>
		/// Wraps the handle into an <c>addon_revoke_handle</c> userdata.
		/// </summary>
		[LuaHidden]
		public static LuaUserData Wrap(LuaAddonRevokeHandle handle)
			=> new(handle, "addon_revoke_handle")
			{
				Metatable = UserDataMetatableGenerator.GetOrCreate(typeof(LuaAddonRevokeHandle))
			};
	}
}
