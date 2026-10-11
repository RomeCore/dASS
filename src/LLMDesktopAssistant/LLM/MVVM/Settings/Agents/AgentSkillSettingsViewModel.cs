using System.ComponentModel;
using LLMDesktopAssistant.Addons;
using LLMDesktopAssistant.Addons.MVVM;
using LLMDesktopAssistant.Agents;
using LLMDesktopAssistant.Agents.Settings;
using LLMDesktopAssistant.LLM.Settings;
using LLMDesktopAssistant.Localization;
using LLMDesktopAssistant.Prompting.Skills;

namespace LLMDesktopAssistant.LLM.MVVM.Settings.Agents;

/// <summary>
/// ViewModel for the per-agent skill settings: the effective skillset selection and the available skills
/// rendered by the reusable addon list, where every card edits the skill overrides of the effective skillset.
/// </summary>
[ViewModelFor(typeof(AgentSkillSettingsView))]
public class AgentSkillSettingsViewModel : ViewModelBase
{
	private readonly ChatSettings _chatSettings;

	private InheritanceLevelItem _selectedSkillChangesInheritance;

	/// <summary>
	/// Gets the underlying agent skill settings.
	/// </summary>
	public AgentSkillSettings SkillSettings { get; }

	/// <summary>
	/// Gets the addon list that renders the available skills and searches over them. The cards of the list
	/// edit the changes of <see cref="EffectiveSkillset"/>.
	/// </summary>
	public AddonListViewModel List { get; }

	/// <summary>
	/// Gets the skillset resolved by the current inheritance level. The cards edit the changes of this set.
	/// </summary>
	public SkillsetSettings EffectiveSkillset => SkillSettings.GetEffectiveSkillset(_chatSettings);

	/// <summary>
	/// Gets or sets the inheritance level of the skillset.
	/// </summary>
	public InheritanceLevelItem SelectedSkillChangesInheritance
	{
		get => _selectedSkillChangesInheritance;
		set
		{
			if (SetProperty(ref _selectedSkillChangesInheritance, value) && value is not null)
				SkillSettings.SkillsetInheritance = value.Value;
		}
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="AgentSkillSettingsViewModel"/> class.
	/// </summary>
	/// <param name="services">The chat services used to build the addon list.</param>
	/// <param name="agent">The agent the list belongs to.</param>
	/// <param name="settings">The agent skill settings.</param>
	/// <param name="chatSettings">The chat settings used to resolve the inherited skillset.</param>
	public AgentSkillSettingsViewModel(IServiceProvider services, ChatAgentDescriptor agent,
		AgentSkillSettings settings, ChatSettings chatSettings)
	{
		SkillSettings = settings;
		_chatSettings = chatSettings;
		_selectedSkillChangesInheritance = InheritanceLevelItem.AllAgent.First(item => item.Value == settings.SkillsetInheritance);
		settings.PropertyChanged += SkillSettings_PropertyChanged;

		List = new AddonListViewModel<SkillInfo, SkillChange>(services, agent, AddonKind.Skill, (list, addon) => new AddonCardContext<SkillInfo, SkillChange>
			{
				Addon = addon,
				SetConfig = EffectiveSkillset,
				TagClickCommand = list.TagClickCommand,
				OnDeleted = list.Update
			})
		{
			SearchPlaceholderKey = Locale.GetKey("settings.skills.search.placeholder"),
			EmptyTextKey = Locale.GetKey("settings.skills.empty")
		};
		List.Update();
	}

	private void SkillSettings_PropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName != nameof(AgentSkillSettings.SkillsetInheritance))
			return;

		_selectedSkillChangesInheritance = InheritanceLevelItem.AllAgent.First(item => item.Value == SkillSettings.SkillsetInheritance);
		RaisePropertyChanged(nameof(SelectedSkillChangesInheritance));
		RaisePropertyChanged(nameof(EffectiveSkillset));
		List.Update();
	}

	/// <inheritdoc/>
	protected override void Dispose(bool disposing)
	{
		base.Dispose(disposing);

		if (disposing)
		{
			SkillSettings.PropertyChanged -= SkillSettings_PropertyChanged;
			List.Dispose();
		}
	}
}
