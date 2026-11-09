using System.Collections.Specialized;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using LLMDesktopAssistant.Addons.Sources;
using LLMDesktopAssistant.Agents;
using LLMDesktopAssistant.Utils;

namespace LLMDesktopAssistant.Addons
{
	/// <summary>
	/// Builds the effective addon sets for the chat and its agents and keeps them up to date incrementally.
	/// </summary>
	/// <remarks>
	/// The collector owns three layers: the available set (deduplicated sources, no configuration), the chat set, and
	/// one set per agent. The available set and the chat set live in fields; agent sets live in a
	/// <see cref="ConditionalWeakTable{TKey,TValue}"/> keyed by the agent descriptor, so an agent that leaves the chat
	/// takes its set with it. The collector owns every subscription and dispatches changes to the passive entries;
	/// an entry holds the resolved configuration and its clones, and never subscribes to anything itself.
	/// </remarks>
	/// <typeparam name="TAddon">The type of the addon.</typeparam>
	/// <typeparam name="TChange">The type of the per-addon change object.</typeparam>
	public abstract class AddonSetCollectorBase<TAddon, TChange> : Disposable, IAddonSetCollector<TAddon>
		where TAddon : AddonChangedBase<TAddon, TChange>
		where TChange : AddonChangeBase
	{
		/// <summary>
		/// The configuration and the gate category of one set.
		/// </summary>
		protected readonly record struct AddonTarget(
			AddonSetConfigurationBase<TChange>? Config,
			IAddonCategorySettings? Category);

		private sealed record ConfigHooks(
			PropertyChangedEventHandler Defaults,
			NotifyCollectionChangedEventHandler Changes);

		private readonly IServiceProvider _services;
		private readonly IFileAddonAccessor<TAddon> _accessor;
		private IAdditionalAddonSource<TAddon>[] _sources = [];
		// The captured collections that have been subscribed to.
		// IFileAddonAccessor.Addons triggers reloading on every property get, causing exceptions on disposal.
		private readonly List<ReadOnlyObservableCollection<TAddon>> _subscriptions = [];

		private readonly AddonSet<TAddon> _available = new();
		private readonly Dictionary<string, List<TAddon>> _groups = new(StringComparer.Ordinal);

		private readonly Lock _stateLock = new();
		private bool _initialized;
		private Entry? _chatEntry;
		private readonly ConditionalWeakTable<ChatAgentDescriptor, Entry> _agentEntries = [];
		private readonly Dictionary<AddonSetConfigurationBase<TChange>, ConfigHooks> _configs = [];
		private readonly Dictionary<IAddonCategorySettings, PropertyChangedEventHandler> _categories = [];
		private readonly Dictionary<TChange, ChangeTracker> _trackers = new(ReferenceEqualityComparer.Instance);

		/// <summary>
		/// Initializes a new instance of the <see cref="AddonSetCollectorBase{TAddon,TChange}"/> class.
		/// </summary>
		/// <param name="services">The chat service provider.</param>
		protected AddonSetCollectorBase(IServiceProvider services)
		{
			_services = services;
			_accessor = _services.GetRequiredService<IFileAddonAccessor<TAddon>>();
		}

		/// <summary>
		/// Resolves the sources and builds the available set on first use. Deferred from the constructor because a
		/// source can depend on a collector: a tool module asks for the toolset collector, and resolving the tool
		/// modules while that collector is still being constructed is a dependency cycle.
		/// </summary>
		private void EnsureInitialized()
		{
			lock (_stateLock)
			{
				if (_initialized)
					return;

				// Marked before the sources are resolved: a source constructor may re-enter the collector, and that
				// call must see an initialized collector instead of resolving the sources a second time.
				_initialized = true;

				_sources = [.. (_services.GetService<IEnumerable<IAdditionalAddonSource<TAddon>>>() ?? [])];

				var accessorAddons = _accessor.Addons;
				if (accessorAddons.CanRaiseCollectionChanged)
				{
					_subscriptions.Add(accessorAddons);
					accessorAddons.CollectionChanged += OnSourcesChanged;
				}
				foreach (var addon in accessorAddons)
					AddToGroup(addon);

				foreach (var source in _sources)
				{
					var sourceAddons = source.Addons;
					if (sourceAddons.CanRaiseCollectionChanged)
					{
						_subscriptions.Add(sourceAddons);
						sourceAddons.CollectionChanged += OnSourcesChanged;
					}
					foreach (var addon in sourceAddons)
						AddToGroup(addon);
				}

				var keys = _groups.Keys.ToList();
				foreach (var key in keys)
					PatchGroup(key);
				DispatchAvailableChanged(keys);
			}
		}

		/// <summary>
		/// Gets the service provider of the collector scope.
		/// </summary>
		protected IServiceProvider Services => _services;

		/// <inheritdoc/>
		public AddonSet<TAddon> GetAvailableAddons()
		{
			EnsureInitialized();
			return _available;
		}

		/// <inheritdoc/>
		public AddonSet<TAddon> GetAddonsForChat()
		{
			EnsureInitialized();

			var target = ResolveChatTarget();
			if (target.Config is not null)
				EnsureConfigSubscribed(target.Config);
			EnsureCategorySubscribed(target.Category);

			Entry entry;
			lock (_stateLock)
			{
				_chatEntry ??= new Entry(this, agent: null);
				entry = _chatEntry;
			}

			entry.EnsureTarget(target);
			entry.Reconcile();
			return entry.Set;
		}

		/// <inheritdoc/>
		public AddonSet<TAddon> GetAddonsForAgent(ChatAgentDescriptor agent)
		{
			ArgumentNullException.ThrowIfNull(agent);
			EnsureInitialized();

			var target = ResolveAgentTarget(agent);
			if (target.Config is not null)
				EnsureConfigSubscribed(target.Config);
			EnsureCategorySubscribed(target.Category);

			Entry entry;
			lock (_stateLock)
				entry = _agentEntries.GetValue(agent, descriptor => new Entry(this, descriptor));

			entry.EnsureTarget(target);
			entry.Reconcile();
			return entry.Set;
		}

		/// <summary>
		/// Resolves the configuration and the gate category of the chat-level set.
		/// </summary>
		protected virtual AddonTarget ResolveChatTarget() => new(null, null);

		/// <summary>
		/// Resolves the configuration and the gate category of the set addressed to the specified agent.
		/// Defaults to the chat-level target, which makes an agent-agnostic collector return the same set for the chat
		/// and for every agent.
		/// </summary>
		/// <param name="agent">The agent the set is requested for.</param>
		protected virtual AddonTarget ResolveAgentTarget(ChatAgentDescriptor agent)
		{
			var target = ResolveChatTarget();
			if (target.Config is null && target.Category is null)
				throw new NotSupportedException($"{GetType()} does not define an agent-addressed addon set.");
			return target;
		}

		/// <summary>
		/// Applies the per-addon change to the clone. Called only for an addon with an explicit change entry.
		/// </summary>
		/// <param name="target">The clone about to be frozen.</param>
		/// <param name="change">The change entry.</param>
		/// <param name="agent">The agent the set is resolved for, or <see langword="null"/> for the chat set.</param>
		protected virtual void ApplyChange(TAddon target, TChange change, ChatAgentDescriptor? agent)
		{
		}

		/// <summary>
		/// Applies the set-level defaults to every included clone. Called for every clone, with or without an explicit
		/// change entry, after <see cref="ApplyChange"/> and before the clone is frozen.
		/// </summary>
		/// <param name="target">The clone about to be frozen.</param>
		/// <param name="config">The set configuration the clone belongs to.</param>
		/// <param name="agent">The agent the set is resolved for, or <see langword="null"/> for the chat set.</param>
		protected virtual void ApplyDefaults(TAddon target, AddonSetConfigurationBase<TChange> config,
			ChatAgentDescriptor? agent)
		{
		}

		/// <summary>
		/// Determines whether the change of the specified default property affects the addon. The base implementation
		/// is exact for <see cref="AddonSetConfigurationBase{TChange}.EnabledByDefault"/> and
		/// <see cref="AddonSetConfigurationBase{TChange}.HiddenByDefault"/> and conservative (<see langword="true"/>)
		/// for the defaults a concrete configuration adds.
		/// </summary>
		/// <param name="addon">The addon that may be affected.</param>
		/// <param name="change">The explicit change entry of the addon, if any.</param>
		/// <param name="propertyName">The name of the changed default property.</param>
		protected virtual bool IsAffectedByDefault(TAddon addon, TChange? change, string? propertyName)
		{
			return propertyName switch
			{
				nameof(AddonSetConfigurationBase<TChange>.EnabledByDefault) =>
					!addon.IsFixed && change?.Enabled is null && addon.Enabled is null,
				nameof(AddonSetConfigurationBase<TChange>.HiddenByDefault) =>
					!addon.IsFixed && change?.Hidden is null && addon.Hidden is null,
				_ => true
			};
		}

		/// <inheritdoc/>
		protected override void Dispose(bool disposing)
		{
			base.Dispose(disposing);

			if (!disposing)
				return;

			if (_initialized)
			{
				foreach (var subscription in _subscriptions)
					subscription.CollectionChanged -= OnSourcesChanged;
			}

			foreach (var (config, hooks) in _configs)
			{
				config.PropertyChanged -= hooks.Defaults;
				config.Changes.CollectionChanged -= hooks.Changes;
			}

			foreach (var (category, handler) in _categories)
				category.PropertyChanged -= handler;

			foreach (var tracker in _trackers.Values)
				tracker.Dispose();

			_configs.Clear();
			_categories.Clear();
			_trackers.Clear();
			_agentEntries.Clear();
			_chatEntry = null;
		}

		private void OnSourcesChanged(object? sender, NotifyCollectionChangedEventArgs e)
		{
			if (e.Action is NotifyCollectionChangedAction.Move)
				return;

			if (e.Action is NotifyCollectionChangedAction.Reset)
			{
				RebuildAvailable();
				return;
			}

			var keys = new HashSet<string>(StringComparer.Ordinal);

			if (e.OldItems is not null)
				foreach (var item in e.OldItems)
					if (item is TAddon addon)
					{
						if (_groups.TryGetValue(addon.Key, out var members))
							members.Remove(addon);
						keys.Add(addon.Key);
					}

			if (e.NewItems is not null)
				foreach (var item in e.NewItems)
					if (item is TAddon addon)
					{
						AddToGroup(addon);
						keys.Add(addon.Key);
					}

			foreach (var key in keys)
				PatchGroup(key);

			DispatchAvailableChanged(keys);
		}

		private void RebuildAvailable()
		{
			var affected = new HashSet<string>(_available.Select(addon => addon.Key), StringComparer.Ordinal);

			_groups.Clear();
			foreach (var addon in _accessor.Addons)
				AddToGroup(addon);
			foreach (var source in _sources)
				foreach (var addon in source.Addons)
					AddToGroup(addon);

			var winners = new List<TAddon>();
			foreach (var key in _groups.Keys.ToList())
				if (BuildWinner(key) is { } winner)
					winners.Add(winner);

			foreach (var winner in winners)
			affected.Add(winner.Key);

			_available.Reset(winners);
			DispatchAvailableChanged([.. affected]);
		}

		private void AddToGroup(TAddon addon)
		{
			if (!_groups.TryGetValue(addon.Key, out var members))
				_groups[addon.Key] = members = [];
			if (!members.Contains(addon))
				members.Add(addon);
		}

		private void PatchGroup(string key)
		{
			var winner = BuildWinner(key);
			if (winner is null)
			{
				_groups.Remove(key);
				_available.Remove(key);
				return;
			}
			_available.Upsert(winner);
		}

		private TAddon? BuildWinner(string key)
		{
			if (!_groups.TryGetValue(key, out var members) || members.Count == 0)
				return null;

			members.Sort(CompareGroup);
			foreach (var member in members)
				member.Freeze();

			var winner = members[^1];
			if (members.Count == 1)
				return winner;

			var clone = winner.Clone();
			clone.Overrides = [.. members.Take(members.Count - 1)];
			clone.Freeze();
			return clone;
		}

		private static int CompareGroup(TAddon x, TAddon y)
		{
			var result = x.OverrideOrder.CompareTo(y.OverrideOrder);
			if (result != 0)
				return result;
			result = x.Order.CompareTo(y.Order);
			if (result != 0)
				return result;
			return string.CompareOrdinal(x.Key, y.Key);
		}

		private void DispatchAvailableChanged(IReadOnlyCollection<string> keys)
		{
			if (keys.Count == 0)
				return;

			foreach (var entry in EnumerateEntries())
				foreach (var key in keys)
					entry.PatchKey(key);
		}

		private IEnumerable<Entry> EnumerateEntries()
		{
			if (_chatEntry is { } chatEntry)
				yield return chatEntry;

			foreach (var pair in _agentEntries)
				yield return pair.Value;
		}

		private void EnsureConfigSubscribed(AddonSetConfigurationBase<TChange> config)
		{
			lock (_stateLock)
			{
				if (_configs.ContainsKey(config))
					return;

				PropertyChangedEventHandler defaults = (_, e) =>
				{
					if (e.PropertyName != nameof(AddonSetConfigurationBase<TChange>.Changes))
						DispatchDefaultsChanged(config, e.PropertyName);
				};
				NotifyCollectionChangedEventHandler changes = (_, e) => DispatchChangesChanged(config, e);

				_configs[config] = new ConfigHooks(defaults, changes);
				config.PropertyChanged += defaults;
				config.Changes.CollectionChanged += changes;

				foreach (var pair in config.Changes)
					SubscribeChange(config, pair.Key, pair.Value);
			}
		}

		private void EnsureCategorySubscribed(IAddonCategorySettings? category)
		{
			if (category is null)
				return;

			lock (_stateLock)
			{
				if (_categories.ContainsKey(category))
					return;

				PropertyChangedEventHandler handler = (_, e) =>
				{
					if (e.PropertyName == nameof(IAddonCategorySettings.AddonsCategoryEnabled))
						DispatchCategoryChanged(category);
				};

				_categories[category] = handler;
				category.PropertyChanged += handler;
			}
		}

		private void SubscribeChange(AddonSetConfigurationBase<TChange> config, string key, TChange change)
		{
			lock (_stateLock)
			{
				if (_trackers.ContainsKey(change))
					return;
				_trackers[change] = new ChangeTracker(change, () => DispatchChange(config, key));
			}
		}

		private void UnsubscribeChange(TChange change)
		{
			lock (_stateLock)
			{
				if (_trackers.Remove(change, out var tracker))
					tracker.Dispose();
			}
		}

		private void DispatchChangesChanged(AddonSetConfigurationBase<TChange> config, NotifyCollectionChangedEventArgs e)
		{
			if (e.Action is NotifyCollectionChangedAction.Reset)
			{
				foreach (var tracker in _trackers.Values)
					tracker.Dispose();
				_trackers.Clear();

				foreach (var pair in config.Changes)
					SubscribeChange(config, pair.Key, pair.Value);

				foreach (var entry in EnumerateEntries())
					if (ReferenceEquals(entry.Config, config))
						entry.Rebuild();
				return;
			}

			var keys = new List<string>();
			if (e.OldItems is not null)
			{
				foreach (var item in e.OldItems)
				{
					if (item is not KeyValuePair<string, TChange> old)
						continue;
					UnsubscribeChange(old.Value);
					keys.Add(old.Key);
				}
			}

			if (e.NewItems is not null)
			{
				foreach (var item in e.NewItems)
				{
					if (item is not KeyValuePair<string, TChange> @new)
						continue;
					SubscribeChange(config, @new.Key, @new.Value);
					keys.Add(@new.Key);
				}
			}

			foreach (var key in keys.Distinct(StringComparer.Ordinal))
				DispatchChange(config, key);
		}

		private void DispatchChange(AddonSetConfigurationBase<TChange> config, string key)
		{
			foreach (var entry in EnumerateEntries())
				if (ReferenceEquals(entry.Config, config))
					entry.PatchKey(key);
		}

		private void DispatchDefaultsChanged(AddonSetConfigurationBase<TChange> config, string? propertyName)
		{
			foreach (var entry in EnumerateEntries())
				if (ReferenceEquals(entry.Config, config))
					entry.OnDefaultsChanged(propertyName);
		}

		private void DispatchCategoryChanged(IAddonCategorySettings category)
		{
			foreach (var entry in EnumerateEntries())
				if (ReferenceEquals(entry.Category, category))
					entry.Reconcile();
		}

		/// <summary>
		/// The state of one resolved set: the resolved configuration, the gate category, the clones of all addons
		/// enabled by the configuration, and the visible set after the availability predicates.
		/// </summary>
		private sealed class Entry(AddonSetCollectorBase<TAddon, TChange> owner, ChatAgentDescriptor? agent)
		{
			private Dictionary<string, TAddon> _clones = [];

			public AddonSetConfigurationBase<TChange>? Config { get; private set; }

			public IAddonCategorySettings? Category { get; private set; }

			public AddonSet<TAddon> Set { get; } = new();

			public void EnsureTarget(AddonTarget target)
			{
				if (ReferenceEquals(Config, target.Config) && ReferenceEquals(Category, target.Category))
					return;

				Config = target.Config;
				Category = target.Category;
				Rebuild();
			}

			public void Rebuild()
			{
				var candidates = BuildCandidates();
				_clones = candidates.ToDictionary(addon => addon.Key, StringComparer.Ordinal);
				Set.Reset(candidates.Where(IncludesByPredicates).ToList());
			}

			public void PatchKey(string key)
			{
				var addon = owner._available.GetByKey(key);
				if (addon is null || !addon.IsValid)
				{
					_clones.Remove(key);
					Set.Remove(key);
					return;
				}

				if (Config is null)
				{
					_clones[key] = addon;
					if (IncludesByPredicates(addon))
						Set.Upsert(addon);
					else
						Set.Remove(key);
					return;
				}

				var change = GetChange(key);
				if (!IsEnabled(addon, change))
				{
					_clones.Remove(key);
					Set.Remove(key);
					return;
				}

				var clone = CreateClone(addon, change);
				_clones[key] = clone;
				if (IncludesByPredicates(clone))
					Set.Upsert(clone);
				else
					Set.Remove(key);
			}

			public void Reconcile()
			{
				if (Category?.AddonsCategoryEnabled is false)
				{
					Set.Clear();
					return;
				}

				foreach (var clone in _clones.Values)
				{
					if (IncludesByPredicates(clone))
						Set.Upsert(clone);
					else
						Set.Remove(clone.Key);
				}
			}

			public void OnDefaultsChanged(string? propertyName)
			{
				if (Config is null)
					return;

				foreach (var addon in owner._available)
					if (owner.IsAffectedByDefault(addon, GetChange(addon.Key), propertyName))
						PatchKey(addon.Key);
			}

			private List<TAddon> BuildCandidates()
			{
				var candidates = new List<TAddon>();
				foreach (var addon in owner._available)
				{
					if (!addon.IsValid)
						continue;

					if (Config is null)
					{
						candidates.Add(addon);
						continue;
					}

					var change = GetChange(addon.Key);
					if (IsEnabled(addon, change))
						candidates.Add(CreateClone(addon, change));
				}
				return candidates;
			}

			private TAddon CreateClone(TAddon addon, TChange? change)
			{
				var clone = addon.Clone();
				if (change is not null)
					owner.ApplyChange(clone, change, agent);
				owner.ApplyDefaults(clone, Config!, agent);
				clone.Enabled = true;
				clone.Hidden = !addon.IsFixed && (change?.Hidden ?? addon.Hidden ?? Config!.HiddenByDefault);
				clone.Change = change;
				clone.Freeze();
				return clone;
			}

			private TChange? GetChange(string key)
				=> Config is not null && Config.Changes.TryGetValue(key, out var change) ? change : null;

			private bool IsEnabled(TAddon addon, TChange? change)
				=> addon.IsFixed || (change?.Enabled ?? addon.Enabled ?? Config!.EnabledByDefault);

			private bool IncludesByPredicates(TAddon addon)
			{
				if (addon.ChatAvailablePredicate is { } chatPredicate && !chatPredicate(addon, owner._services))
					return false;
				if (agent is not null && addon.AgentAvailablePredicate is { } agentPredicate &&
					!agentPredicate(addon, owner._services, agent))
					return false;
				return true;
			}
		}
	}
}
