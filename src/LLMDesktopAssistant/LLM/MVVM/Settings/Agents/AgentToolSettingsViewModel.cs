using System.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LLMDesktopAssistant.Addons;
using LLMDesktopAssistant.Addons.Management;
using LLMDesktopAssistant.Addons.MVVM;
using LLMDesktopAssistant.Addons.MVVM.Grouping;
using LLMDesktopAssistant.Addons.Search;
using LLMDesktopAssistant.Agents;
using LLMDesktopAssistant.Agents.Settings;
using LLMDesktopAssistant.LLM.Settings;
using LLMDesktopAssistant.Localization;
using LLMDesktopAssistant.Settings;
using LLMDesktopAssistant.Tools;
using LLMDesktopAssistant.Utils;

namespace LLMDesktopAssistant.LLM.MVVM.Settings.Agents
{
	public class ToolBehaviourCategoryViewModel
	{
		public required LocaleKeyBase Title { get; init; }

		public required ImmutableList<ToolBehaviourMaskItem> Toggles { get; init; }
	}


	[ViewModelFor(typeof(AgentToolSettingsView))]
	public class AgentToolSettingsViewModel : ViewModelBase, ISetPolicyMaskFlag
	{
		private enum IdEditMode
		{
			Create,
			Rename
		}

		private readonly ChatSettings _chatSettings;
		private IdEditMode _mode = IdEditMode.Create;

		/// <summary>
		/// Gets the underlying agent tool settings.
		/// </summary>
		public AgentToolSettings ToolSettings { get; }

		/// <summary>
		/// Gets the effective toolset settings resolved by the current inheritance level.
		/// </summary>
		public ToolsetSettings EffectiveToolset => ToolSettings.GetEffectiveToolset(_chatSettings);

		/// <summary>
		/// Gets the effective toolset configuration (custom or referenced shared).
		/// </summary>
		public ToolsetConfiguration EffectiveToolsetConfiguration => EffectiveToolset.GetEffectiveConfiguration();

		/// <summary>
		/// Gets the list of default approval level options for unchanged tools.
		/// </summary>
		public ImmutableList<ToolApprovalLevelItem> DefaultApprovalLevelList { get; } = ToolApprovalLevelItem.All;

		/// <summary>
		/// Gets or sets the default approval level for unchanged tools.
		/// </summary>
		public ToolApprovalLevelItem? DefaultApprovalLevel
		{
			get => DefaultApprovalLevelList.FirstOrDefault(i => i.Value == EffectiveToolsetConfiguration.DefaultApprovalLevel);
			set
			{
				if (value != null && DefaultApprovalLevel?.Value != value.Value)
					EffectiveToolsetConfiguration.DefaultApprovalLevel = value.Value!.Value; // Value... value... value...
			}
		}

		/// <summary>
		/// List of ToolBehaviour flags with combined Auto-Approve / Disallowed policy toggles.
		/// </summary>
		public ImmutableList<ToolBehaviourCategoryViewModel> PolicyMaskCategoryItems { get; }

		private InheritanceLevelItem _selectedPolicyInheritance;
		/// <summary>
		/// Gets or sets the inheritance level for the tool policy group.
		/// </summary>
		public InheritanceLevelItem SelectedPolicyInheritance
		{
			get => _selectedPolicyInheritance;
			set
			{
				if (SetProperty(ref _selectedPolicyInheritance, value) && value != null)
					ToolSettings.PolicyInheritance = value.Value;
			}
		}

		private InheritanceLevelItem _selectedToolsetInheritance;
		/// <summary>
		/// Gets or sets the inheritance level for the toolset group.
		/// </summary>
		public InheritanceLevelItem SelectedToolsetInheritance
		{
			get => _selectedToolsetInheritance;
			set
			{
				if (SetProperty(ref _selectedToolsetInheritance, value) && value != null)
					ToolSettings.ToolsetInheritance = value.Value;
			}
		}

		/// <summary>
		/// Gets the settings category that stores shared toolset configurations.
		/// </summary>
		public static SettingsCategory<ToolsetConfiguration> ToolsetCategory { get; } = SettingsManager.GetCategory<ToolsetConfiguration>();

		/// <summary>
		/// Gets the available shared toolset configuration IDs.
		/// </summary>
		public RangeObservableCollection<SettingsIdItemViewModel> ToolsetIds { get; } = [ ..ToolsetCategory.Ids
			.Where(c => c != SettingsObject.DefaultId)
			.Select(c => new SettingsIdItemViewModel { Id = c })
			.Prepend(SettingsIdItemViewModel.Default) ];

		private SettingsIdItemViewModel _selectedToolsetId = null!;
		/// <summary>
		/// Gets or sets the selected shared toolset configuration.
		/// </summary>
		public SettingsIdItemViewModel SelectedToolsetId
		{
			get => _selectedToolsetId;
			set
			{
				if (value == null)
					value = SettingsIdItemViewModel.Default;
				if (SetProperty(ref _selectedToolsetId, value))
				{
					EffectiveToolset.Reference.Id = value.Id;
					RaisePropertyChanged(nameof(EffectiveToolsetConfiguration));
					RaisePropertyChanged(nameof(DefaultApprovalLevel));
					List.Update();
				}
			}
		}

