using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LLMDesktopAssistant.Addons;
using LLMDesktopAssistant.Agents;
using LLMDesktopAssistant.Agents.Settings;
using LLMDesktopAssistant.LLM.Services.Prompting;
using LLMDesktopAssistant.LLM.Settings;
using LLMDesktopAssistant.Localization;
using LLMDesktopAssistant.Prompting;
using LLMDesktopAssistant.Prompting.Context;
using LLMDesktopAssistant.Prompting.Management;
using LLMDesktopAssistant.Utils;

namespace LLMDesktopAssistant.LLM.MVVM.Settings.Agents;

/// <summary>
/// A type filter for the prompt part search picker.
/// Note: the <see cref="PromptSlotElement"/> type is expanded into its concrete slot kinds
/// (system / persona / specialization), so the generic "slot" type is never shown.
/// </summary>
public class PromptPartTypeFilterItem
{
	public LocaleKeyBase DisplayNameKey { get; }

	public Func<PromptPartBase, bool>? Predicate { get; }

	public PromptPartTypeFilterItem(LocaleKeyBase displayNameKey, Func<PromptPartBase, bool>? predicate)
	{
		DisplayNameKey = displayNameKey;
		Predicate = predicate;
	}
}

/// <summary>
/// A single prompt component checkbox item of the components section.
/// </summary>
public class ComponentItemViewModel : NotifyPropertyChanged
{
	private readonly AgentPromptSettingsViewModel _parent;
	private bool _isSelected;

	public PromptComponent Component { get; }

	public ComponentItemViewModel(AgentPromptSettingsViewModel parent, PromptComponent component)
	{
		_parent = parent;
		Component = component;
		_isSelected = parent.IsComponentSelected(component.Guid);
	}

	public string Name => Component.Name;

	public bool IsSelected
	{
		get => _isSelected;
		set
		{
			if (SetProperty(ref _isSelected, value))
				_parent.SetComponentSelected(Component, value);
		}
	}

	/// <summary>
	/// Refreshes the checkbox state from the effective selection without invoking the parent callback.
	/// </summary>
	public void SyncSelection()
	{
		var selected = _parent.IsComponentSelected(Component.Guid);
		if (_isSelected != selected)
		{
			_isSelected = selected;
			RaisePropertyChanged(nameof(IsSelected));
		}
	}
}

/// <summary>
/// A category group of the components section.
/// </summary>
public class ComponentCategoryViewModel : NotifyPropertyChanged
{
	public string CategoryName { get; }

	public ObservableCollection<ComponentItemViewModel> Components { get; } = [];

	public ComponentCategoryViewModel(string categoryName)
	{
		CategoryName = categoryName;
	}
}

/// <summary>
/// ViewModel for the agent prompt settings: a searchable library picker of reusable prompt parts
/// (slot elements and components) plus compact sections for the system prompt, prompt components,
/// the persona and the specialization. Each slot section supports a custom text or a registered
/// element picked from a combo box with an optional parameter editor.
/// </summary>
[ViewModelFor(typeof(AgentPromptSettingsView))]
public class AgentPromptSettingsViewModel : ViewModelBase
{
	private readonly ChatSettings _chatSettings;
	private readonly IAddonSetCollector<PromptContextInfo> _promptContextCollector;
	private readonly ChatAgentDescriptor _agent;
	private readonly IPromptSlotElementManager _slotElementManager;
	private readonly IPromptComponentManager _componentManager;

	/// <summary>
	/// Gets the underlying agent prompt settings.
	/// </summary>
	public AgentPromptSettings PromptSettings { get; }

	/// <summary>
	/// Gets the effective system prompt group resolved by the current inheritance level.
	/// </summary>
	public SystemPromptSettings EffectiveSystemPrompt => PromptSettings.GetEffectiveSystemPrompt(_chatSettings);

	/// <summary>
	/// Gets the effective persona group resolved by the current inheritance level.
	/// </summary>
	public PersonaSettings EffectivePersona => PromptSettings.GetEffectivePersona(_chatSettings);

	/// <summary>
	/// Gets the effective specialization group resolved by the current inheritance level.
	/// </summary>
	public SpecializationSettings EffectiveSpecialization => PromptSettings.GetEffectiveSpecialization(_chatSettings);

	/// <summary>
	/// Gets the effective prompt component selections resolved by the current inheritance level.
	/// </summary>
	public RangeObservableCollection<PromptPartKeyedSelection<Guid>> EffectivePromptComponents => PromptSettings.GetEffectivePromptComponents(_chatSettings);

