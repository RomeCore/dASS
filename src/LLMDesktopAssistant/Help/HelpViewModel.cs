using System.Text.RegularExpressions;
using Avalonia.Controls;
using LiveMarkdown.Avalonia;
using LLMDesktopAssistant.Localization;
using LLMDesktopAssistant.MVVM;
using LLMDesktopAssistant.Settings.Application;

namespace LLMDesktopAssistant.Help;

/// <summary>
/// View model for the built-in help viewer: exposes the documentation tree and the
/// markdown content of the currently selected document. Reacts to language changes.
/// </summary>
[ViewModelFor(typeof(HelpView))]
public partial class HelpViewModel : ViewModelBase
{
	private readonly HelpDocumentStore _store;
	private string _locale = string.Empty;

	/// <summary>
	/// Gets the callback that handles link clicks in the help viewer. Returns <see langword="true"/>
	/// when the link was handled (navigated to another help document), or <see langword="false"/>
	/// when the link should be opened externally.
	/// </summary>
	public Func<Uri, bool> LinkClicked { get; }

	/// <summary>
	/// Gets the child nodes of the documentation root, used as the tree items source.
	/// </summary>
	public IReadOnlyList<HelpDocumentNode> RootNodes => _store.Root.Children;

	private HelpDocumentNode? _selectedNode;
	/// <summary>
	/// Gets or sets the currently selected node. Selecting a document updates <see cref="MarkdownText"/>.
	/// </summary>
	public HelpDocumentNode? SelectedNode
	{
		get => _selectedNode;
		set
		{
			if (SetProperty(ref _selectedNode, value))
				UpdateMarkdown();
		}
	}

	private string _markdownText = string.Empty;
	/// <summary>
	/// Gets the markdown content of the selected document, or a placeholder when a category is selected.
	/// </summary>
	public string MarkdownText
	{
		get => _markdownText;
		private set => SetProperty(ref _markdownText, value);
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="HelpViewModel"/> class.
	/// </summary>
	/// <param name="store">The help document store.</param>
	public HelpViewModel(HelpDocumentStore store)
	{
		_store = store;
		_locale = GetCurrentLocale();
		_store.UpdateTitles(_locale);
		LocalizationManager.StaticLanguageChanged += OnLanguageChanged;

		LinkClicked = HandleLinkClicked;
	}

	private bool HandleLinkClicked(Uri uri)
	{
		if (uri.IsAbsoluteUri)
			return false;

		var relativePath = uri.OriginalString;
		var fragmentIndex = relativePath.IndexOf('#');
		if (fragmentIndex >= 0)
			relativePath = relativePath[..fragmentIndex];

		if (relativePath.EndsWith(".md", StringComparison.OrdinalIgnoreCase))
			relativePath = relativePath[..^3];

		var currentPath = _selectedNode?.NodePath ?? string.Empty;
		var resolvedPath = ResolveRelativePath(currentPath, relativePath.Trim('/'));

		if (FindNode(RootNodes, resolvedPath) is { HasDocument: true } node)
		{
			SelectedNode = node;
			return true;
		}

		return false;
	}

	private static string ResolveRelativePath(string currentPath, string relativePath)
	{
		var directory = currentPath.Contains('/') ? currentPath[..currentPath.LastIndexOf('/')] : string.Empty;
		var combined = string.IsNullOrEmpty(directory) ? relativePath : directory + "/" + relativePath;

		var segments = new List<string>();
		foreach (var segment in combined.Split('/'))
		{
			if (segment.Length == 0 || segment == ".")
				continue;
			if (segment == "..")
			{
				if (segments.Count > 0)
					segments.RemoveAt(segments.Count - 1);
				continue;
			}
			segments.Add(segment);
		}

		return string.Join("/", segments);
	}

	private static HelpDocumentNode? FindNode(IEnumerable<HelpDocumentNode> nodes, string path)
	{
		foreach (var node in nodes)
		{
			if (string.Equals(node.NodePath, path, StringComparison.OrdinalIgnoreCase))
				return node;
			if (FindNode(node.Children, path) is { } found)
				return found;
		}

		return null;
	}

	private void OnLanguageChanged(object? sender, string language)
	{
		_locale = GetCurrentLocale();
		_store.UpdateTitles(_locale);
		UpdateMarkdown();
	}

	private void UpdateMarkdown()
	{
		if (_selectedNode?.DocumentPath is { } path)
		{
			MarkdownText = ReplaceImageLinks(_store.GetContent(path, _locale) ?? string.Empty, path);
		}
		else
		{
			MarkdownText = LocalizationManager.LocalizeStatic("help.select_document");
		}
	}

	private static string GetCurrentLocale()
	{
		return ApplicationStartupSettingsAccessor.Current.Language;
	}

	private static string ReplaceImageLinks(string markdown, string documentPath)
	{
		var assemblyName = typeof(HelpViewModel).Assembly.GetName().Name;
		return ImageLinkRegex().Replace(markdown, match =>
		{
			var resolvedPath = ResolveRelativePath(documentPath, match.Groups[2].Value);
			return $"{match.Groups[1].Value}avares://{assemblyName}/Assets/help/{resolvedPath}";
		});
	}

	[GeneratedRegex(@"(!\[[^\]]*\]\()(?!\w+://)([^)\s]+\.(?:png|jpe?g|gif|svg|webp))", RegexOptions.IgnoreCase)]
	private static partial Regex ImageLinkRegex();
}
