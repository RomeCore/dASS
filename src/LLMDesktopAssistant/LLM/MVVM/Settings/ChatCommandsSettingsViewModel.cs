using System.ComponentModel;
using LLMDesktopAssistant.Addons;
using LLMDesktopAssistant.Addons.Management;
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
	/// <param name="settings">The chat command settings.</param>
	/// <param name="collector">The collector that provides the available commands.</param>
	/// <param name="cardFactory">The factory that builds the command cards.</param>
	/// <param name="addonInvalidator">The invalidator used to reload the addons before building the list.</param>
	public ChatCommandsSettingsViewModel(ChatCommandSettings settings,
		IAddonSetCollector<SlashCommandInfo> collector,
		IAddonCardFactory<SlashCommandInfo, SlashCommandChange> cardFactory,
		IAddonManagerInvalidator addonInvalidator)
	{
		_commandSettings = settings;
		_selectedCommandsSetInheritance = InheritanceLevelItem.AllProfile.First(item => item.Value == settings.CommandsSetInheritance);
		settings.PropertyChanged += CommandSettings_PropertyChanged;

		// The commands are never agent-searchable, so no search service is passed: the list falls back to the plain
		// substring match over name/description/tags.
		List = new AddonListViewModel<SlashCommandInfo, SlashCommandChange>(collector, cardFactory, addonInvalidator,
			AddonKind.SlashCommand, null, (list, addon) => new AddonCardContext<SlashCommandInfo, SlashCommandChange>
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
