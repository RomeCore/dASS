using System.ComponentModel;
using LLMDesktopAssistant.Addons;
using LLMDesktopAssistant.Addons.MVVM;
using LLMDesktopAssistant.Hooks;
using LLMDesktopAssistant.LLM.Settings;
using LLMDesktopAssistant.Localization;

namespace LLMDesktopAssistant.LLM.MVVM.Settings;

/// <summary>
/// ViewModel for the chat-level hook settings: the master enable gate, the effective hook set selection (with
/// its inheritance level and its enabled-by-default value) and the available hooks rendered by the reusable
/// addon list, where every card edits the hook overrides of the effective hook set.
/// </summary>
[ViewModelFor(typeof(ChatHooksSettingsView))]
public class ChatHooksSettingsViewModel : ViewModelBase
{
	private readonly ChatHookSettings _hookSettings;

	private InheritanceLevelItem _selectedHooksSetInheritance;

	/// <summary>
	/// Gets the underlying chat hook settings (the master gate lives here).
	/// </summary>
	public ChatHookSettings HookSettings => _hookSettings;

	/// <summary>
	/// Gets the addon list that renders the available hooks and searches over them. The cards of the list edit
	/// the changes of <see cref="EffectiveHooksSet"/>.
	/// </summary>
	public AddonListViewModel List { get; }

	/// <summary>
	/// Gets the hook set resolved by the current inheritance level. The cards edit the changes of this set.
	/// </summary>
	public HookSetConfiguration EffectiveHooksSet => _hookSettings.GetEffectiveHooksSet();

	/// <summary>
	/// Gets or sets the inheritance level of the hook set.
	/// </summary>
	public InheritanceLevelItem SelectedHooksSetInheritance
	{
		get => _selectedHooksSetInheritance;
		set
		{
			if (SetProperty(ref _selectedHooksSetInheritance, value) && value is not null)
				_hookSettings.HooksSetInheritance = value.Value;
		}
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="ChatHooksSettingsViewModel"/> class.
	/// </summary>
	/// <param name="services">The chat services used to build the addon list.</param>
	/// <param name="settings">The chat hook settings.</param>
	public ChatHooksSettingsViewModel(IServiceProvider services, ChatHookSettings settings)
	{
		_hookSettings = settings;
		_selectedHooksSetInheritance = InheritanceLevelItem.AllProfile.First(item => item.Value == settings.HooksSetInheritance);
		settings.PropertyChanged += HookSettings_PropertyChanged;

		List = new AddonListViewModel<HookInfo, HookChange>(services, null, AddonKind.Hook, (list, addon) => new AddonCardContext<HookInfo, HookChange>
		{
			Addon = addon,
			Services = services,
			SetConfig = EffectiveHooksSet,
			TagClickCommand = list.TagClickCommand,
			OnDeleted = list.Update
		})
		{
			SearchPlaceholderKey = Locale.GetKey("settings.hooks.search.placeholder"),
			EmptyTextKey = Locale.GetKey("settings.hooks.empty")
		};
		List.Update();
	}

	private void HookSettings_PropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName != nameof(ChatHookSettings.HooksSetInheritance))
			return;

		_selectedHooksSetInheritance = InheritanceLevelItem.AllProfile.First(item => item.Value == _hookSettings.HooksSetInheritance);
		RaisePropertyChanged(nameof(SelectedHooksSetInheritance));
		RaisePropertyChanged(nameof(EffectiveHooksSet));
		List.Update();
	}

	/// <inheritdoc/>
	protected override void Dispose(bool disposing)
	{
		base.Dispose(disposing);

		if (disposing)
		{
			_hookSettings.PropertyChanged -= HookSettings_PropertyChanged;
			List.Dispose();
		}
	}
}
