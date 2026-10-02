using LLMDesktopAssistant.Localization;
using LLMDesktopAssistant.Utils;
using Material.Icons;

namespace LLMDesktopAssistant.MVVM.Debug;

/// <summary>
/// View model for the debug pages section. Builds a tree of debug pages whose view
/// models are created lazily when the page is opened and disposed when the selection
/// changes.
/// </summary>
[ViewModelFor(typeof(DebugPagesView))]
public class DebugPagesViewModel : ViewModelBase
{
	/// <summary>
	/// Gets the tree of debug pages.
	/// </summary>
	public RangeObservableCollection<DebugTreeNode> DebugTree { get; } = [];

	private DebugTreeNode? _selectedNode;

	/// <summary>
	/// Gets or sets the currently selected debug tree node. Selecting a node creates
	/// its page view model; selecting another node releases (disposes) the previous one.
	/// </summary>
	public DebugTreeNode? SelectedNode
	{
		get => _selectedNode;
		set
		{
			if (ReferenceEquals(_selectedNode, value))
				return;

			_selectedNode?.ReleasePage();

			SetProperty(ref _selectedNode, value);
		}
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="DebugPagesViewModel"/> class.
	/// </summary>
	public DebugPagesViewModel()
	{
		InitializeTree();
		SelectedNode = DebugTree[0];
	}

	private void InitializeTree()
	{
		DebugTree.Add(
			new DebugLeafNode(LocalizationManager.LocalizeStatic("debug.llt_editor.title"),
				MaterialIconKind.FileCode,
				() => new LLTEditorDebugPageViewModel()));

		DebugTree.Add(
			new DebugLeafNode(LocalizationManager.LocalizeStatic("debug.addon_cards.title"),
				MaterialIconKind.Cards,
				() => new AddonCardsDebugPageViewModel()));

		DebugTree.Add(
			new DebugLeafNode(LocalizationManager.LocalizeStatic("debug.parameterization.title"),
				MaterialIconKind.Tune,
				() => new ParameterizationDebugPageViewModel()));

		DebugTree.Add(
			new DebugLeafNode(LocalizationManager.LocalizeStatic("debug.highlight_textbox.title"),
				MaterialIconKind.FormatColorText,
				() => new HighlightTextBoxDebugPageViewModel()));

		DebugTree.Add(
			new DebugLeafNode(LocalizationManager.LocalizeStatic("debug.dynamic_viewmodel.title"),
				MaterialIconKind.FileCode,
				() => new DynamicViewModelDebugPageViewModel()));
	}

	/// <inheritdoc/>
	protected override void Dispose(bool disposing)
	{
		base.Dispose(disposing);

		if (disposing)
		{
			foreach (var node in DebugTree)
				node.Dispose();
		}
	}
}
