using System.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LLMDesktopAssistant.Addons.Management;
using LLMDesktopAssistant.Addons.MVVM.Grouping;
using LLMDesktopAssistant.Addons.Search;
using LLMDesktopAssistant.Agents;
using LLMDesktopAssistant.Localization;
using LLMDesktopAssistant.Tools;
using LLMDesktopAssistant.Utils;

namespace LLMDesktopAssistant.Addons.MVVM
{
	/// <summary>
	/// The view model of a reusable addon list: the search query, the current grouping mode, the current
	/// elements of the list (single cards and, when a grouping mode is selected, the group cards that hold
	/// them) and the commands a host view binds to.
	/// </summary>
	/// <remarks>
	/// The class is intentionally non-generic and abstract: it is used as the <c>x:DataType</c> of the
	/// addon list panel view (generic types cannot be used in compiled bindings), while the list itself
	/// is built by <see cref="AddonListViewModel{TAddon, TChange}"/>.
	/// </remarks>
	public abstract class AddonListViewModel : ViewModelBase
	{
		private string _searchText = string.Empty;
		private GroupingMode? _selectedGroupingMode;
		private bool _hasVisibleItems;

		/// <summary>
		/// Initializes a new instance of the <see cref="AddonListViewModel"/> class.
		/// </summary>
		protected AddonListViewModel()
		{
			TagClickCommand = new RelayCommand<string>(tag =>
			{
				if (!string.IsNullOrEmpty(tag))
					SearchText = tag;
			});
			RefreshCommand = new RelayCommand(Update);
		}

		/// <summary>
		/// Gets the command that filters the list by the tag that was clicked on a card.
		/// </summary>
		public ICommand TagClickCommand { get; }

		/// <summary>
		/// Gets the command that reloads the addons from disk and rebuilds the list.
		/// </summary>
		public ICommand RefreshCommand { get; }

		/// <summary>
		/// Gets or sets the locale key of the search box placeholder.
		/// </summary>
		public LocaleKeyBase SearchPlaceholderKey { get; set; } = Locale.GetConstKey(string.Empty);

		/// <summary>
		/// Gets or sets the locale key of the text shown when no element matches the search query.
		/// </summary>
		public LocaleKeyBase EmptyTextKey { get; set; } = Locale.GetConstKey(string.Empty);

		/// <summary>
		/// Gets the grouping modes available for the list. The mode selector is shown by the panel
		/// only when more than one mode is provided.
		/// </summary>
		public ImmutableList<GroupingMode> GroupingModes
		{
			get => field;
			set
			{
				if (!SetProperty(ref field, value))
					return;

				var selected = SelectedGroupingMode;
				if (selected is null || !value.Contains(selected))
					SelectedGroupingMode = value.FirstOrDefault();

				RaisePropertyChanged(nameof(HasGroupingModes));
			}
		} = [];

		/// <summary>
		/// Gets or sets the grouping mode the list is currently built with.
		/// </summary>
		public GroupingMode? SelectedGroupingMode
		{
			get => _selectedGroupingMode;
			set
			{
				if (SetProperty(ref _selectedGroupingMode, value))
					ApplyGrouping();
			}
		}

		/// <summary>
		/// Gets a value indicating whether the list provides more than one grouping mode.
		/// </summary>
		public bool HasGroupingModes => GroupingModes.Count > 1;

		/// <summary>
		/// Gets a value indicating whether at least one element of the list is visible for the current
		/// search query.
		/// </summary>
		public bool HasVisibleItems
		{
			get => _hasVisibleItems;
			protected set => SetProperty(ref _hasVisibleItems, value);
		}

		/// <summary>
		/// Gets or sets the search query that filters the list by name, description and tags.
		/// </summary>
		public string SearchText
		{
			get => _searchText;
			set
			{
				if (SetProperty(ref _searchText, value))
					ApplyFilter();
			}
		}

		/// <summary>
		/// Gets the current elements of the list: single cards, and group cards when a grouping mode
		/// is selected.
		/// </summary>
		public RangeObservableCollection<AddonCardViewModel> Items
		{
			get => field ??= [];
			protected set => (field ??= []).Reset(value);
		}

		/// <summary>
		/// Reloads the addons from disk and rebuilds the list.
		/// </summary>
		public abstract void Update();

		/// <summary>
		/// Rebuilds <see cref="Items"/> for the current <see cref="SearchText"/>.
		/// </summary>
		protected abstract void ApplyFilter();

		/// <summary>
		/// Rebuilds <see cref="Items"/> for the current <see cref="SelectedGroupingMode"/>.
		/// </summary>
		protected abstract void ApplyGrouping();
	}