	private InheritanceLevelItem _selectedSystemPromptInheritance;
	/// <summary>
	/// Gets or sets the inheritance level for the system prompt group.
	/// </summary>
	public InheritanceLevelItem SelectedSystemPromptInheritance
	{
		get => _selectedSystemPromptInheritance;
		set
		{
			if (SetProperty(ref _selectedSystemPromptInheritance, value) && value != null)
				PromptSettings.SystemPromptInheritance = value.Value;
		}
	}

	private InheritanceLevelItem _selectedComponentsInheritance;
	/// <summary>
	/// Gets or sets the inheritance level for the prompt components group.
	/// </summary>
	public InheritanceLevelItem SelectedComponentsInheritance
	{
		get => _selectedComponentsInheritance;
		set
		{
			if (SetProperty(ref _selectedComponentsInheritance, value) && value != null)
				PromptSettings.PromptComponentsInheritance = value.Value;
		}
	}

	private InheritanceLevelItem _selectedPersonaInheritance;
	/// <summary>
	/// Gets or sets the inheritance level for the persona group.
	/// </summary>
	public InheritanceLevelItem SelectedPersonaInheritance
	{
		get => _selectedPersonaInheritance;
		set
		{
			if (SetProperty(ref _selectedPersonaInheritance, value) && value != null)
				PromptSettings.PersonaInheritance = value.Value;
		}
	}

	private InheritanceLevelItem _selectedSpecializationInheritance;
	/// <summary>
	/// Gets or sets the inheritance level for the specialization group.
	/// </summary>
	public InheritanceLevelItem SelectedSpecializationInheritance
	{
		get => _selectedSpecializationInheritance;
		set
		{
			if (SetProperty(ref _selectedSpecializationInheritance, value) && value != null)
				PromptSettings.SpecializationInheritance = value.Value;
		}
	}

	private string _searchText = string.Empty;
	/// <summary>
	/// Gets or sets the search text filtering the library picker by name, description and category.
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
	/// The type filters available in the library picker.
	/// </summary>
	public ObservableCollection<PromptPartTypeFilterItem> TypeFilters { get; } = [];

	private PromptPartTypeFilterItem _selectedTypeFilter = null!;
	/// <summary>
	/// Gets or sets the active type filter of the library picker.
	/// </summary>
	public PromptPartTypeFilterItem SelectedTypeFilter
	{
		get => _selectedTypeFilter;
		set
		{
			if (SetProperty(ref _selectedTypeFilter, value) && value != null)
				ApplyFilter();
		}
	}

	/// <summary>
	/// All prompt part cards (slot elements and components), unfiltered.
	/// </summary>
	private readonly List<PromptPartCardViewModel> _allCards = [];

	/// <summary>
	/// The filtered prompt part cards shown in the library picker.
	/// </summary>
	public ObservableCollection<PromptPartCardViewModel> SearchResults { get; } = [];

	/// <summary>
	/// Gets a value indicating whether the library picker has no visible results.
	/// </summary>
	public bool IsSearchEmpty => SearchResults.Count == 0;

	/// <summary>
	/// The system prompt slot section.
	/// </summary>
	public PromptSlotSectionViewModel<SystemPromptSettings>? SystemPromptSection { get; private set; }

	/// <summary>
	/// The persona slot section.
	/// </summary>
	public PromptSlotSectionViewModel<PersonaSettings>? PersonaSection { get; private set; }

	/// <summary>
	/// The specialization slot section.
	/// </summary>
	public PromptSlotSectionViewModel<SpecializationSettings>? SpecializationSection { get; private set; }

	/// <summary>
	/// The components section, grouped by category.
	/// </summary>
	public ObservableCollection<ComponentCategoryViewModel> ComponentCategories { get; } = [];

	private bool _isPreviewVisible;
	/// <summary>
	/// Whether the system prompt preview is visible.
	/// </summary>
	public bool IsPreviewVisible
	{
		get => _isPreviewVisible;
		set => SetProperty(ref _isPreviewVisible, value);
	}

	private string _systemPromptPreview = string.Empty;
	/// <summary>
	/// The fully rendered system prompt preview text.
	/// </summary>
	public string SystemPromptPreview
	{
		get => _systemPromptPreview;
		private set => SetProperty(ref _systemPromptPreview, value);
	}

	public ICommand TogglePreviewCommand { get; }

