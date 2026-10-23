using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Input;
using CommunityToolkit.Mvvm.Input;
using LiveMarkdown.Avalonia;
using LLMDesktopAssistant.Controls.Icons;
using LLMDesktopAssistant.Controls.Markdown.CodeBlocks;
using LLMDesktopAssistant.Localization;

namespace LLMDesktopAssistant.Markdown.Controls
{
	/// <summary>
	/// The live state of one code block: the tab strip, which tab is active, whether the preview sits next to the
	/// source, and the action buttons with their panels.
	/// </summary>
	/// <remarks>
	/// The state belongs to <see cref="Controls.Markdown.PreviewCodeBlock"/> and outlives streaming: previews are
	/// created once, actions are created once, and every later content change is pushed into the previews instead of
	/// re-creating them. The two dimensions of the block are independent: <see cref="ActiveTab"/> is switched by the
	/// tab chips, while <see cref="IsSplit"/> is switched by the split button (or by the source chip, which turns the
	/// split off).
	/// </remarks>
	public sealed class ExtendedCodeBlockViewModel : NotifyPropertyChanged
	{
		private readonly ExtendedCodeBlock _block;
		private readonly bool _isCompact;
		private readonly Func<MarkdownCodeBlockContext> _contextFactory;
		private readonly Func<bool> _isFinalFactory;
		private readonly Func<IReadOnlyList<ICodeBlockPreviewProvider>?> _previewProviders;
		private readonly Func<IReadOnlyList<IMarkdownCodeBlockActionProvider>?> _actionProviders;

		private readonly ObservableCollection<CodeBlockTabItem> _tabs = [];
		private readonly ObservableCollection<CodeBlockActionItem> _actions = [];
		private readonly List<ICodeBlockPreview> _previews = [];

		private CancellationTokenSource? _updateToken;

		private bool _isCreated;
		private bool _isSplit;
		private CodeBlockTabItem? _activeTab;

		/// <summary>
		/// Creates the state of one block.
		/// </summary>
		/// <param name="block">The code block the state reads the language and the text from.</param>
		/// <param name="isCompact">Whether the block is rendered without the top bar at all.</param>
		/// <param name="previewProviders">
		/// Reads the preview providers of the surface at the moment the block needs them; the list is not known
		/// when the block is created, because the consumer binds it onto the renderer afterwards.
		/// </param>
		/// <param name="actionProviders">Reads the action providers of the surface, for the same reason.</param>
		/// <param name="contextFactory">Builds a fresh context at the moment an extension needs one.</param>
		/// <param name="isFinalFactory">Tells whether the block is final (the stream has stopped).</param>
		public ExtendedCodeBlockViewModel(
			ExtendedCodeBlock block,
			bool isCompact,
			Func<IReadOnlyList<ICodeBlockPreviewProvider>?> previewProviders,
			Func<IReadOnlyList<IMarkdownCodeBlockActionProvider>?> actionProviders,
			Func<MarkdownCodeBlockContext> contextFactory,
			Func<bool> isFinalFactory)
		{
			_block = block;
			_isCompact = isCompact;
			_previewProviders = previewProviders;
			_actionProviders = actionProviders;
			_contextFactory = contextFactory;
			_isFinalFactory = isFinalFactory;

			SourceTab = new CodeBlockTabItem(Locale.GetKey("common.code"), () => SelectTab(null));
			_tabs.Add(SourceTab);
		}

		/// <summary>
		/// Gets the chip that shows the source alone.
		/// </summary>
		public CodeBlockTabItem SourceTab { get; }

		/// <summary>
		/// Gets every chip of the block, the source chip first.
		/// </summary>
		public IReadOnlyList<CodeBlockTabItem> Tabs => _tabs;

		/// <summary>
		/// Gets a value indicating whether the chip strip is worth showing: a single "source" chip over a block that
		/// has nothing to switch to is noise.
		/// </summary>
		public bool AreTabsVisible => _previews.Count > 0;

		/// <summary>
		/// Gets the action buttons of the block.
		/// </summary>
		public IReadOnlyList<CodeBlockActionItem> Actions => _actions;

		/// <summary>
		/// Gets a value indicating whether the whole top bar is shown.
		/// </summary>
		public bool IsBarVisible => !_isCompact;

		/// <summary>
		/// Gets a value indicating whether the split button can be offered at all.
		/// </summary>
		public bool CanSplit => _previews.Count > 0;