	/// <summary>
	/// The addon list implementation: takes the addons from an addon set collector, renders each of them
	/// with the card of the addon type, filters the list by the search query, groups the cards by the
	/// selected grouping mode and caches the cards.
	/// </summary>
	/// <remarks>
	/// Filtering happens before the cards are created and the cards themselves are cached per addon, so a
	/// change of the search query does not rebuild them: the elements of a card contain real controls, and
	/// creating them on every keystroke would be a waste. When an <see cref="IAddonSearchService{T}"/> is
	/// provided, the list is filtered (and ordered) by the relevance of the search service; otherwise the
	/// plain substring match is used as a fallback. The single cards are shared by every grouping mode:
	/// switching the mode only rebuilds <see cref="AddonListViewModel.Items"/> and the group cards.
	/// </remarks>
	/// <typeparam name="TAddon">The type of the addon shown by the list.</typeparam>
	/// <typeparam name="TChange">The type of the change (override) object of that addon.</typeparam>
	public class AddonListViewModel<TAddon, TChange> : AddonListViewModel
		where TAddon : AddonChangedBase<TAddon, TChange>, new()
		where TChange : AddonChangeBase, new()
	{
		private readonly IServiceProvider _services;
		private readonly ChatAgentDescriptor? _agent;
		private readonly IAddonSetCollector<TAddon> _collector;
		private readonly IAddonCardFactory<TAddon, TChange> _factory;
		private readonly IAddonManagerInvalidator _invalidator;
		private readonly AddonKind _kind;
		private readonly IAddonSearchService<TAddon>? _searchService;
		private readonly Func<AddonListViewModel<TAddon, TChange>, TAddon, AddonCardContext<TAddon, TChange>>? _contextBuilder;

		private readonly RangeObservableCollection<AddonCardViewModel> _groupCards = [];
		private readonly Dictionary<AddonCardViewModel, string> _groupIds = [];
		private readonly Dictionary<string, bool> _manualExpansion = [];

		private ImmutableList<(TAddon Addon, AddonCardViewModel Card)> _pairs = [];
		private bool _updatingGroupState;
		private bool _wasFiltering;

		/// <summary>
		/// Initializes a new instance of the <see cref="AddonListViewModel{TAddon, TChange}"/> class.
		/// </summary>
		/// <param name="services">The services to use.</param>
		/// <param name="agent">The agent descriptor, if any.</param>
		/// <param name="kind">The kind of addon to list.</param>
		/// <param name="contextBuilder">The optional builder of the card context, used when the cards need
		/// a context other than the default one (for example, when they edit the overrides of a set).</param>
		public AddonListViewModel(IServiceProvider services, ChatAgentDescriptor? agent, AddonKind kind,
			Func<AddonListViewModel<TAddon, TChange>, TAddon, AddonCardContext<TAddon, TChange>>? contextBuilder = null)
		{
			_services = services;
			_agent = agent;
			_collector = services.GetRequiredService<IAddonSetCollector<TAddon>>();
			_factory = services.GetRequiredService<IAddonCardFactory<TAddon, TChange>>();
			_invalidator = services.GetRequiredService<IAddonManagerInvalidator>();
			_kind = kind;
			_searchService = services.GetService<IAddonSearchService<TAddon>>();
			_contextBuilder = contextBuilder;
			GroupingModes = StandardGroupingModes.CreateDefault<TAddon>();
		}

		/// <inheritdoc/>
		public override void Update()
		{
			_invalidator.Reload(_kind);

			foreach (var (_, card) in _pairs)
				card.Dispose();
			DisposeGroupCards();

			_pairs = [.. GetAddons().Select(addon =>
			{
				var card = _factory.Create(CreateContext(addon));
				return (addon, card);
			})];

			RebuildItems();
			ApplyFilter();
		}

		/// <summary>
		/// Gets the addons shown by the list.
		/// </summary>
		protected virtual IEnumerable<TAddon> GetAddons()
		{
			Func<TAddon, bool> filter;
			if (_agent is null)
				filter = a => a.ChatAvailablePredicate?.Invoke(a, _services) ?? true;
			else
				filter = a => (a.ChatAvailablePredicate?.Invoke(a, _services) ?? true)
					&& (a.AgentAvailablePredicate?.Invoke(a, _services, _agent) ?? true);

			return _collector.GetAvailableAddons().Where(filter);
		}

		/// <summary>
		/// Creates the context the card of the given addon is built with.
		/// </summary>
		protected virtual AddonCardContext<TAddon, TChange> CreateContext(TAddon addon)
		{
			if (_contextBuilder is not null)
				return _contextBuilder(this, addon);

			return new AddonCardContext<TAddon, TChange>
			{
				Addon = addon,
				TagClickCommand = TagClickCommand,
				OnDeleted = Update
			};
		}

		/// <summary>
		/// Gets a value indicating whether the addon matches the search query by a plain substring match.
		/// Used only when no search service is provided.
		/// </summary>
		protected virtual bool Matches(TAddon addon, string query) =>
			addon.Name.Contains(query, StringComparison.OrdinalIgnoreCase)
			|| addon.Description.Contains(query, StringComparison.OrdinalIgnoreCase)
			|| addon.Tags.Any(tag => tag.Contains(query, StringComparison.OrdinalIgnoreCase));

		/// <inheritdoc/>
		protected override void ApplyFilter()
		{
			var query = SearchText?.Trim() ?? string.Empty;
			HashSet<TAddon>? matching = null;

			if (query.Length > 0)
			{
				matching = _searchService is not null
					? [.. _searchService.Search(query, _pairs.Select(pair => pair.Addon), maxResults: 0)
						.Select(result => result.Addon)]
					: [.. _pairs.Where(pair => Matches(pair.Addon, query)).Select(pair => pair.Addon)];
			}

			foreach (var (addon, card) in _pairs)
				card.IsVisible = matching is null || matching.Contains(addon);

			UpdateGroups(filtering: matching is not null);

			HasVisibleItems = Items.Any(card => card.IsVisible);
		}

		/// <inheritdoc/>
		protected override void ApplyGrouping()
		{
			RebuildItems();
			ApplyFilter();
		}

		/// <summary>
		/// Rebuilds <see cref="AddonListViewModel.Items"/> for the selected grouping mode, reusing the
		/// cards built by the last <see cref="Update"/>.
		/// </summary>
		private void RebuildItems()
		{
			DisposeGroupCards();

			if (SelectedGroupingMode is not GroupingMode<TAddon> mode)
			{
				Items.Reset(_pairs.Select(pair => pair.Card));
				return;
			}

			// The order entries are the groups in the order of their first appearance and the cards of
			// the addons the mode does not group.
			var order = new List<object>();
			var groups = new Dictionary<string, GroupBuilder>();

			foreach (var (addon, card) in _pairs)
			{
				var key = mode.GetGroupKey(addon);

				if (key is null)
				{
					order.Add(card);
					continue;
				}

				if (!groups.TryGetValue(key.Id, out var builder))
				{
					builder = new GroupBuilder(key);
					groups.Add(key.Id, builder);
					order.Add(builder);
				}

				builder.Addons.Add(addon);
				builder.Children.Add(card);
			}

			var cards = new List<AddonCardViewModel>(order.Count);

			foreach (var entry in order)
			{
				if (entry is AddonCardViewModel single)
				{
					cards.Add(single);
					continue;
				}

				var builder = (GroupBuilder)entry;
				var groupCard = _factory.CreateGroup(new AddonGroupCardContext<TAddon, TChange>
				{
					Key = builder.Key,
					Addons = [.. builder.Addons],
					Children = [.. builder.Children]
				});

				_groupCards.Add(groupCard);
				_groupIds.Add(groupCard, builder.Key.Id);
				groupCard.PropertyChanged += GroupCard_PropertyChanged;
				groupCard.IsChildrenExpanded = _manualExpansion.GetValueOrDefault(builder.Key.Id);
				cards.Add(groupCard);
			}

			Items.Reset(cards);
		}

		/// <summary>
		/// Refreshes the visibility of the group cards and their automatic expansion after the visibility
		/// of the children was refreshed.
		/// </summary>
		/// <param name="filtering">Whether a search query is currently active.</param>
		private void UpdateGroups(bool filtering)
		{
			var startedFiltering = filtering && !_wasFiltering;

			foreach (var groupCard in _groupCards)
			{
				var wasVisible = groupCard.IsVisible;
				var isVisible = groupCard.Children.Any(child => child.IsVisible);

				_updatingGroupState = true;

				try
				{
					groupCard.IsVisible = isVisible;

					if (!filtering)
					{
						// The automatic expansion is a part of the filtering itself: without an active
						// query the state the user left the group in is restored.
						groupCard.IsChildrenExpanded = _manualExpansion.GetValueOrDefault(_groupIds[groupCard]);
					}
					else if (isVisible && (startedFiltering || !wasVisible))
					{
						// The group appeared in the results (the query was just entered, or the group just
						// matched it): expand it.
						groupCard.IsChildrenExpanded = true;
					}
				}
				finally
				{
					_updatingGroupState = false;
				}
			}

			_wasFiltering = filtering;
		}

		private void GroupCard_PropertyChanged(object? sender, PropertyChangedEventArgs e)
		{
			if (_updatingGroupState)
				return;

			if (e.PropertyName is nameof(AddonCardViewModel.IsChildrenExpanded) && sender is AddonCardViewModel card)
				_manualExpansion[_groupIds[card]] = card.IsChildrenExpanded;
		}

		private void DisposeGroupCards()
		{
			foreach (var card in _groupCards)
			{
				card.PropertyChanged -= GroupCard_PropertyChanged;
				card.Dispose();
			}

			_groupCards.Clear();
			_groupIds.Clear();
		}

		/// <inheritdoc/>
		protected override void Dispose(bool disposing)
		{
			base.Dispose(disposing);

			if (disposing)
			{
				DisposeGroupCards();

				foreach (var (_, card) in _pairs)
					card.Dispose();

				_pairs = [];
				Items.Clear();
			}
		}

		/// <summary>
		/// The mutable group the cards of the list are distributed into while the grouped items are built.
		/// </summary>
		private sealed class GroupBuilder(AddonGroupKey key)
		{
			public AddonGroupKey Key { get; } = key;

			public List<TAddon> Addons { get; } = [];

			public List<AddonCardViewModel> Children { get; } = [];
		}
	}
}