	public AgentPromptSettingsViewModel(
		AgentPromptSettings settings,
		ChatSettings chatSettings,
		IAddonSetCollector<PromptContextInfo> promptContextCollector,
		ChatAgentDescriptor agent,
		IPromptComponentManager componentManager,
		IPromptSlotElementManager slotElementManager)
	{
		PromptSettings = settings;
		_chatSettings = chatSettings;
		_promptContextCollector = promptContextCollector;
		_agent = agent;

		_componentManager = componentManager;
		_slotElementManager = slotElementManager;

		_selectedSystemPromptInheritance = InheritanceLevelItem.AllAgent.First(i => i.Value == settings.SystemPromptInheritance);
		_selectedComponentsInheritance = InheritanceLevelItem.AllAgent.First(i => i.Value == settings.PromptComponentsInheritance);
		_selectedPersonaInheritance = InheritanceLevelItem.AllAgent.First(i => i.Value == settings.PersonaInheritance);
		_selectedSpecializationInheritance = InheritanceLevelItem.AllAgent.First(i => i.Value == settings.SpecializationInheritance);

		TogglePreviewCommand = new RelayCommand(() => IsPreviewVisible = !IsPreviewVisible);

		BuildTypeFilters();

		settings.PropertyChanged += PromptSettings_PropertyChanged;
		SubscribeEffectiveComponents();

		Refresh();
		RebuildSections();
		RegeneratePreview();
	}

	private void BuildTypeFilters()
	{
		TypeFilters.Add(new PromptPartTypeFilterItem(Locale.GetKey("prompt.type.all"), null));
		TypeFilters.Add(new PromptPartTypeFilterItem(Locale.GetKey("prompt.kind.system"), p => p is PromptSlotElement { Kind: PromptSlotKind.System }));
		TypeFilters.Add(new PromptPartTypeFilterItem(Locale.GetKey("prompt.kind.persona"), p => p is PromptSlotElement { Kind: PromptSlotKind.Persona }));
		TypeFilters.Add(new PromptPartTypeFilterItem(Locale.GetKey("prompt.kind.specialization"), p => p is PromptSlotElement { Kind: PromptSlotKind.Specialization }));
		TypeFilters.Add(new PromptPartTypeFilterItem(Locale.GetKey("prompt.type.component"), p => p is PromptComponent));

		_selectedTypeFilter = TypeFilters[0];
		RaisePropertyChanged(nameof(SelectedTypeFilter));
	}

	private void PromptSettings_PropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		switch (e.PropertyName)
		{
			case nameof(AgentPromptSettings.SystemPromptInheritance):
				_selectedSystemPromptInheritance = InheritanceLevelItem.AllAgent.First(i => i.Value == PromptSettings.SystemPromptInheritance);
				RaisePropertyChanged(nameof(SelectedSystemPromptInheritance));
				break;

			case nameof(AgentPromptSettings.PromptComponentsInheritance):
				_selectedComponentsInheritance = InheritanceLevelItem.AllAgent.First(i => i.Value == PromptSettings.PromptComponentsInheritance);
				RaisePropertyChanged(nameof(SelectedComponentsInheritance));
				break;

			case nameof(AgentPromptSettings.PersonaInheritance):
				_selectedPersonaInheritance = InheritanceLevelItem.AllAgent.First(i => i.Value == PromptSettings.PersonaInheritance);
				RaisePropertyChanged(nameof(SelectedPersonaInheritance));
				break;

			case nameof(AgentPromptSettings.SpecializationInheritance):
				_selectedSpecializationInheritance = InheritanceLevelItem.AllAgent.First(i => i.Value == PromptSettings.SpecializationInheritance);
				RaisePropertyChanged(nameof(SelectedSpecializationInheritance));
				break;
		}