		private bool _isEditingId;
		/// <summary>
		/// Gets or sets a value indicating whether the toolset ID editor is visible.
		/// </summary>
		public bool IsEditingId
		{
			get => _isEditingId;
			set => SetProperty(ref _isEditingId, value);
		}

		private string? _newId;
		/// <summary>
		/// Gets or sets the toolset ID being created or renamed.
		/// </summary>
		public string? NewId
		{
			get => _newId;
			set => SetProperty(ref _newId, value);
		}

		/// <summary>
		/// Gets or sets a value indicating whether the custom toolset is used instead of the referenced shared one.
		/// </summary>
		public bool UseCustomToolset
		{
			get => EffectiveToolset.UseCustomToolset;
			set
			{
				if (EffectiveToolset.UseCustomToolset != value)
				{
					EffectiveToolset.UseCustomToolset = value;
					RaisePropertyChanged(nameof(EffectiveToolsetConfiguration));
					RaisePropertyChanged(nameof(DefaultApprovalLevel));
					List.Update();
				}
			}
		}

		public ICommand CreateNewIdCommand { get; }
		public ICommand RenameIdCommand { get; }
		public ICommand RemoveIdCommand { get; }
		public ICommand ConfirmEditIdCommand { get; }
		public ICommand CancelEditIdCommand { get; }

		/// <summary>
		/// Gets the addon list that renders the available tools, searches over them and groups them by the
		/// selected grouping mode. The cards of the list edit the changes of <see cref="EffectiveToolsetConfiguration"/>.
		/// </summary>
		public AddonListViewModel List { get; }

		/// <summary>
		/// Initializes a new instance of the <see cref="AgentToolSettingsViewModel"/> class.
		/// </summary>
		/// <param name="settings">The agent tool settings to edit.</param>
		/// <param name="toolsetBuildingService">The toolset building service used to enumerate available tools.</param>
		/// <param name="chatSettings">The chat settings used to resolve inherited settings.</param>
		/// <param name="cardFactory">The factory that builds the tool cards.</param>
		/// <param name="addonInvalidator">The invalidator used to reload the addons before building the list.</param>
		/// <param name="searchService">The search service used to filter the list by the search query.</param>
		public AgentToolSettingsViewModel(AgentToolSettings settings, IAddonSetCollector<ToolInfo> toolsetBuildingService,
			ChatSettings chatSettings, IAddonCardFactory<ToolInfo, ToolChange> cardFactory,
			IAddonManagerInvalidator addonInvalidator, IAddonSearchService<ToolInfo> searchService)
		{
			_chatSettings = chatSettings;
			ToolSettings = settings;

			_selectedPolicyInheritance = InheritanceLevelItem.AllAgent.First(i => i.Value == settings.PolicyInheritance);
			_selectedToolsetInheritance = InheritanceLevelItem.AllAgent.First(i => i.Value == settings.ToolsetInheritance);
			_selectedToolsetId = ToolsetIds.FirstOrDefault(i => i.Id == EffectiveToolset.Reference.Id) ?? SettingsIdItemViewModel.Default;

			settings.PropertyChanged += ToolSettings_PropertyChanged;

			PolicyMaskCategoryItems = InitializePolicyMaskItems();

			List = new AddonListViewModel<ToolInfo, ToolChange>(toolsetBuildingService, cardFactory, addonInvalidator,
				AddonKind.Tool, searchService, (list, addon) => new AddonCardContext<ToolInfo, ToolChange>
				{
					Addon = addon,
					SetConfig = EffectiveToolsetConfiguration,
					TagClickCommand = list.TagClickCommand,
					OnDeleted = list.Update
				})
			{
				SearchPlaceholderKey = Locale.GetKey("settings.tools.search.placeholder"),
				EmptyTextKey = Locale.GetKey("settings.tools.empty")
			};
			List.SelectedGroupingMode = List.GroupingModes.First(mode => mode is CategoryGroupingMode<ToolInfo>);
			List.Update();

			CreateNewIdCommand = new RelayCommand(CreateNewId);
			RenameIdCommand = new RelayCommand(RenameId);
			RemoveIdCommand = new RelayCommand(RemoveId);
			ConfirmEditIdCommand = new RelayCommand(ConfirmEditId);
			CancelEditIdCommand = new RelayCommand(() => IsEditingId = false);
		}

