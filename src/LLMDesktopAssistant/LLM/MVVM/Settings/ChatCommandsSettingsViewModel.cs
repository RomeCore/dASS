using System.ComponentModel;
using LLMDesktopAssistant.Addons;
using LLMDesktopAssistant.Addons.MVVM;
using LLMDesktopAssistant.LLM.Settings;
using LLMDesktopAssistant.Localization;
using LLMDesktopAssistant.SlashCommands;

namespace LLMDesktopAssistant.LLM.MVVM.Settings;

/// <summary>
/// ViewModel for the chat-level slash command settings: the master enable gate, the effective command set
/// selection (with its inheritance level) and the available commands rendered by the reusable addon list, where
/// every card edits the command overrides of the effective command set.
/// </summary>
[ViewModelFor(typeof(ChatCommandsSettingsView))]
public class ChatCommandsSettingsViewModel : ViewModelBase
{
	private readonly ChatCommandSettings _commandSettings;

	private InheritanceLevelItem _selectedCommandsSetInheritance;

	/// <summary>
	/// Gets the underlying chat command settings (the master gate lives here).
	/// </summary>
	public ChatCommandSettings CommandSettings => _commandSettings;

	/// <summary>
	/// Gets the addon list that renders the available commands and searches over them. The cards of the list edit
	/// the changes of <see cref="EffectiveCommandsSet"/>.
	/// </summary>
	public AddonListViewModel List { get; }

	/// <summary>
	/// Gets the command set resolved by the current inheritance level. The cards edit the changes of this set.
	/// </summary>
	public SlashCommandSet EffectiveCommandsSet => _commandSettings.GetEffectiveCommandsSet();

	/// <summary>
	/// Gets or sets the inheritance level of the command set.
	/// </summary>
	public InheritanceLevelItem SelectedCommandsSetInheritance
	{
		get => _selectedCommandsSetInheritance;
		set
		{
			if (SetProperty(ref _selectedCommandsSetInheritance, value) && value is not null)
				_commandSettings.CommandsSetInheritance = value.Value;
		}
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="ChatCommandsSettingsViewModel"/> class.
	/// </summary>
	/// <param name="services">The chat services used to build the addon list.</param>
	/// <param name="settings">The chat command settings.</param>
	public ChatCommandsSettingsViewModel(IServiceProvider services, ChatCommandSettings settings)
	{
		_commandSettings = settings;
		_selectedCommandsSetInheritance = InheritanceLevelItem.AllProfile.First(item => item.Value == settings.CommandsSetInheritance);
		settings.PropertyChanged += CommandSettings_PropertyChanged;

		List = new AddonListViewModel<SlashCommandInfo, SlashCommandChange>(services, null, AddonKind.SlashCommand, (list, addon) => new AddonCardContext<SlashCommandInfo, SlashCommandChange>
			{
				Addon = addon,
				SetConfig = EffectiveCommandsSet,
				TagClickCommand = list.TagClickCommand,
				OnDeleted = list.Update
			})
		{
			SearchPlaceholderKey = Locale.GetKey("settings.commands.search.placeholder"),
			EmptyTextKey = Locale.GetKey("settings.commands.empty")
		};
		List.Update();
	}

	private void CommandSettings_PropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName != nameof(ChatCommandSettings.CommandsSetInheritance))
			return;

		_selectedCommandsSetInheritance = InheritanceLevelItem.AllProfile.First(item => item.Value == _commandSettings.CommandsSetInheritance);
		RaisePropertyChanged(nameof(SelectedCommandsSetInheritance));
		RaisePropertyChanged(nameof(EffectiveCommandsSet));
		List.Update();
	}

	/// <inheritdoc/>
	protected override void Dispose(bool disposing)
	{
		base.Dispose(disposing);

		if (disposing)
		{
			_commandSettings.PropertyChanged -= CommandSettings_PropertyChanged;
			List.Dispose();
		}
	}
}
