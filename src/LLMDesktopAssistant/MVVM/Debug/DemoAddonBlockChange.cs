using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Layout;
using CommunityToolkit.Mvvm.Input;
using LLMDesktopAssistant.Addons.MVVM;
using LLMDesktopAssistant.Localization;
using Material.Icons;

namespace LLMDesktopAssistant.MVVM.Debug;

/// <summary>
/// A fully interactive <see cref="IAddonCardBlockChange"/> used by the addon cards debug page.
/// It holds a mutable value, reports itself as changed while the value differs from the definition
/// value (so the card draws the accent marker on the left) and resets itself back to that value.
/// </summary>
public class DemoAddonBlockChange : ViewModelBase, IAddonCardBlockChange
{
	private readonly bool _definitionValue;

	private bool _value;

	/// <summary>
	/// Initializes a new instance of the <see cref="DemoAddonBlockChange"/> class.
	/// </summary>
	/// <param name="definitionValue">The value taken from the addon definition.</param>
	/// <param name="order">The order of the element inside its group.</param>
	public DemoAddonBlockChange(bool definitionValue, int order = 0)
	{
		_definitionValue = definitionValue;
		_value = definitionValue;
		Order = order;
	}

	/// <inheritdoc/>
	public int Order { get; }

	/// <inheritdoc/>
	public LocaleKeyBase? Title { get; set; }

	/// <summary>
	/// Where and when the block is rendered. Mirrors <see cref="AddonCardBlock.Visibility"/>: assigning
	/// <see cref="AddonCardBlockVisibility.Collapsible"/> collapses the block (<see cref="IsExpanded"/>
	/// is set to <see langword="false"/>).
	/// </summary>
	public AddonCardBlockVisibility Visibility
	{
		get;
		set
		{
			field = value;

			if (value == AddonCardBlockVisibility.Collapsible)
				IsExpanded = false;
		}
	}

	/// <inheritdoc/>
	public object? Content { get; private set; }

	/// <inheritdoc/>
	public ImmutableList<IAddonCardChip>? Chips { get; set; }

	/// <inheritdoc/>
	public VisualIconKind? ToggleIcon { get; set; }

	/// <inheritdoc/>
	public LocaleKeyBase? ToggleToolTip { get; set; }

	/// <inheritdoc/>
	public bool IsExpanded
	{
		get;
		set => SetProperty(ref field, value);
	} = true;

	/// <summary>
	/// Gets or sets the current (override) value. Setting a value different from the definition
	/// value turns the change into the "changed" state.
	/// </summary>
	public bool Value
	{
		get => _value;
		set
		{
			if (SetProperty(ref _value, value))
				RaisePropertyChanged(nameof(IsChanged));
		}
	}

	/// <inheritdoc/>
	public bool IsChanged => _value != _definitionValue;

	/// <inheritdoc/>
	public ICommand? ResetCommand => field ??= new RelayCommand(() => Value = _definitionValue);

	/// <summary>
	/// Creates a change with a checkbox bound to its value.
	/// </summary>
	/// <param name="definitionValue">The value taken from the addon definition.</param>
	/// <param name="order">The order of the element inside its group.</param>
	/// <param name="visibility">Where and when the block is rendered.</param>
	public static DemoAddonBlockChange Toggle(bool definitionValue, int order = 0,
		AddonCardBlockVisibility visibility = AddonCardBlockVisibility.Inline)
	{
		var change = new DemoAddonBlockChange(definitionValue, order)
		{
			Visibility = visibility
		};

		var box = new CheckBox
		{
			DataContext = change,
			VerticalAlignment = VerticalAlignment.Center
		};
		box.Bind(CheckBox.IsCheckedProperty, new Binding(nameof(Value)) { Mode = BindingMode.TwoWay });

		change.Content = box;
		return change;
	}
}