		/// <summary>
		/// Gets or sets a value indicating whether the source and the active preview are shown side by side.
		/// </summary>
		public bool IsSplit
		{
			get => _isSplit;
			set
			{
				if (SetProperty(ref _isSplit, value))
					RaiseViewStateChanged();
			}
		}

		/// <summary>
		/// Gets or sets the active preview chip; <see langword="null"/> means the source.
		/// </summary>
		public CodeBlockTabItem? ActiveTab
		{
			get => _activeTab;
			private set
			{
				if (!SetProperty(ref _activeTab, value)) return;
				RaisePropertyChanged(nameof(PreviewContent));
				RaiseViewStateChanged();
			}
		}

		/// <summary>
		/// Gets the content of the active preview, or <see langword="null"/> when the source is active.
		/// </summary>
		public object? PreviewContent => _activeTab?.Preview?.Content;

		/// <summary>
		/// Gets a value indicating whether the source pane is shown.
		/// </summary>
		public bool IsSourceVisible => !HasPreview || _isSplit || _activeTab is null;

		/// <summary>
		/// Gets a value indicating whether the preview pane is shown.
		/// </summary>
		public bool IsPreviewVisible => HasPreview && (_isSplit || _activeTab is not null);

		/// <summary>
		/// Gets a value indicating whether the splitter between the panes is shown.
		/// </summary>
		public bool IsSplitterVisible => HasPreview && _isSplit;

		private bool HasPreview => _previews.Count > 0;

		/// <summary>
		/// Creates the previews and the actions of the block, once.
		/// </summary>
		/// <remarks>
		/// Called when the opening fence line is complete (or the stream has stopped), because before that the
		/// language of the block can still grow and a provider would be asked about the wrong one.
		/// </remarks>
		public void EnsureCreated()
		{
			if (_isCreated) return;

			// The node is created while the document is parsed, long before the consumer's binding puts the provider
			// lists onto the renderer. A null list is "not bound yet", not "no providers", so the block has to wait
			// for a later update instead of remembering an empty set for good.
			var resolvedPreviewProviders = _previewProviders();
			var resolvedActionProviders = _actionProviders();
			if (resolvedPreviewProviders is null || resolvedActionProviders is null) return;

			_isCreated = true;

			var context = _contextFactory();
			var previewProviders = resolvedPreviewProviders;
			var actionProviders = resolvedActionProviders;

			foreach (var provider in previewProviders.OrderBy(provider => provider.Order))
			{
				var preview = provider.TryCreatePreview(context);
				if (preview is null) continue;

				_previews.Add(preview);

				// The closure has to name the tab itself, not the index it happens to sit at.
				CodeBlockTabItem tab = null!;
				tab = new CodeBlockTabItem(preview.Title, () => SelectTab(tab), preview);
				_tabs.Add(tab);
			}

			// Providers are already ordered, so the first chip that wants to be the default one wins.
			var defaultPreview = _tabs.FirstOrDefault(tab => tab.Preview?.IsDefault is true);
			if (defaultPreview?.Preview is { } chosen)
			{
				_activeTab = defaultPreview;
				_isSplit = chosen.IsShownSplitByDefault;
				defaultPreview.IsSelected = true;
			}
			else
			{
				SourceTab.IsSelected = true;
			}

			foreach (var provider in actionProviders)
			{
				foreach (var action in provider.GetActions(context))
					_actions.Add(new CodeBlockActionItem(action, _contextFactory));
			}

			RaisePropertyChanged(nameof(AreTabsVisible));
			RaisePropertyChanged(nameof(IsSplit));
			RaisePropertyChanged(nameof(CanSplit));
			RaisePropertyChanged(nameof(ActiveTab));
			RaisePropertyChanged(nameof(PreviewContent));
			RaiseViewStateChanged();
		}

		/// <summary>
		/// Pushes the current content of the block into every preview.
		/// </summary>
		public void Update(bool isFinal)
		{
			if (!_isCreated) return;

			_updateToken?.Cancel();
			_updateToken?.Dispose();
			_updateToken = new CancellationTokenSource();
			var token = _updateToken.Token;

			var content = _contextFactory().Content;
			var final = isFinal || _isFinalFactory();

			foreach (var preview in _previews)
			{
				try
				{
					_ = preview.UpdateAsync(content, final, token);
				}
				catch (Exception)
				{
					// A broken preview must never take the message down with it.
				}
			}
		}

		private void SelectTab(CodeBlockTabItem? tab)
		{
			// Choosing the source chip drops the split: while split is on, the source is always visible.
			if (tab is null) IsSplit = false;

			var selected = tab ?? SourceTab;
			foreach (var item in _tabs)
				item.IsSelected = ReferenceEquals(item, selected);

			ActiveTab = tab;
		}

