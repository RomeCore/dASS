using System.ComponentModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.Input;
using LLMDesktopAssistant.Addons;
using LLMDesktopAssistant.Addons.Management;
using LLMDesktopAssistant.Addons.MVVM;
using LLMDesktopAssistant.Addons.Search;
using LLMDesktopAssistant.Agents.SubAgents;
using LLMDesktopAssistant.Controls.Dialogs;
using LLMDesktopAssistant.LLM.MVVM.Settings.Agents;
using LLMDesktopAssistant.LLM.Settings;
using LLMDesktopAssistant.Localization;
using LLMDesktopAssistant.Services;
using LLMDesktopAssistant.Services.Instances;
using LLMDesktopAssistant.Tools;
using LLMDesktopAssistant.Utils;

namespace LLMDesktopAssistant.LLM.MVVM.Settings;

/// <summary>
/// ViewModel for the chat-level sub-agent settings: the behaviour policy applied when the chat calls sub-agents,
/// and the available sub-agents rendered by the reusable addon list (with the search box) and the sub-agent file actions.
/// </summary>
[ViewModelFor(typeof(ChatSubAgentsSettingsView))]
public class ChatSubAgentsSettingsViewModel : ViewModelBase, ISetPolicyMaskFlag
{
	/// <summary>
	/// Gets the underlying chat sub-agent settings.
	/// </summary>
	public ChatSubAgentSettings SubAgentSettings { get; }

	/// <summary>
	/// Gets the addon list that renders the available sub-agents and searches over them.
	/// </summary>
	public AddonListViewModel List { get; }

	/// <summary>
	/// Gets the command that creates a new sub-agent file from a template.
	/// </summary>
	public ICommand CreateSubAgentCommand { get; }

	/// <summary>
	/// Gets the behaviour policy toggles grouped by category.
	/// </summary>
	public ImmutableList<ToolBehaviourCategoryViewModel> PolicyMaskCategoryItems { get; }

	private InheritanceLevelItem _selectedPolicyInheritance;
	/// <summary>
	/// Gets or sets the inheritance level for the sub-agent behaviour policy.
	/// </summary>
	public InheritanceLevelItem SelectedPolicyInheritance
	{
		get => _selectedPolicyInheritance;
		set
		{
			if (SetProperty(ref _selectedPolicyInheritance, value) && value is not null)
				SubAgentSettings.PolicyInheritance = value.Value;
		}
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="ChatSubAgentsSettingsViewModel"/> class.
	/// </summary>
	/// <param name="settings">The chat sub-agent settings.</param>
	/// <param name="subAgentsetCollector">The collector that provides the available sub-agents.</param>
	/// <param name="cardFactory">The factory that builds the sub-agent cards.</param>
	/// <param name="addonInvalidator">The invalidator used to reload the addons before building the list.</param>
	/// <param name="searchService">The search service used to filter the list by the search query.</param>
	public ChatSubAgentsSettingsViewModel(ChatSubAgentSettings settings,
		IAddonSetCollector<SubAgentInfo> subAgentsetCollector,
		IAddonCardFactory<SubAgentInfo, SubAgentChange> cardFactory,
		IAddonManagerInvalidator addonInvalidator,
		IAddonSearchService<SubAgentInfo> searchService)
	{
		SubAgentSettings = settings;
		CreateSubAgentCommand = new AsyncRelayCommand(CreateSubAgentAsync);

		_selectedPolicyInheritance = InheritanceLevelItem.AllProfile.First(i => i.Value == settings.PolicyInheritance);
		settings.PropertyChanged += SubAgentSettings_PropertyChanged;

		PolicyMaskCategoryItems = InitializePolicyMaskItems();

		List = new AddonListViewModel<SubAgentInfo, SubAgentChange>(subAgentsetCollector, cardFactory, addonInvalidator,
			AddonKind.SubAgent, searchService)
		{
			SearchPlaceholderKey = Locale.GetKey("settings.sub_agents.search.placeholder"),
			EmptyTextKey = Locale.GetKey("settings.sub_agents.empty")
		};
		List.Update();
	}

	private void SubAgentSettings_PropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName != nameof(ChatSubAgentSettings.PolicyInheritance))
			return;

		_selectedPolicyInheritance = InheritanceLevelItem.AllProfile.First(i => i.Value == SubAgentSettings.PolicyInheritance);
		RaisePropertyChanged(nameof(SelectedPolicyInheritance));
		RefreshPolicyMaskItems();
	}

	private ImmutableList<ToolBehaviourCategoryViewModel> InitializePolicyMaskItems()
	{
		var builder = ImmutableList.CreateBuilder<ToolBehaviourCategoryViewModel>();
		var effectivePolicyMask = SubAgentSettings.GetEffectivePolicy();

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
		var mask = ToolPolicyMaskEditing.SetFlag(SubAgentSettings.GetEffectivePolicy(), flag, state);
		SubAgentSettings.SetEffectivePolicy(mask);
	}

	private void RefreshPolicyMaskItems()
	{
		var mask = SubAgentSettings.GetEffectivePolicy();
		foreach (var category in PolicyMaskCategoryItems)
			foreach (var item in category.Toggles)
				item.Refresh(ToolPolicyMaskEditing.GetFlagState(mask, item.Flag));
	}

	private async Task CreateSubAgentAsync()
	{
		var dialog = new TextInputDialogViewModel
		{
			Title = Locale.Get("settings.sub_agents.create.title"),
			Description = Locale.Get("settings.sub_agents.create.description"),
			Label = Locale.Get("settings.sub_agents.create.name.label"),
			Placeholder = Locale.Get("settings.sub_agents.create.name.placeholder"),
			SubmitText = Locale.Get("common.create"),
			CancelText = Locale.Get("common.cancel"),
			IsRequired = true
		};

		var name = (string?)await DialogManager.ShowDialogAsync(dialog);
		if (string.IsNullOrEmpty(name))
			return;

		var toast = ServiceRegistry.Provider.GetRequiredService<IToastService>();
		if (!SubAgentName.IsValidSubAgentName(name))
		{
			toast.ShowError(Locale.Get("settings.sub_agents.create.title"), Locale.Get("settings.sub_agents.create.error.invalid_name"));
			return;
		}

		var path = Path.Combine(Directories.Agents, $"{name}.md");
		if (File.Exists(path))
		{
			toast.ShowError(Locale.Get("settings.sub_agents.create.title"), Locale.Get("settings.sub_agents.create.error.exists"));
			return;
		}

		try
		{
			Directory.CreateDirectory(Directories.Agents);
			File.WriteAllText(path, BuildTemplate(name));
			List.Update();

			toast.ShowSuccess(Locale.Get("settings.sub_agents.create.success"));
			Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
		}
		catch (Exception ex)
		{
			toast.ShowError(Locale.Get("common.error"), ex.Message);
		}
	}

	private static string BuildTemplate(string name) => $"""
		---
		name: {name}
		description: A sub-agent that helps with specific tasks.
		---

		# {name}

		Write the instructions for this sub-agent here.
		""";

	/// <inheritdoc/>
	protected override void Dispose(bool disposing)
	{
		base.Dispose(disposing);

		if (disposing)
		{
			SubAgentSettings.PropertyChanged -= SubAgentSettings_PropertyChanged;
			List.Dispose();
		}
	}
}
