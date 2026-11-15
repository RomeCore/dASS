using AsyncLua;
using AsyncLua.Values;
using LLMDesktopAssistant.Addons;
using LLMDesktopAssistant.Addons.Sources;

namespace LLMDesktopAssistant.Scripting.Lua
{
	/// <summary>
	/// The handle a runtime registration returns to Lua as an <c>addon_revoke_handle</c>.
	/// </summary>
	public class LuaAddonRevokeHandle<TAddon>(RuntimeAddonRegistration<TAddon> registration)
		where TAddon : AddonBase<TAddon>
	{
		/// <summary>
		/// Whether the registration is gone: revoked, or replaced by another registration under the same name.
		/// </summary>
		public bool Revoked => registration.Revoked;

		/// <summary>
		/// Removes the registration.
		/// </summary>
		/// <returns><see langword="true"/> when it was still registered.</returns>
		public bool Revoke() => registration.Revoke();

		/// <summary>
		/// Wraps the handle into an <c>addon_revoke_handle</c> userdata.
		/// </summary>
		[LuaHidden]
		public static LuaUserData Wrap(LuaAddonRevokeHandle<TAddon> handle)
			=> new(handle, "addon_revoke_handle")
			{
				Metatable = UserDataMetatableGenerator.GetOrCreate(typeof(LuaAddonRevokeHandle<TAddon>))
			};

		/// <summary>
		/// Wraps the handle into an <c>addon_revoke_handle</c> userdata.
		/// </summary>
		[LuaHidden]
		public static LuaUserData Wrap(RuntimeAddonRegistration<TAddon> registration)
			=> new(new LuaAddonRevokeHandle<TAddon>(registration), "addon_revoke_handle")
			{
				Metatable = UserDataMetatableGenerator.GetOrCreate(typeof(LuaAddonRevokeHandle<TAddon>))
			};
	}
}