		SubscribeEffectiveComponents();
		Refresh();
		RebuildSections();
		RegeneratePreview();
	}

	private RangeObservableCollection<PromptPartKeyedSelection<Guid>>? _subscribedComponents;

	private void SubscribeEffectiveComponents()
	{
		var components = EffectivePromptComponents;
		if (ReferenceEquals(_subscribedComponents, components))
			return;
		if (_subscribedComponents is not null)
			_subscribedComponents.CollectionChanged -= EffectiveComponents_CollectionChanged;
		_subscribedComponents = components;
		components.CollectionChanged += EffectiveComponents_CollectionChanged;
	}

	private void EffectiveComponents_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
	{
		RefreshSelectionStates();
		RefreshComponentItems();
		RegeneratePreview();
	}

	/// <summary>
	/// Regenerates the system prompt preview from the prompt sections.
	/// </summary>
	public void RegeneratePreview() => _ = RegeneratePreviewAsync();

	private async Task RegeneratePreviewAsync()
	{
		try
		{
			var header = await _promptContextCollector.GetAddonsForAgent(_agent)
				.Select(c => c.Provider).Anchored().RenderHeaderAsync(_agent);
			SystemPromptPreview = header.Text;
		}
		catch (Exception ex)
		{
			SystemPromptPreview = $"// Failed to render preview: {ex.Message}";
		}
	}

	/// <summary>
	/// Rebuilds the card lists from the current managers and effective settings.
	/// </summary>
	public void Refresh()
	{
		_allCards.ForEach(c => c.Dispose());
		_allCards.Clear();

		// --- Slot elements ---
		foreach (var element in _slotElementManager.GetAll())
		{
			PromptPartKeyedSelection<Guid>? selection = element.Kind switch
			{
				PromptSlotKind.System => EffectiveSystemPrompt,
				PromptSlotKind.Persona => EffectivePersona,
				PromptSlotKind.Specialization => EffectiveSpecialization,
				_ => null
			};
			_allCards.Add(new PromptPartCardViewModel(this, element, element.Kind, selection, isRadio: true));
		}

		// --- Components ---
		var componentSelections = EffectivePromptComponents;
		foreach (var component in _componentManager.GetAll())
		{
			var selection = componentSelections.FirstOrDefault(s => s.Id == component.Guid);
			_allCards.Add(new PromptPartCardViewModel(this, component, null, selection, isRadio: false));
		}

		RefreshComponentCategories();
		ApplyFilter();
	}

	private void RefreshComponentCategories()
	{
		ComponentCategories.Clear();
		var grouped = _componentManager.GetAll()
			.GroupBy(c => string.IsNullOrWhiteSpace(c.Category)
				? LocalizationManager.LocalizeStatic("prompt.category.uncategorized")
				: c.Category);

		foreach (var group in grouped.OrderBy(g => g.Key))
		{
			var categoryVm = new ComponentCategoryViewModel(group.Key);
			foreach (var component in group.OrderBy(c => c.Name))
				categoryVm.Components.Add(new ComponentItemViewModel(this, component));
			ComponentCategories.Add(categoryVm);
		}
	}

	/// <summary>
	/// Recreates the slot sections for the current effective settings.
	/// </summary>
	private void RebuildSections()
	{
		SystemPromptSection?.Dispose();
		PersonaSection?.Dispose();
		SpecializationSection?.Dispose();

		SystemPromptSection = CreateSection(PromptSlotKind.System, EffectiveSystemPrompt,
			s => s.UseCustomSystemPrompt, (s, v) => s.UseCustomSystemPrompt = v,
			s => s.CustomSystemPrompt, (s, v) => s.CustomSystemPrompt = v);

		PersonaSection = CreateSection(PromptSlotKind.Persona, EffectivePersona,
			s => s.UseCustomPersona, (s, v) => s.UseCustomPersona = v,
			s => s.CustomPersona, (s, v) => s.CustomPersona = v);

		SpecializationSection = CreateSection(PromptSlotKind.Specialization, EffectiveSpecialization,
			s => s.UseCustomSpecialization, (s, v) => s.UseCustomSpecialization = v,
			s => s.CustomSpecialization, (s, v) => s.CustomSpecialization = v);

		RaisePropertyChanged(nameof(SystemPromptSection));
		RaisePropertyChanged(nameof(PersonaSection));
		RaisePropertyChanged(nameof(SpecializationSection));
	}

	private PromptSlotSectionViewModel<TSettings> CreateSection<TSettings>(
		PromptSlotKind kind,
		TSettings settings,
		Func<TSettings, bool> getUseCustom,
		Action<TSettings, bool> setUseCustom,
		Func<TSettings, string?> getCustomText,
		Action<TSettings, string?> setCustomText)
		where TSettings : PromptPartKeyedSelection<Guid>
	{
		return new PromptSlotSectionViewModel<TSettings>(
			kind,
			settings,
			() => getUseCustom(settings),
			v => setUseCustom(settings, v),
			() => getCustomText(settings),
			v => setCustomText(settings, v),
			_slotElementManager,
			OnSectionChanged);
	}

	private void OnSectionChanged()
	{
		RefreshSelectionStates();
		RegeneratePreview();
	}

	/// <summary>
	/// Gets a value indicating whether the given card is selected for the agent.
	/// </summary>
	public bool IsSelected(PromptPartCardViewModel card)
	{
		if (card.IsRadio)
		{
			var guid = card.Part.Guid;
			return card.Kind switch
			{
				PromptSlotKind.System => !EffectiveSystemPrompt.UseCustomSystemPrompt && EffectiveSystemPrompt.Id == guid,
				PromptSlotKind.Persona => !EffectivePersona.UseCustomPersona && EffectivePersona.Id == guid,
				PromptSlotKind.Specialization => !EffectiveSpecialization.UseCustomSpecialization && EffectiveSpecialization.Id == guid,
				_ => false
			};
		}

		return EffectivePromptComponents.Any(s => s.Id == card.Part.Guid);
	}

	/// <summary>
	/// Selects a radio card (slot element) for the agent, or deselects it when already selected.
	/// </summary>
	public void SelectCard(PromptPartCardViewModel card)
	{
		if (!card.IsRadio)
			return;

		if (IsSelected(card))
		{
			DeselectCard(card);
			return;
		}

		switch (card.Kind)
		{
			case PromptSlotKind.System:
				SystemPromptSection?.SelectOption(card.Part.Guid);
				break;
			case PromptSlotKind.Persona:
				PersonaSection?.SelectOption(card.Part.Guid);
				break;
			case PromptSlotKind.Specialization:
				SpecializationSection?.SelectOption(card.Part.Guid);
				break;
		}
	}

	/// <summary>
	/// Deselects the given radio card (clears the slot selection).
	/// </summary>
	public void DeselectCard(PromptPartCardViewModel card)
	{
		if (!card.IsRadio)
			return;

		switch (card.Kind)
		{
			case PromptSlotKind.System:
				SystemPromptSection?.Clear();
				break;
			case PromptSlotKind.Persona:
				PersonaSection?.Clear();
				break;
			case PromptSlotKind.Specialization:
				SpecializationSection?.Clear();
				break;
		}
	}

	/// <summary>
	/// Sets a checkbox card (component) selection for the agent.
	/// </summary>
	public void SetComponentSelected(PromptPartCardViewModel card, bool selected)
	{
		var collection = EffectivePromptComponents;
		var existing = collection.FirstOrDefault(s => s.Id == card.Part.Guid);

		if (selected && existing is null)
		{
			var selection = new PromptPartKeyedSelection<Guid> { Id = card.Part.Guid };
			card.SetSelection(selection);
			collection.Add(selection);
			card.EnsureParameters();
		}
		else if (!selected && existing is not null)
		{
			collection.Remove(existing);
			card.SetSelection(null);
		}
	}

	/// <summary>
	/// Sets a component selection for the agent (used by the components section checkboxes).
	/// </summary>
	public void SetComponentSelected(PromptComponent component, bool selected)
	{
		var collection = EffectivePromptComponents;
		var existing = collection.FirstOrDefault(s => s.Id == component.Guid);

		if (selected && existing is null)
			collection.Add(new PromptPartKeyedSelection<Guid> { Id = component.Guid });
		else if (!selected && existing is not null)
			collection.Remove(existing);
	}

	/// <summary>
	/// Gets a value indicating whether the component with the given GUID is selected.
	/// </summary>
	public bool IsComponentSelected(Guid guid) => EffectivePromptComponents.Any(s => s.Id == guid);

	private void RefreshSelectionStates()
	{
		foreach (var card in _allCards)
			card.NotifySelectionChanged();
	}

	private void RefreshComponentItems()
	{
		foreach (var category in ComponentCategories)
		{
			foreach (var item in category.Components)
				item.SyncSelection();
		}
	}

	private void ApplyFilter()
	{
		var query = SearchText?.Trim() ?? string.Empty;
		var typePredicate = SelectedTypeFilter?.Predicate;

		SearchResults.Clear();
		foreach (var card in _allCards)
		{
			if (typePredicate is not null && !typePredicate(card.Part))
				continue;
			if (query.Length > 0 &&
				!card.Name.Contains(query, StringComparison.OrdinalIgnoreCase) &&
				!card.Description.Contains(query, StringComparison.OrdinalIgnoreCase) &&
				!card.Category.Contains(query, StringComparison.OrdinalIgnoreCase))
			{
				continue;
			}
			SearchResults.Add(card);
		}

		RaisePropertyChanged(nameof(IsSearchEmpty));
	}

	/// <inheritdoc/>
	protected override void Dispose(bool disposing)
	{
		base.Dispose(disposing);

		if (disposing)
		{
			PromptSettings.PropertyChanged -= PromptSettings_PropertyChanged;
			if (_subscribedComponents is not null)
				_subscribedComponents.CollectionChanged -= EffectiveComponents_CollectionChanged;

			SystemPromptSection?.Dispose();
			PersonaSection?.Dispose();
			SpecializationSection?.Dispose();

			_allCards.ForEach(c => c.Dispose());
			_allCards.Clear();
		}
	}
}
