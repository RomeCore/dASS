using LLMDesktopAssistant.Localization;
using LLMDesktopAssistant.Utils;
using Material.Icons;

namespace LLMDesktopAssistant.Settings.Application.MVVM
{
	/// <summary>
	/// ViewModel for the application settings page. Builds a tree of settings categories whose
	/// view models are created lazily when the category is opened and disposed when the
	/// selection changes.
	/// </summary>
	[ViewModelFor(typeof(ApplicationSettingsView))]
	public class ApplicationSettingsViewModel : ViewModelBase
	{
		/// <summary>
		/// Gets the tree of settings categories.
		/// </summary>
		public RangeObservableCollection<SettingsTreeNode> SettingsTree { get; } = [];

		private SettingsTreeNode? _selectedNode;
		/// <summary>
		/// Gets or sets the currently selected settings tree node. Selecting a node creates
		/// its view model; selecting another node releases (disposes) the previous one.
		/// </summary>
		public SettingsTreeNode? SelectedNode
		{
			get => _selectedNode;
			set
			{
				if (ReferenceEquals(_selectedNode, value))
					return;

				_selectedNode?.ReleaseViewModel();

				SetProperty(ref _selectedNode, value);
			}
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="ApplicationSettingsViewModel"/> class.
		/// </summary>
		public ApplicationSettingsViewModel()
		{
			InitializeTree();
			SelectedNode = SettingsTree[0];
		}

		private void InitializeTree()
		{
			SettingsTree.Add(
				new SettingsLeafNode(LocalizationManager.LocalizeStatic("appsettings.appearance.title"),
				MaterialIconKind.Palette,
				() => new ApplicationAppearanceSettingsViewModel()));

			SettingsTree.Add(
				new SettingsLeafNode(LocalizationManager.LocalizeStatic("appsettings.language.title"),
				MaterialIconKind.Translate,
				() => new ApplicationLanguageSettingsViewModel()));

			SettingsTree.Add(
				new SettingsLeafNode(LocalizationManager.LocalizeStatic("appsettings.web.fetch.title"),
				MaterialIconKind.Web,
				() => new WebFetchSettingsViewModel()));
		}

		/// <inheritdoc/>
		protected override void Dispose(bool disposing)
		{
			base.Dispose(disposing);

			if (disposing)
			{
				foreach (var node in SettingsTree)
					node.Dispose();
			}
		}
	}
}