		private void RaiseViewStateChanged()
		{
			RaisePropertyChanged(nameof(IsSourceVisible));
			RaisePropertyChanged(nameof(IsPreviewVisible));
			RaisePropertyChanged(nameof(IsSplitterVisible));
		}

		/// <inheritdoc />
		protected override void Dispose(bool disposing)
		{
			base.Dispose(disposing);
			if (!disposing) return;

			_updateToken?.Cancel();
			_updateToken?.Dispose();
			_updateToken = null;

			foreach (var preview in _previews) preview.Dispose();
			_previews.Clear();

			foreach (var action in _actions) action.Dispose();
			_actions.Clear();
		}
	}

	/// <summary>
	/// One chip of the code block's tab strip.
	/// </summary>
	public sealed class CodeBlockTabItem : NotifyPropertyChanged
	{
		private bool _isSelected;

		internal CodeBlockTabItem(LocaleKeyBase title, Action select, ICodeBlockPreview? preview = null)
		{
			Title = title;
			Preview = preview;
			SelectCommand = new RelayCommand(select);
		}

		/// <summary>
		/// Gets the localized chip title.
		/// </summary>
		public LocaleKeyBase Title { get; }

		/// <summary>
		/// Gets the preview behind the chip; <see langword="null"/> for the source chip.
		/// </summary>
		public ICodeBlockPreview? Preview { get; }

		/// <summary>
		/// Gets the command that makes this chip active.
		/// </summary>
		public ICommand SelectCommand { get; }

		/// <summary>
		/// Gets or sets a value indicating whether this chip is the active one.
		/// </summary>
		public bool IsSelected
		{
			get => _isSelected;
			internal set => SetProperty(ref _isSelected, value);
		}
	}

	/// <summary>
	/// The button of one code block action: the descriptor stays the author's, the command belongs to the block.
	/// </summary>
	public sealed class CodeBlockActionItem : NotifyPropertyChanged
	{
		private readonly Func<MarkdownCodeBlockContext> _contextFactory;

		internal CodeBlockActionItem(IMarkdownCodeBlockAction action, Func<MarkdownCodeBlockContext> contextFactory)
		{
			Action = action;
			_contextFactory = contextFactory;
			Command = new AsyncRelayCommand(ExecuteAsync);

			action.PropertyChanged += OnActionPropertyChanged;
		}

		/// <summary>
		/// Gets the action behind the button.
		/// </summary>
		public IMarkdownCodeBlockAction Action { get; }

		/// <summary>
		/// Gets the localized tooltip/title.
		/// </summary>
		public LocaleKeyBase Title => Action.Title;

		/// <summary>
		/// Gets the icon of the button.
		/// </summary>
		public VisualIconKind Icon => Action.Icon;

		/// <summary>
		/// Gets a value indicating whether the button is shown.
		/// </summary>
		public bool IsVisible => Action.IsVisible;

		/// <summary>
		/// Gets the panel the action appends under the block.
		/// </summary>
		public object? AdditionalContent => Action.AdditionalContent;

		/// <summary>
		/// Gets a value indicating whether the action contributes a panel at all.
		/// </summary>
		public bool HasAdditionalContent => Action.AdditionalContent is not null;

		/// <summary>
		/// Gets the command that runs the action with a context built at click time.
		/// </summary>
		public ICommand Command { get; }

		private Task ExecuteAsync() => Action.ExecuteAsync(_contextFactory(), CancellationToken.None);

		private void OnActionPropertyChanged(object? sender, PropertyChangedEventArgs e)
		{
			switch (e.PropertyName)
			{
				case nameof(IMarkdownCodeBlockAction.Icon):
					RaisePropertyChanged(nameof(Icon));
					break;
				case nameof(IMarkdownCodeBlockAction.IsVisible):
					RaisePropertyChanged(nameof(IsVisible));
					break;
				case nameof(IMarkdownCodeBlockAction.AdditionalContent):
					RaisePropertyChanged(nameof(AdditionalContent));
					RaisePropertyChanged(nameof(HasAdditionalContent));
					break;
			}
		}

		/// <inheritdoc />
		protected override void Dispose(bool disposing)
		{
			base.Dispose(disposing);
			if (!disposing) return;

			Action.PropertyChanged -= OnActionPropertyChanged;
			Action.Dispose();
		}
	}
}
