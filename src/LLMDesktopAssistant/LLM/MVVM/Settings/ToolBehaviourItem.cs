using Avalonia.Media;
using LLMDesktopAssistant.Tools;
using Material.Icons;

namespace LLMDesktopAssistant.LLM.MVVM.Settings;

/// <summary>
/// ViewModel item for a single <see cref="ToolBehaviour"/> flag.
/// </summary>
public class ToolBehaviourItem : NotifyPropertyChanged
{
	private readonly Func<ToolBehaviour> _getCurrentValue;
	private readonly Action<ToolBehaviour> _setCurrentValue;
	private readonly ToolBehaviour _flag;
	private bool _isEnabled;

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
	public string DisplayName { get; }

	/// <summary>
	/// Localized description of the behaviour.
	/// </summary>
	public string Description { get; }

	/// <summary>
	/// Whether this behaviour flag is currently enabled.
	/// Updates the parent's ToolBehaviour value when changed.
	/// </summary>
	public bool IsEnabled
	{
		get => _isEnabled;
		set
		{
			if (SetProperty(ref _isEnabled, value))
			{
				var current = _getCurrentValue();
				_setCurrentValue(value ? current | _flag : current & ~_flag);
			}
		}
	}

	/// <summary>
	/// Creates a new <see cref="ToolBehaviourItem"/>.
	/// </summary>
	/// <param name="getCurrentValue">Function to get the current ToolBehaviour value from the parent.</param>
	/// <param name="setCurrentValue">Action to set the new ToolBehaviour value on the parent.</param>
	/// <param name="flag">The ToolBehaviour flag this item represents.</param>
	/// <param name="displayName">Localized display name.</param>
	/// <param name="description">Localized description.</param>
	public ToolBehaviourItem(
		Func<ToolBehaviour> getCurrentValue,
		Action<ToolBehaviour> setCurrentValue,
		ToolBehaviour flag,
		string displayName,
		string description)
	{
		_getCurrentValue = getCurrentValue;
		_setCurrentValue = setCurrentValue;
		_flag = flag;
		DisplayName = displayName;
		Description = description;
		_isEnabled = getCurrentValue().HasFlag(flag);
		var flagInfo = ToolBehaviourFlagInfo.Create(flag);
		Icon = flagInfo.Icon;
		Color = flagInfo.Color;
	}
}
