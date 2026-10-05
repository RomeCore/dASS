using Material.Icons;

namespace LLMDesktopAssistant.Settings
{
	/// <summary>
	/// A node of the settings tree. The node view model is created lazily when the
	/// category is opened (selected) and released when the selection changes or the
	/// dialog is closed, so that category view models do not outlive their usage.
	/// </summary>
	public abstract class SettingsTreeNode : ViewModelBase
	{
		private readonly Func<object?> _viewModelFactory;
		private object? _viewModel;

		/// <summary>
		/// Initializes a new instance of the <see cref="SettingsTreeNode"/> class.
		/// </summary>
		/// <param name="viewModelFactory">The factory that creates the view model shown when this node is selected.</param>
		protected SettingsTreeNode(Func<object?> viewModelFactory)
		{
			_viewModelFactory = viewModelFactory;
		}

		/// <summary>
		/// Gets the display name of the node.
		/// </summary>
		public abstract string DisplayName { get; }

		/// <summary>
		/// Gets the icon shown next to the node.
		/// </summary>
		public abstract VisualIconKind Icon { get; }

		/// <summary>
		/// Gets the child nodes, or <see langword="null"/> for leaf nodes.
		/// </summary>
		public abstract IEnumerable<SettingsTreeNode>? Children { get; }

		private bool _isExpanded;
		/// <summary>
		/// Gets or sets a value indicating whether the node is expanded in the settings tree.
		/// Used for parent nodes to control the expanded state of their <see cref="TreeViewItem"/>.
		/// </summary>
		public bool IsExpanded
		{
			get => _isExpanded;
			set => SetProperty(ref _isExpanded, value);
		}

		/// <summary>
		/// Gets the view model shown when this node is selected, creating it lazily on first access.
		/// </summary>
		public object? ViewModel => _viewModel ??= _viewModelFactory();

		/// <summary>
		/// Disposes the created view model (if any) and resets the cache so that the next
		/// access recreates it.
		/// </summary>
		/// <returns>The released view model, or <see langword="null"/> if none was created.</returns>
		public object? ReleaseViewModel()
		{
			var viewModel = _viewModel;
			_viewModel = null;
			(viewModel as IDisposable)?.Dispose();
			return viewModel;
		}

		/// <inheritdoc/>
		protected override void Dispose(bool disposing)
		{
			base.Dispose(disposing);

			if (disposing)
			{
				ReleaseViewModel();
				if (Children is not null)
					foreach (var child in Children)
						child.Dispose();
			}
		}
	}

	/// <summary>
	/// A settings tree node that groups child nodes together.
	/// </summary>
	public class SettingsParentNode : SettingsTreeNode
	{
		/// <summary>
		/// Gets the display name of the node.
		/// </summary>
		public override string DisplayName { get; }

		/// <summary>
		/// Gets the icon shown next to the node.
		/// </summary>
		public override VisualIconKind Icon { get; }

		/// <summary>
		/// Gets the child nodes.
		/// </summary>
		public override IEnumerable<SettingsTreeNode> Children { get; }

		/// <summary>
		/// Initializes a new instance of the <see cref="SettingsParentNode"/> class.
		/// </summary>
		/// <param name="name">The display name of the node.</param>
		/// <param name="icon">The icon shown next to the node.</param>
		/// <param name="children">The child nodes.</param>
		/// <param name="viewModelFactory">The factory that creates the view model shown when this node is selected.</param>
		public SettingsParentNode(string name, VisualIconKind icon,
			List<SettingsTreeNode> children, Func<object?> viewModelFactory)
			: base(viewModelFactory)
		{
			DisplayName = name;
			Icon = icon;
			Children = children;
		}
	}

	/// <summary>
	/// A leaf settings tree node that shows a single settings category.
	/// </summary>
	public class SettingsLeafNode : SettingsTreeNode
	{
		/// <summary>
		/// Gets the display name of the node.
		/// </summary>
		public override string DisplayName { get; }

		/// <summary>
		/// Gets the icon shown next to the node.
		/// </summary>
		public override VisualIconKind Icon { get; }

		/// <summary>
		/// Gets <see langword="null"/> because leaf nodes do not have children.
		/// </summary>
		public override IEnumerable<SettingsTreeNode>? Children => null;

		/// <summary>
		/// Initializes a new instance of the <see cref="SettingsLeafNode"/> class.
		/// </summary>
		/// <param name="name">The display name of the node.</param>
		/// <param name="icon">The icon shown next to the node.</param>
		/// <param name="viewModelFactory">The factory that creates the view model shown when this node is selected.</param>
		public SettingsLeafNode(string name, VisualIconKind icon, Func<object?> viewModelFactory)
			: base(viewModelFactory)
		{
			DisplayName = name;
			Icon = icon;
		}
	}
}
