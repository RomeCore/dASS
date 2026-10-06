using LLMDesktopAssistant.Agents;
using Serilog;

namespace LLMDesktopAssistant.Addons
{
	public abstract class AddonSetCollectorBase<TAddon, TChange> : IAddonSetCollector<TAddon>
		where TAddon : AddonChangedBase<TAddon, TChange>, new()
		where TChange : AddonChangeBase
	{
		private readonly IServiceProvider _services;
		private readonly IAddonAccessor<TAddon> _accessor;

		public AddonSetCollectorBase(IServiceProvider services)
		{
			_services = services;
			_accessor = _services.GetRequiredService<IAddonAccessor<TAddon>>();
		}

		protected virtual IEnumerable<TAddon> GetAdditionalAddons()
		{
			return [];
		}

		/// <summary>
		/// Gets the key used to deduplicate addons that represent the same logical addon.
		/// Defaults to the addon name; override with any value that has meaningful equality
		/// (a string, a tuple, a record ...) when a name alone is not unique
		/// (for example, commands whose name can appear in several namespaces).
		/// </summary>
		protected virtual object GetDeduplicationKey(TAddon addon)
		{
			return addon.Name;
		}

		protected virtual void ApplyChange(TAddon target, TChange change, ChatAgentDescriptor? agent)
		{
		}

		public IEnumerable<TAddon> GetAvailableAddons()
		{
			List<TAddon> addons = [];

			addons.AddRange(_accessor.Addons);
			addons.AddRange(GetAdditionalAddons());

			return addons
				.GroupBy(GetDeduplicationKey)
				.Select(g =>
				{
					ImmutableList<TAddon>.Builder? overridesBuilder = null;
					TAddon? last = null;
					foreach (var addon in g.OrderBy(a => a.OverrideOrder).ThenBy(a => a.Order))
					{
						addon.Freeze();
						if (last is not null)
						{
							overridesBuilder ??= ImmutableList.CreateBuilder<TAddon>();
							overridesBuilder.Add(last);
						}
						last = addon;
					}
					if (overridesBuilder == null)
						return last!;
					last = last!.Clone();
					last.Overrides = overridesBuilder.ToImmutable();
					last.Freeze();
					return last;
				})
				.OrderBy(a => a.Order)
				.ThenBy(a => a.Name);
		}

		public virtual IEnumerable<TAddon> GetAddonsForChat()
		{
			Log.Warning("GetAddons* not implemented for {0}! Returning all addons. Override if necessary.", GetType());
			return GetAvailableAddons().Where(a =>
			{
				if (a.Diagnostic?.IsFatal is true)
					return false;
				if (a.ChatAvailablePredicate is null)
					return true;
				return a.ChatAvailablePredicate.Invoke(a, _services);
			});
		}

		public virtual IEnumerable<TAddon> GetAddonsForAgent(ChatAgentDescriptor agent)
		{
			return GetAddonsForChat().Where(a =>
			{
				if (a.Diagnostic?.IsFatal is true)
					return false;
				if (a.AgentAvailablePredicate is null)
					return true;
				return a.AgentAvailablePredicate.Invoke(a, _services, agent);
			});
		}

		protected IEnumerable<TAddon> GetAddonsWithChanges(AddonSetConfigurationBase<TChange> setConfig,
			ChatAgentDescriptor? agent)
		{
			var addons = GetAvailableAddons();
			var result = new List<TAddon>();

			foreach (var addon in addons)
			{
				if (addon.Diagnostic?.IsFatal is true)
					continue;

				if (addon.ChatAvailablePredicate is not null && !addon.ChatAvailablePredicate.Invoke(addon, _services))
					continue;

				if (agent is not null && addon.AgentAvailablePredicate is not null &&
					!addon.AgentAvailablePredicate.Invoke(addon, _services, agent))
					continue;

				if (setConfig.Changes.TryGetValue(addon.Name, out var change))
				{
					if (addon.IsFixed || (change.Enabled ?? addon.Enabled ?? setConfig.EnabledByDefault))
					{
						var clone = addon.Clone();
						ApplyChange(clone, change, agent);
						clone.Enabled = true;
						if (addon.IsFixed)
							clone.Hidden = false;
						else
							clone.Hidden = change.Hidden ?? addon.Hidden ?? setConfig.HiddenByDefault;
						clone.Change = change;
						clone.Freeze();
						result.Add(clone);
					}
				}
				else
				{
					if (addon.IsFixed || (addon.Enabled ?? setConfig.EnabledByDefault))
					{
						var clone = addon.Clone();
						clone.Enabled = true;
						if (addon.IsFixed)
							clone.Hidden = false;
						else
							clone.Hidden = addon.Hidden ?? setConfig.HiddenByDefault;
						clone.Freeze();
						result.Add(clone);
					}
				}
			}

			return result
				.OrderBy(a => a.Order)
				.ThenBy(a => a.Name);
		}
	}
}
