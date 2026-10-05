using Avalonia.Media;
using LLMDesktopAssistant.Localization;
using LLMDesktopAssistant.Tools;
using Material.Icons;

namespace LLMDesktopAssistant.LLM.MVVM.Settings;

/// <summary>
/// Defines the possible policy modes for a <see cref="ToolBehaviour"/> flag.
/// </summary>
public enum BehaviourPolicyMode
{
	/// <summary>
	/// No explicit policy — follow the default approval behaviour.
	/// </summary>
	Default,

	/// <summary>
	/// The behaviour is automatically approved without user confirmation.
	/// </summary>
	AutoApprove,

	/// <summary>
	/// The behaviour is disallowed and will be blocked.
	/// </summary>
	Disallowed,
}

/// <summary>
/// Represents a selectable policy mode for the ComboBox.
/// </summary>
public class PolicyModeItem
{
	/// <summary>
	/// The policy mode value.
	/// </summary>
	public BehaviourPolicyMode Value { get; }

	/// <summary>
	/// Localized display name for the mode.
	/// </summary>
	public string DisplayName { get; }

	public PolicyModeItem(BehaviourPolicyMode value, string displayName)
	{
		Value = value;
		DisplayName = displayName;
	}

	/// <summary>
	/// Gets all available policy modes with localized names.
	/// </summary>
	public static ImmutableList<PolicyModeItem> All { get; } = [
		new(BehaviourPolicyMode.Default, LocalizationManager.LocalizeStatic("settings.tool.behaviour_policy.default")),
		new(BehaviourPolicyMode.AutoApprove, LocalizationManager.LocalizeStatic("settings.tool.behaviour_policy.auto_approve")),
		new(BehaviourPolicyMode.Disallowed, LocalizationManager.LocalizeStatic("settings.tool.behaviour_policy.disallowed")),
	];
}

/// <summary>
/// ViewModel item for a single <see cref="ToolBehaviour"/> flag with
/// a policy mode selector (Default / Auto-Approve / Disallowed).
/// </summary>
public class ToolBehaviourPolicyItem : NotifyPropertyChanged
{
	private readonly Func<ToolBehaviour> _getAutoApprove;
	private readonly Action<ToolBehaviour> _setAutoApprove;
	private readonly Func<ToolBehaviour> _getDisallowed;
	private readonly Action<ToolBehaviour> _setDisallowed;
	private readonly ToolBehaviour _flag;

	/// <summary>
	/// The tool behaviour flag this item represents.
	/// </summary>
	public ToolBehaviour Flag => _flag;

	/// <summary>
	/// Gets the icon associated with this behaviour flag.
	/// </summary>
	public VisualIconKind Icon { get; }

	/// <summary>
	/// Gets the color associated with this behaviour flag.
	/// </summary>
	public IBrush Color { get; }

	/// <summary>
	/// Localized display name of the behaviour.
	/// </summary>
	public LocaleKeyBase DisplayName { get; }

	/// <summary>
	/// Localized description of the behaviour.
	/// </summary>
	public LocaleKeyBase Description { get; }

	/// <summary>
	/// Available policy modes for the ComboBox.
	/// </summary>
	public ImmutableList<PolicyModeItem> PolicyModes { get; } = PolicyModeItem.All;

	/// <summary>
	/// Gets or sets the current policy mode for this behaviour flag.
	/// </summary>
	public PolicyModeItem? PolicyMode
	{
		get
		{
			if (_getAutoApprove().HasFlag(_flag))
				return PolicyModes[1]; // AutoApprove
			if (_getDisallowed().HasFlag(_flag))
				return PolicyModes[2]; // Disallowed
			return PolicyModes[0]; // Default
		}
		set
		{
			if (value == null)
				return;

			switch (value.Value)
			{
				case BehaviourPolicyMode.AutoApprove:
					_setAutoApprove(_getAutoApprove() | _flag);
					_setDisallowed(_getDisallowed() & ~_flag);
					break;
				case BehaviourPolicyMode.Disallowed:
					_setDisallowed(_getDisallowed() | _flag);
					_setAutoApprove(_getAutoApprove() & ~_flag);
					break;
				default: // Default
					_setAutoApprove(_getAutoApprove() & ~_flag);
					_setDisallowed(_getDisallowed() & ~_flag);
					break;
			}
			RaisePropertyChanged();
		}
	}

	/// <summary>
	/// Creates a new <see cref="ToolBehaviourPolicyItem"/>.
	/// </summary>
	public ToolBehaviourPolicyItem(
		Func<ToolBehaviour> getAutoApprove,
		Action<ToolBehaviour> setAutoApprove,
		Func<ToolBehaviour> getDisallowed,
		Action<ToolBehaviour> setDisallowed,
		ToolBehaviour flag,
		LocaleKeyBase displayName,
		LocaleKeyBase description)
	{
		_getAutoApprove = getAutoApprove;
		_setAutoApprove = setAutoApprove;
		_getDisallowed = getDisallowed;
		_setDisallowed = setDisallowed;
		_flag = flag;
		DisplayName = displayName;
		Description = description;

		var flagInfo = ToolBehaviourFlagInfo.Create(flag);
		Icon = flagInfo.Icon;
		Color = flagInfo.Color;
	}
}
