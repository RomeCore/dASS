using Material.Icons;

namespace LLMDesktopAssistant.MVVM.Debug;

/// <summary>
/// A node of the debug pages tree. The page view model is created lazily when the
/// node is selected and released when the selection changes, so that debug pages
/// do not outlive their usage.
/// </summary>
public abstract class DebugTreeNode : ViewModelBase
{
	private readonly Func<object?> _pageFactory;
	private object? _page;

	/// <summary>
	/// Initializes a new instance of the <see cref="DebugTreeNode"/> class.
	/// </summary>
	/// <param name="pageFactory">The factory that creates the page view model shown when this node is selected.</param>
	protected DebugTreeNode(Func<object?> pageFactory)
	{
		_pageFactory = pageFactory;
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
	public abstract IEnumerable<DebugTreeNode>? Children { get; }

	/// <summary>
	/// Gets the page view model shown when this node is selected, creating it lazily on first access.
	/// </summary>
	public object? Page => _page ??= _pageFactory();

	/// <summary>
	/// Disposes the created page view model (if any) and resets the cache so that the
	/// next access recreates it.
	/// </summary>
	/// <returns>The released page view model, or <see langword="null"/> if none was created.</returns>
	public object? ReleasePage()
	{
		var page = _page;
		_page = null;
		(page as IDisposable)?.Dispose();
		return page;
	}

	/// <inheritdoc/>
	protected override void Dispose(bool disposing)
	{
		base.Dispose(disposing);

		if (disposing)
		{
			ReleasePage();
			if (Children is not null)
				foreach (var child in Children)
					child.Dispose();
		}
	}
}

/// <summary>
/// A debug tree node that groups child nodes together.
/// </summary>
public class DebugParentNode : DebugTreeNode
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
	public override IEnumerable<DebugTreeNode> Children { get; }

	/// <summary>
	/// Initializes a new instance of the <see cref="DebugParentNode"/> class.
	/// </summary>
	/// <param name="name">The display name of the node.</param>
	/// <param name="icon">The icon shown next to the node.</param>
	/// <param name="children">The child nodes.</param>
	/// <param name="pageFactory">The factory that creates the page view model shown when this node is selected.</param>
	public DebugParentNode(string name, VisualIconKind icon,
		List<DebugTreeNode> children, Func<object?> pageFactory)
		: base(pageFactory)
	{
		DisplayName = name;
		Icon = icon;
		Children = children;
	}
}

/// <summary>
/// A leaf debug tree node that shows a single debug page.
/// </summary>
public class DebugLeafNode : DebugTreeNode
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
	public override IEnumerable<DebugTreeNode>? Children => null;

	/// <summary>
	/// Initializes a new instance of the <see cref="DebugLeafNode"/> class.
	/// </summary>
	/// <param name="name">The display name of the node.</param>
	/// <param name="icon">The icon shown next to the node.</param>
	/// <param name="pageFactory">The factory that creates the page view model shown when this node is selected.</param>
	public DebugLeafNode(string name, VisualIconKind icon, Func<object?> pageFactory)
		: base(pageFactory)
	{
		DisplayName = name;
		Icon = icon;
	}
}
