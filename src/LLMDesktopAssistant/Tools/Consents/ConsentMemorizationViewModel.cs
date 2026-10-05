using System.Collections.ObjectModel;
using Avalonia.Media;
using CommunityToolkit.Mvvm.Input;
using LLMDesktopAssistant.Controls.Dialogs;
using LLMDesktopAssistant.LLM.Domain;
using LLMDesktopAssistant.LLM.Services;
using LLMDesktopAssistant.LLM.Services.Agents;
using LLMDesktopAssistant.Localization;
using Material.Icons;

namespace LLMDesktopAssistant.Tools.Consents;

/// <summary>
/// Represents a session-memorized consent decision of a chat in the memorization manager.
/// </summary>
public class MemorizedConsentItemViewModel : ViewModelBase
{
	private readonly IToolMemorizationService _memorizationService;
	private readonly Chat _chat;
	private readonly string _toolName;
	private readonly Action _onForgotten;

	/// <summary>
	/// Initializes a new instance of the <see cref="MemorizedConsentItemViewModel"/> class.
	/// </summary>
	/// <param name="memorizationService">The memorization service used to forget the decision.</param>
	/// <param name="chat">The chat the decision belongs to.</param>
	/// <param name="info">The memorized decision data.</param>
	/// <param name="onForgotten">The callback invoked after the decision is forgotten.</param>
	public MemorizedConsentItemViewModel(IToolMemorizationService memorizationService, Chat chat, MemorizedConsentInfo info, Action onForgotten)
	{
		_memorizationService = memorizationService;
		_chat = chat;
		_toolName = info.ToolName;
		_onForgotten = onForgotten;
		ToolName = info.ToolName;
		Approved = info.Approved;
		Notes = info.Notes;

		ForgetCommand = new RelayCommand(Forget);
	}

	/// <summary>
	/// Gets the name of the tool.
	/// </summary>
	public string ToolName { get; }

	/// <summary>
	/// Gets whether the tool was approved (<see langword="true"/>) or denied (<see langword="false"/>).
	/// </summary>
	public bool Approved { get; }

	/// <summary>
	/// Gets the user notes or the denial reason, or <see langword="null"/>.
	/// </summary>
	public string? Notes { get; }

	/// <summary>
	/// Gets whether the decision has notes.
	/// </summary>
	public bool HasNotes => !string.IsNullOrEmpty(Notes);

	/// <summary>
	/// Gets the localized decision text.
	/// </summary>
	public string DecisionText => LocalizationManager.LocalizeStatic(
		Approved ? "tool.call.remember.manager.approved" : "tool.call.remember.manager.denied");

	/// <summary>
	/// Gets the decision icon.
	/// </summary>
	public VisualIconKind DecisionIcon => Approved ? MaterialIconKind.CheckCircle : MaterialIconKind.CloseCircle;

	/// <summary>
	/// Gets the brush used to colorize the decision icon.
	/// </summary>
	public IBrush DecisionBrush => Approved ? Brushes.Green : Brushes.Red;

	/// <summary>
	/// Gets the command that forgets this memorized decision.
	/// </summary>
	public IRelayCommand ForgetCommand { get; }

	private void Forget()
	{
		if (_memorizationService.ForgetConsent(_chat, _toolName))
			_onForgotten();
	}
}

/// <summary>
/// Represents a persisted "always" consent decision of an agent toolset in the memorization manager.
/// </summary>
public class MemorizedAlwaysItemViewModel : ViewModelBase
{
	private readonly ToolChange _change;
	private readonly Action _onForgotten;

	/// <summary>
	/// Initializes a new instance of the <see cref="MemorizedAlwaysItemViewModel"/> class.
	/// </summary>
	/// <param name="agentName">The display name of the agent owning the decision.</param>
	/// <param name="change">The tool change carrying the "always" approval level.</param>
	/// <param name="onForgotten">The callback invoked after the decision is forgotten.</param>
	public MemorizedAlwaysItemViewModel(string agentName, string toolName, ToolChange change, Action onForgotten)
	{
		AgentName = agentName;
		_change = change;
		_onForgotten = onForgotten;
		ToolName = toolName;
		Approved = change.ApprovalLevel == ToolApprovalLevel.AlwaysApprove;

		ForgetCommand = new RelayCommand(Forget);
	}

	/// <summary>
	/// Gets the display name of the agent owning the decision.
	/// </summary>
	public string AgentName { get; }

	/// <summary>
	/// Gets the name of the tool.
	/// </summary>
	public string ToolName { get; }

