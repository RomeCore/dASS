using Material.Icons;

namespace LLMDesktopAssistant.Agents.Memory.MVVM
{
	/// <summary>
	/// A section of the memory block edit dialog shown in the sections tree.
	/// The section view model is created lazily on the first access.
	/// </summary>
	public class EditMemoryBlockSection
	{
		private readonly Func<object?> _viewModelFactory;
		private object? _viewModel;

		/// <summary>
		/// Gets the display name of the section.
		/// </summary>
		public string DisplayName { get; }

		/// <summary>
		/// Gets the icon shown next to the section.
		/// </summary>
		public VisualIconKind Icon { get; }

		/// <summary>
		/// Gets the view model of the section, creating it lazily on first access.
		/// </summary>
		public object? ViewModel => _viewModel ??= _viewModelFactory();

		/// <summary>
		/// Initializes a new instance of the <see cref="EditMemoryBlockSection"/> class.
		/// </summary>
		/// <param name="displayName">The display name of the section.</param>
		/// <param name="icon">The icon shown next to the section.</param>
		/// <param name="viewModelFactory">The factory that creates the view model of the section.</param>
		public EditMemoryBlockSection(string displayName, VisualIconKind icon, Func<object?> viewModelFactory)
		{
			DisplayName = displayName;
			Icon = icon;
			_viewModelFactory = viewModelFactory;
		}
	}
}
