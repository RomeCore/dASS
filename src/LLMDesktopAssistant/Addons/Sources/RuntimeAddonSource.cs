using LLMDesktopAssistant.Utils;

namespace LLMDesktopAssistant.Addons.Sources
{
	/// <summary>
	/// An addon source whose addons are registered at run time - by a script - and revoked the same way.
	/// </summary>
	/// <remarks>
	/// Registration is keyed by <see cref="AddonBase{Self}.Key"/>: registering an addon whose key is already present
	/// replaces the older one, so a script that registers on load does not accumulate duplicates across reloads. A
	/// replacement creates a new instance, so the older handle reports itself revoked and revoking it is a no-op.
	/// </remarks>
	/// <typeparam name="TAddon">The type of the addon.</typeparam>
	public class RuntimeAddonSource<TAddon> : ObservableAddonSourceBase<TAddon>
		where TAddon : AddonBase<TAddon>
	{
		private readonly Lock _lock = new();

		/// <summary>
		/// Adds the addon, replacing the one already registered under the same key.
		/// </summary>
		/// <param name="addon">The addon to expose to the collector.</param>
		/// <returns>The handle that revokes it.</returns>
		public RuntimeAddonRegistration<TAddon> Register(TAddon addon)
		{
			ArgumentNullException.ThrowIfNull(addon);

			lock (_lock)
			{
				var existing = IndexOfKey(addon.Key);
				if (existing >= 0)
					Items.RemoveAt(existing);
				Items.Add(addon);
			}

			return new RuntimeAddonRegistration<TAddon>(this, addon);
		}

		/// <summary>
		/// Checks whether the addon is still registered.
		/// </summary>
		/// <param name="addon">The addon.</param>
		public bool Contains(TAddon addon)
		{
			lock (_lock)
				return Items.Contains(addon);
		}

		/// <summary>
		/// Removes the addon when it is still registered.
		/// </summary>
		/// <param name="addon">The addon.</param>
		/// <returns><see langword="true"/> when it was registered and is now gone.</returns>
		public bool Revoke(TAddon addon)
		{
			lock (_lock)
			{
				var index = Items.IndexOf(addon);
				if (index < 0)
					return false;
				Items.RemoveAt(index);
				return true;
			}
		}

		private int IndexOfKey(string key)
		{
			for (var index = 0; index < Items.Count; index++)
				if (string.Equals(Items[index].Key, key, StringComparison.Ordinal))
					return index;
			return -1;
		}
	}
}