		private void ToolSettings_PropertyChanged(object? sender, PropertyChangedEventArgs e)
		{
			switch (e.PropertyName)
			{
				case nameof(AgentToolSettings.PolicyInheritance):
					_selectedPolicyInheritance = InheritanceLevelItem.AllAgent.First(i => i.Value == ToolSettings.PolicyInheritance);
					RaisePropertyChanged(nameof(SelectedPolicyInheritance));
					RefreshPolicyMaskItems();
					break;

				case nameof(AgentToolSettings.ToolsetInheritance):
					_selectedToolsetInheritance = InheritanceLevelItem.AllAgent.First(i => i.Value == ToolSettings.ToolsetInheritance);
					_selectedToolsetId = ToolsetIds.FirstOrDefault(i => i.Id == EffectiveToolset.Reference.Id) ?? SettingsIdItemViewModel.Default;
					RaisePropertyChanged(nameof(SelectedToolsetInheritance));
					RaisePropertyChanged(nameof(EffectiveToolset));
					RaisePropertyChanged(nameof(EffectiveToolsetConfiguration));
					RaisePropertyChanged(nameof(DefaultApprovalLevel));
					RaisePropertyChanged(nameof(SelectedToolsetId));
					RaisePropertyChanged(nameof(UseCustomToolset));
					List.Update();
					break;
			}
		}

		private ImmutableList<ToolBehaviourCategoryViewModel> InitializePolicyMaskItems()
		{
			var builder = ImmutableList.CreateBuilder<ToolBehaviourCategoryViewModel>();
			var effectivePolicyMask = ToolSettings.GetEffectivePolicy(_chatSettings);

			foreach (var (category, flags) in ToolBehaviours.ByCategory)
			{
				builder.Add(new ToolBehaviourCategoryViewModel
				{
					Title = Locale.GetKey($"tool.behaviour.category.{category.ToString().ToLower()}"),
					Toggles = flags.Select(f => new ToolBehaviourMaskItem(this,
						ToolBehaviourFlagInfo.Create(f), ToolPolicyMaskEditing.GetFlagState(effectivePolicyMask, f), true))
						.ToImmutableList()
				});
			}

			return builder.ToImmutableList();
		}

		/// <inheritdoc/>
		public void SetPolicyMaskFlag(ToolBehaviour flag, bool? state)
		{
			var mask = ToolPolicyMaskEditing.SetFlag(ToolSettings.GetEffectivePolicy(_chatSettings), flag, state);
			ToolSettings.SetEffectivePolicy(_chatSettings, mask);
		}

		private void RefreshPolicyMaskItems()
		{
			var mask = ToolSettings.GetEffectivePolicy(_chatSettings);
			foreach (var category in PolicyMaskCategoryItems)
				foreach (var item in category.Toggles)
					item.Refresh(ToolPolicyMaskEditing.GetFlagState(mask, item.Flag));
		}

		private void CreateNewId()
		{
			_mode = IdEditMode.Create;
			IsEditingId = true;
			NewId = null;
		}

		private void RenameId()
		{
			_mode = IdEditMode.Rename;
			IsEditingId = true;
			NewId = EffectiveToolset.Reference.Id;
		}

		private void RemoveId()
		{
			var currentId = EffectiveToolset.Reference.Id;
			if (ToolsetCategory.Remove(currentId))
			{
				if (currentId != SettingsObject.DefaultId)
					ToolsetIds.Remove(new SettingsIdItemViewModel { Id = currentId });
				SelectedToolsetId = SettingsIdItemViewModel.Default;
			}
		}

		private void ConfirmEditId()
		{
			var oldId = EffectiveToolset.Reference.Id;
			switch (_mode)
			{
				case IdEditMode.Create:

					if (!string.IsNullOrWhiteSpace(NewId) && ToolsetCategory.Copy(oldId, NewId))
					{
						if (NewId != SettingsObject.DefaultId && !ToolsetIds.Any(c => c.Id == NewId))
							ToolsetIds.Add(new SettingsIdItemViewModel { Id = NewId });

						SelectedToolsetId = new SettingsIdItemViewModel { Id = NewId };
						IsEditingId = false;
						NewId = null;
					}

					break;

				case IdEditMode.Rename:

					var newId = NewId == SettingsIdItemViewModel.Default.DisplayId ? SettingsObject.DefaultId : NewId;
					if (!string.IsNullOrWhiteSpace(newId) &&
						newId != oldId &&
						ToolsetCategory.Rename(oldId, newId))
					{
						if (newId != SettingsObject.DefaultId && !ToolsetIds.Any(c => c.Id == newId))
							ToolsetIds.Add(new SettingsIdItemViewModel { Id = newId });
						if (oldId != SettingsObject.DefaultId)
							ToolsetIds.Remove(new SettingsIdItemViewModel { Id = oldId });
						SelectedToolsetId = new SettingsIdItemViewModel { Id = newId };

						ToolsetCategory.Get(SettingsObject.DefaultId); // Ensure default settings are loaded if they were renamed.
						IsEditingId = false;
						NewId = null;
					}

					break;
			}
		}

		/// <inheritdoc/>
		protected override void Dispose(bool disposing)
		{
			base.Dispose(disposing);

			if (disposing)
			{
				ToolSettings.PropertyChanged -= ToolSettings_PropertyChanged;
				List.Dispose();
			}
		}
	}
}
