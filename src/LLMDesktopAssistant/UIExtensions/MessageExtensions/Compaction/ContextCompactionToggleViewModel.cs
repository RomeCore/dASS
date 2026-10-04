using System.Windows.Input;
using Avalonia;
using LLMDesktopAssistant.Prompting;
using Material.Icons;

namespace LLMDesktopAssistant.UIExtensions.MessageExtensions.Compaction
{
	/// <summary>
	/// A single toggle segment of the context compaction panel.
	/// </summary>
	public class ContextCompactionToggleViewModel : NotifyPropertyChanged
	{
		/// <summary>
		/// Gets the context checkpoint kind this segment toggles.
		/// </summary>
		public ContextCheckpointKind Kind { get; }

		/// <summary>
		/// Gets the icon of the segment.
		/// </summary>
		public MaterialIconKind Icon { get; }

		/// <summary>
		/// Gets or sets the localization key of the segment tooltip.
		/// May change while a summary is being generated.
		/// </summary>
		public string? Tooltip
		{
			get => field;
			set => SetProperty(ref field, value);
		}

		/// <summary>
		/// Gets or sets the checked state of the segment;
		/// <c>null</c> means the indeterminate (busy) state.
		/// </summary>
		public bool? IsChecked
		{
			get => field;
			set => SetProperty(ref field, value);
		}

		/// <summary>
		/// Gets or sets a value indicating whether the segment can be interacted with.
		/// </summary>
		public bool IsEnabled
		{
			get => field;
			set => SetProperty(ref field, value);
		} = true;

		/// <summary>
		/// Gets the corner radius that makes the segments look connected.
		/// </summary>
		public CornerRadius CornerRadius { get; }

		/// <summary>
		/// Gets the command executed when the segment is clicked.
		/// </summary>
		public ICommand Command { get; }

		public ContextCompactionToggleViewModel(
			ContextCheckpointKind kind,
			MaterialIconKind icon,
			string tooltip,
			CornerRadius cornerRadius,
			ICommand command)
		{
			Kind = kind;
			Icon = icon;
			Tooltip = tooltip;
			CornerRadius = cornerRadius;
			Command = command;
		}
	}
}