	/// <summary>
	/// Gets whether the tool was always approved (<see langword="true"/>) or always denied (<see langword="false"/>).
	/// </summary>
	public bool Approved { get; }

	/// <summary>
	/// Gets the localized decision text.
	/// </summary>
	public string DecisionText => LocalizationManager.LocalizeStatic(
		Approved ? "tool.call.remember.manager.approved" : "tool.call.remember.manager.denied");

	/// <summary>
	/// Gets the decision icon.
	/// </summary>
	public VisualIconKind DecisionIcon => Approved ? MaterialIconKind.CheckCircle : MaterialIconKind.CloseCircle;

	/// <summary>
	/// Gets the brush used to colorize the decision icon.
	/// </summary>
	public IBrush DecisionBrush => Approved ? Brushes.Green : Brushes.Red;

	/// <summary>
	/// Gets the command that forgets this persisted decision.
	/// </summary>
	public IRelayCommand ForgetCommand { get; }

	private void Forget()
	{
		ToolConsentPersister.ForgetAlways(_change);
		_onForgotten();
	}
}

/// <summary>
/// The view model of the consent memorization manager dialog: lists the session-memorized
/// decisions of the chat and the persisted "always" decisions of the chat agents,
/// allowing the user to forget them manually.
/// </summary>
[ViewModelFor(typeof(ConsentMemorizationView))]
public class ConsentMemorizationViewModel : ViewModelBase
{
	private readonly Chat _chat;
	private readonly IToolMemorizationService _memorizationService;
	private readonly IAgentManagementService _agentManager;
	private readonly IChatSettingsService _chatSettings;

	/// <summary>
	/// Gets the session-memorized consent decisions of the chat.
	/// </summary>
	public ObservableCollection<MemorizedConsentItemViewModel> SessionItems { get; } = [];

	/// <summary>
	/// Gets the persisted "always" consent decisions of the chat agents.
	/// </summary>
	public ObservableCollection<MemorizedAlwaysItemViewModel> AlwaysItems { get; } = [];

	/// <summary>
	/// Gets whether there are session-memorized decisions.
	/// </summary>
	public bool HasSessionItems => SessionItems.Count > 0;

	/// <summary>
	/// Gets whether there are persisted "always" decisions.
	/// </summary>
	public bool HasAlwaysItems => AlwaysItems.Count > 0;

	/// <summary>
	/// Gets whether there are no memorized decisions at all.
	/// </summary>
	public bool IsEmpty => !HasSessionItems && !HasAlwaysItems;

	/// <summary>
	/// Gets the command that forgets all session-memorized decisions of the chat.
	/// </summary>
	public IRelayCommand ClearSessionCommand { get; }

	/// <summary>
	/// Gets the command that closes the dialog.
	/// </summary>
	public IRelayCommand CloseCommand { get; }

	/// <summary>
	/// Initializes a new instance of the <see cref="ConsentMemorizationViewModel"/> class.
	/// </summary>
	/// <param name="chat">The chat to manage the memorized decisions for.</param>
	public ConsentMemorizationViewModel(Chat chat)
	{
		_chat = chat;
		_memorizationService = chat.Services.GetRequiredService<IToolMemorizationService>();
		_agentManager = chat.Services.GetRequiredService<IAgentManagementService>();
		_chatSettings = chat.Services.GetRequiredService<IChatSettingsService>();

		ClearSessionCommand = new RelayCommand(ClearSession);
		CloseCommand = new RelayCommand(() => DialogManager.CloseDialog());

		Refresh();
	}

	private void Refresh()
	{
		SessionItems.Clear();
		foreach (var info in _memorizationService.GetMemorizedConsents(_chat))
			SessionItems.Add(new MemorizedConsentItemViewModel(_memorizationService, _chat, info, Refresh));

		AlwaysItems.Clear();
		foreach (var (agent, _) in _agentManager.ListAgents())
			foreach (var (toolName, change) in ToolConsentPersister.GetAlwaysChanges(agent, _chatSettings))
				AlwaysItems.Add(new MemorizedAlwaysItemViewModel(agent.Info.Name, toolName, change, Refresh));

		RaisePropertyChanged(nameof(HasSessionItems));
		RaisePropertyChanged(nameof(HasAlwaysItems));
		RaisePropertyChanged(nameof(IsEmpty));
	}

	private void ClearSession()
	{
		_memorizationService.ClearConsents(_chat);
		Refresh();
	}
}
