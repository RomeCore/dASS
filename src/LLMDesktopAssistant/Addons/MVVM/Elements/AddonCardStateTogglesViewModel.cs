using System.ComponentModel;
using Avalonia.Media;
using LLMDesktopAssistant.Localization;
using Material.Icons;

namespace LLMDesktopAssistant.Addons.MVVM.Elements
{
	/// <summary>
	/// The kind of a state toggle: a switch that edits the enabled state, or one that switches
	/// the addon between the shown and the hidden state.
	/// </summary>
	public enum AddonCardStateToggleKind
	{
		/// <summary>The toggle edits the enabled state of the addon.</summary>
		Enabled,

		/// <summary>The toggle switches the addon between the shown and the hidden state.</summary>
		Shown
	}

	/// <summary>
	/// The view model of a single state toggle of an addon card. The value lives in the element
	/// (the aggregate of a group card decides what a click means), the view model only adapts
	/// it for the bindings: the state, the icon, the brush and the editability.
	/// </summary>
	[ViewModelFor(typeof(AddonCardStateToggleView))]
	public sealed class AddonCardStateToggleViewModel : NotifyPropertyChanged
	{
		private static readonly IBrush _positiveBrush = Brushes.LightGreen;
		private static readonly IBrush _negativeBrush = Brushes.OrangeRed;
		private static readonly IBrush _mixedBrush = Brushes.Gray;

		private readonly AddonCardStateToggleKind _kind;
		private readonly Func<bool?> _get;
		private readonly Action<bool?> _set;
		private readonly Func<bool> _canEdit;

		/// <summary>
		/// Initializes a new instance of the <see cref="AddonCardStateToggleViewModel"/> class.
		/// </summary>
		/// <param name="kind">What the toggle edits: the enabled or the shown state.</param>
		/// <param name="toolTip">The localized hint shown when the toggle is hovered.</param>
		/// <param name="isThreeState">Whether the toggle can show the mixed state.</param>
		/// <param name="get">Reads the effective state of the toggle.</param>
		/// <param name="set">Writes the state requested by the user. The owner is free to normalize the value.</param>
		/// <param name="canEdit">Whether the toggle is editable.</param>
		public AddonCardStateToggleViewModel(AddonCardStateToggleKind kind, LocaleKeyBase toolTip, bool isThreeState,
			Func<bool?> get, Action<bool?> set, Func<bool> canEdit)
		{
			_kind = kind;
			ToolTip = toolTip;
			IsThreeState = isThreeState;
			_get = get;
			_set = set;
			_canEdit = canEdit;
		}

		/// <summary>
		/// Gets the localized hint of the toggle.
		/// </summary>
		public LocaleKeyBase ToolTip { get; }

		/// <summary>
		/// Gets a value indicating whether the toggle shows the mixed (indeterminate) state.
		/// </summary>
		public bool IsThreeState { get; }

		/// <summary>
		/// Gets or sets the current state of the toggle.
		/// </summary>
		public bool? IsOn
		{
			get => _get();
			set
			{
				if (_get() == value)
					return;

				_set(value);

				// The owner may normalize the value (the enabled toggle of a group, for example,
				// decides applied value from the aggregate of its children).
				Refresh();
			}
		}

		/// <summary>
		/// Gets a value indicating whether the toggle is editable.
		/// </summary>
		public bool CanEdit => _canEdit();

		/// <summary>
		/// Gets the icon that represents the current state.
		/// </summary>
		public VisualIconKind Icon => _kind == AddonCardStateToggleKind.Enabled
			? IsOn switch
			{
				true => MaterialIconKind.Check,
				false => MaterialIconKind.Close,
				_ => MaterialIconKind.MinusCircle
			}
			: IsOn switch
			{
				true => MaterialIconKind.Eye,
				false => MaterialIconKind.EyeOff,
				_ => MaterialIconKind.MinusCircle
			};

		/// <summary>
		/// Gets the brush that colors the icon for the current state.
		/// </summary>
		public IBrush Brush => IsOn switch
		{
			true => _positiveBrush,
			false => _negativeBrush,
			_ => _mixedBrush
		};

		/// <summary>
		/// Re-reads the state from the owner and notifies the view.
		/// </summary>
		public void Refresh()
		{
			RaisePropertyChanged(nameof(IsOn));
			RaisePropertyChanged(nameof(CanEdit));
			RaisePropertyChanged(nameof(Icon));
			RaisePropertyChanged(nameof(Brush));
		}
	}

	/// <summary>
	/// The view model of the pair of state toggles shown in the header of a card: the enabled toggle
	/// and the shown/hidden toggle. It mirrors every change of the owner element into both toggles.
	/// </summary>
	[ViewModelFor(typeof(AddonCardStateTogglesView))]
	public sealed class AddonCardStateTogglesViewModel : NotifyPropertyChanged
	{
		private readonly INotifyPropertyChanged _source;

		/// <summary>
		/// Initializes a new instance of the <see cref="AddonCardStateTogglesViewModel"/> class.
		/// </summary>
		/// <param name="source">The element that owns the toggles.</param>
		/// <param name="enabled">The toggle that edits the enabled state.</param>
		/// <param name="shown">The toggle that switches the addon between shown and hidden.</param>
		public AddonCardStateTogglesViewModel(INotifyPropertyChanged source,
			AddonCardStateToggleViewModel enabled, AddonCardStateToggleViewModel shown)
		{
			_source = source;
			Enabled = enabled;
			Shown = shown;

			_source.PropertyChanged += Source_PropertyChanged;
		}

		/// <summary>
		/// Gets the toggle that edits the enabled state.
		/// </summary>
		public AddonCardStateToggleViewModel Enabled { get; }

		/// <summary>
		/// Gets the toggle that switches the addon between shown and hidden.
		/// </summary>
		public AddonCardStateToggleViewModel Shown { get; }

		/// <inheritdoc/>
		protected override void Dispose(bool disposing)
		{
			base.Dispose(disposing);

			if (disposing)
			{
				_source.PropertyChanged -= Source_PropertyChanged;
				Enabled.Dispose();
				Shown.Dispose();
			}
		}

		private void Source_PropertyChanged(object? sender, PropertyChangedEventArgs e)
		{
			Enabled.Refresh();
			Shown.Refresh();
		}
	}
}
