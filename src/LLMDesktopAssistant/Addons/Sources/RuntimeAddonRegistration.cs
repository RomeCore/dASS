namespace LLMDesktopAssistant.Addons.Sources
{
	/// <summary>
	/// One runtime registration: what a script hands back to undo it.
	/// </summary>
	/// <typeparam name="TAddon">The type of the addon.</typeparam>
	public class RuntimeAddonRegistration<TAddon>(RuntimeAddonSource<TAddon> owner, TAddon addon)
		where TAddon : AddonBase<TAddon>
	{
		/// <summary>
		/// The addon this handle registered.
		/// </summary>
		public TAddon Addon => addon;

		/// <summary>
		/// Whether the registration is gone: revoked, or replaced by a registration under the same key.
		/// </summary>
		public bool Revoked => !owner.Contains(addon);

		/// <summary>
		/// Removes the addon from its source.
		/// </summary>
		/// <returns><see langword="true"/> when it was still registered.</returns>
		public bool Revoke() => owner.Revoke(addon);
	}
}
