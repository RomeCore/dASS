using System.ComponentModel;
using LLMDesktopAssistant.Addons;
using LLMDesktopAssistant.Addons.MVVM;
using LLMDesktopAssistant.Agents;
using LLMDesktopAssistant.Agents.Settings;
using LLMDesktopAssistant.Agents.SubAgents;
using LLMDesktopAssistant.LLM.Settings;
using LLMDesktopAssistant.Localization;

namespace LLMDesktopAssistant.LLM.MVVM.Settings.Agents;

/// <summary>
/// ViewModel for the per-agent sub-agent settings: the effective sub-agentset selection and the available
/// sub-agents rendered by the reusable addon list, where every card edits the sub-agent overrides of the
/// effective sub-agentset.
/// </summary>
[ViewModelFor(typeof(AgentSubAgentSettingsView))]
public class AgentSubAgentSettingsViewModel : ViewModelBase
{
	private readonly ChatSettings _chatSettings;

	private InheritanceLevelItem _selectedSubAgentChangesInheritance;

	/// <summary>
	/// Gets the underlying agent sub-agent settings.
	/// </summary>
	public AgentSubAgentSettings SubAgentSettings { get; }

	/// <summary>
	/// Gets the addon list that renders the available sub-agents and searches over them. The cards of the
	/// list edit the changes of <see cref="EffectiveSubAgentset"/>.
	/// </summary>
	public AddonListViewModel List { get; }

	/// <summary>
	/// Gets the sub-agentset resolved by the current inheritance level. The cards edit the changes of this set.
	/// </summary>
	public SubAgentsetSettings EffectiveSubAgentset => SubAgentSettings.GetEffectiveSubAgentset(_chatSettings);

	/// <summary>
	/// Gets or sets the inheritance level of the sub-agentset.
	/// </summary>
	public InheritanceLevelItem SelectedSubAgentChangesInheritance
	{
		get => _selectedSubAgentChangesInheritance;
		set
		{
			if (SetProperty(ref _selectedSubAgentChangesInheritance, value) && value is not null)
				SubAgentSettings.SubAgentsetInheritance = value.Value;
		}
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="AgentSubAgentSettingsViewModel"/> class.
	/// </summary>
	/// <param name="services">The chat services used to build the addon list.</param>
	/// <param name="agent">The agent the list belongs to.</param>
	/// <param name="settings">The agent sub-agent settings.</param>
	/// <param name="chatSettings">The chat settings used to resolve the inherited sub-agentset.</param>
	public AgentSubAgentSettingsViewModel(IServiceProvider services, ChatAgentDescriptor agent,
		AgentSubAgentSettings settings, ChatSettings chatSettings)
	{
		SubAgentSettings = settings;
		_chatSettings = chatSettings;
		_selectedSubAgentChangesInheritance = InheritanceLevelItem.AllAgent.First(item => item.Value == settings.SubAgentsetInheritance);
		settings.PropertyChanged += SubAgentSettings_PropertyChanged;

		List = new AddonListViewModel<SubAgentInfo, SubAgentChange>(services, agent, AddonKind.SubAgent, (list, addon) => new AddonCardContext<SubAgentInfo, SubAgentChange>
			{
				Addon = addon,
				SetConfig = EffectiveSubAgentset,
				TagClickCommand = list.TagClickCommand,
				OnDeleted = list.Update
			})
		{
			SearchPlaceholderKey = Locale.GetKey("settings.sub_agents.search.placeholder"),
			EmptyTextKey = Locale.GetKey("settings.sub_agents.empty")
		};
		List.Update();
	}

	private void SubAgentSettings_PropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName != nameof(AgentSubAgentSettings.SubAgentsetInheritance))
			return;

		_selectedSubAgentChangesInheritance = InheritanceLevelItem.AllAgent.First(item => item.Value == SubAgentSettings.SubAgentsetInheritance);
		RaisePropertyChanged(nameof(SelectedSubAgentChangesInheritance));
		RaisePropertyChanged(nameof(EffectiveSubAgentset));
		List.Update();
	}

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
