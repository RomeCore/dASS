using System.Diagnostics;
using CommunityToolkit.Mvvm.Input;
using LLMDesktopAssistant.Addons;
using LLMDesktopAssistant.Addons.MVVM;
using LLMDesktopAssistant.Controls.Dialogs;
using LLMDesktopAssistant.LLM.Settings;
using LLMDesktopAssistant.Localization;
using LLMDesktopAssistant.Prompting.Skills;
using LLMDesktopAssistant.Services;
using LLMDesktopAssistant.Services.Instances;
using LLMDesktopAssistant.Utils;

namespace LLMDesktopAssistant.LLM.MVVM.Settings;

/// <summary>
/// ViewModel for the chat-level skill settings: the available skills rendered by the reusable addon list
/// (with the search box) and the skill file actions.
/// </summary>
[ViewModelFor(typeof(ChatSkillsSettingsView))]
public class ChatSkillsSettingsViewModel : ViewModelBase
{
	/// <summary>
	/// Gets the underlying chat skill settings.
	/// </summary>
	public ChatSkillSettings SkillSettings { get; }

	/// <summary>
	/// Gets the addon list that renders the available skills and searches over them.
	/// </summary>
	public AddonListViewModel List { get; }

	/// <summary>
	/// Gets the command that creates a new skill file from a template.
	/// </summary>
	public ICommand CreateSkillCommand { get; }

	/// <summary>
	/// Initializes a new instance of the <see cref="ChatSkillsSettingsViewModel"/> class.
	/// </summary>
	/// <param name="services">The chat services used to build the addon list.</param>
	/// <param name="settings">The chat skill settings.</param>
	public ChatSkillsSettingsViewModel(IServiceProvider services, ChatSkillSettings settings)
	{
		SkillSettings = settings;
		CreateSkillCommand = new AsyncRelayCommand(CreateSkillAsync);

		List = new AddonListViewModel<SkillInfo, SkillChange>(services, null, AddonKind.Skill)
		{
			SearchPlaceholderKey = Locale.GetKey("settings.skills.search.placeholder"),
			EmptyTextKey = Locale.GetKey("settings.skills.empty")
		};
		List.Update();
	}

	private async Task CreateSkillAsync()
	{
		var dialog = new TextInputDialogViewModel
		{
			Title = Locale.Get("settings.skills.create.title"),
			Description = Locale.Get("settings.skills.create.description"),
			Label = Locale.Get("settings.skills.create.name.label"),
			Placeholder = Locale.Get("settings.skills.create.name.placeholder"),
			SubmitText = Locale.Get("common.create"),
			CancelText = Locale.Get("common.cancel"),
			IsRequired = true
		};

		var name = (string?)await DialogManager.ShowDialogAsync(dialog);
		if (string.IsNullOrEmpty(name))
			return;

		var toast = ServiceRegistry.Provider.GetRequiredService<IToastService>();
		if (!SkillName.IsValidSkillName(name))
		{
			toast.ShowError(Locale.Get("settings.skills.create.title"), Locale.Get("settings.skills.create.error.invalid_name"));
			return;
		}

		var directory = Path.Combine(Directories.Skills, name);
		var path = Path.Combine(directory, "SKILL.md");
		if (File.Exists(path))
		{
			toast.ShowError(Locale.Get("settings.skills.create.title"), Locale.Get("settings.skills.create.error.exists"));
			return;
		}

		try
		{
			Directory.CreateDirectory(directory);
			File.WriteAllText(path, BuildTemplate(name));
			List.Update();

			toast.ShowSuccess(Locale.Get("settings.skills.create.success"));
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
		description: A skill that helps with specific tasks.
		---

		# {name}

		Write the instructions for this skill here.
		""";

	/// <inheritdoc/>
	protected override void Dispose(bool disposing)
	{
		base.Dispose(disposing);

		if (disposing)
			List.Dispose();
	}
}
