using System.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LLMDesktopAssistant.LLM.MVVM.Settings.Agents;
using LLMDesktopAssistant.Localization;
using LLMDesktopAssistant.Prompting;
using LLMDesktopAssistant.StructuredValues.Parameterization;
using LLMDesktopAssistant.Utils;
using Material.Icons;

namespace LLMDesktopAssistant.LLM.MVVM.Settings;

/// <summary>
/// ViewModel for a single prompt part card in the agent prompt settings UI.
/// Used both for slot elements (system prompt / persona / specialization — radio selection)
/// and for prompt components (checkbox selection).
/// Shows name, description, category, source, language, diagnostics, parameter UI
/// and template metadata, plus a details section.
/// </summary>
public class PromptPartCardViewModel : ViewModelBase
{
	private readonly AgentPromptSettingsViewModel _parent;
	private bool _isDetailsVisible;
	private bool _isParametersVisible;
	private bool _lastCanShowParameters;
	private PromptPartKeyedSelection<Guid>? _selection;
	private bool _parameterValueSubscribed;

	/// <summary>
	/// Initializes a new instance of the <see cref="PromptPartCardViewModel"/> class.
	/// </summary>
	/// <param name="parent">The parent agent prompt settings ViewModel.</param>
	/// <param name="part">The prompt part to display.</param>
	/// <param name="kind">The slot kind of the element, or <see langword="null"/> for components.</param>
	/// <param name="selection">The agent's selection for this part (used for parameter values), if any.</param>
	/// <param name="isRadio">Whether the selection is single-choice (slot elements) or multi-choice (components).</param>
	public PromptPartCardViewModel(AgentPromptSettingsViewModel parent, PromptPartBase part,
		PromptSlotKind? kind, PromptPartKeyedSelection<Guid>? selection, bool isRadio)
	{
		_parent = parent;
		Part = part;
		Kind = kind;
		_selection = selection;
		IsRadio = isRadio;

		DiagnosticFlags = PromptPartDiagnosticFlagInfo.CreateFromDiagnostic(part.CombinedDiagnostic);
		MetadataItems = BuildMetadata().ToImmutableList();

		ToggleDetailsCommand = new RelayCommand(() => IsDetailsVisible = !IsDetailsVisible);
		ToggleParametersCommand = new RelayCommand(ToggleParameters);
		SelectCommand = new RelayCommand(() => _parent.SelectCard(this));

		// The parameter editor is visible by default for a selected part with a schema,
		// and can be collapsed with the Tune toggle.
		_lastCanShowParameters = CanShowParameters;
		if (_lastCanShowParameters)
		{
			EnsureParameters();
			IsParametersVisible = true;
		}
		FilterByCategoryCommand = new RelayCommand<string?>(category =>
		{
			if (!string.IsNullOrEmpty(category))
				_parent.SearchText = category;
		});

		// TODO: dialogs for editing and management of prompt parts are planned but not implemented yet.
		// Wire the commands to real handlers and enable the buttons once the dialogs are ready.
		EditCommand = new RelayCommand(() => { }, () => false);
		OpenFileCommand = new RelayCommand(() => { }, () => false);
		ShowInExplorerCommand = new RelayCommand(() => { }, () => false);
		DuplicateCommand = new RelayCommand(() => { }, () => false);
		DeleteCommand = new RelayCommand(() => { }, () => false);
	}

	/// <summary>
	/// Gets the underlying prompt part.
	/// </summary>
	public PromptPartBase Part { get; }

	/// <summary>
	/// Gets the slot kind of the element, or <see langword="null"/> for components.
	/// </summary>
	public PromptSlotKind? Kind { get; }

	/// <summary>
	/// Gets a value indicating whether the selection is single-choice (radio) or multi-choice (checkbox).
	/// </summary>
	public bool IsRadio { get; }

	/// <summary>
	/// Gets the agent's selection for this part, if any.
	/// </summary>
	public PromptPartKeyedSelection<Guid>? Selection => _selection;

	/// <summary>
	/// Gets the name of the prompt part.
	/// </summary>
	public string Name => Part.Name;

	/// <summary>
	/// Gets the description of the prompt part.
	/// </summary>
	public string Description => Part.Description ?? string.Empty;

	/// <summary>
	/// Gets the category of the prompt part.
	/// </summary>
	public string Category => Part.Category ?? string.Empty;

	/// <summary>
	/// Gets a value indicating whether the category is non-empty.
	/// </summary>
	public bool HasCategory => !string.IsNullOrWhiteSpace(Category);

	/// <summary>
	/// Gets a value indicating whether the card represents a slot element (has a kind).
	/// </summary>
	public bool HasKind => Kind.HasValue;

	/// <summary>
	/// Gets the localization key of the slot kind display name.
	/// </summary>
	public LocaleKeyBase? KindNameKey => Kind is { } kind ? Locale.GetKey($"prompt.kind.{kind.ToString().ToLower()}") : null;

	/// <summary>
	/// Gets the icon of the slot kind.
	/// </summary>
	public VisualIconKind KindIcon => Kind switch
	{
		PromptSlotKind.System => MaterialIconKind.CogOutline,
		PromptSlotKind.Persona => MaterialIconKind.AccountOutline,
		PromptSlotKind.Specialization => MaterialIconKind.SchoolOutline,
		_ => MaterialIconKind.Puzzle
	};

	/// <summary>
	/// Gets the localization key of the source display name.
	/// </summary>
	public LocaleKeyBase SourceNameKey => Locale.GetKey($"prompt.source.{Part.Source.ToString().ToLower()}");

	/// <summary>
	/// Gets the icon of the source.
	/// </summary>
	public VisualIconKind SourceIcon => Part.Source switch
	{
		PromptPartSource.BuiltInTemplate => MaterialIconKind.Box,
		PromptPartSource.UserTemplate => MaterialIconKind.AccountCircle,
		PromptPartSource.WorkdirTemplate => MaterialIconKind.Folder,
		PromptPartSource.Configuration => MaterialIconKind.Cog,
		_ => MaterialIconKind.HelpCircle
	};

	/// <summary>
	/// Gets the display name of the template language.
	/// </summary>
	public string LanguageName => Part.Language.ToString();

	/// <summary>
	/// Gets the display name of the language this part is localized for, if any.
	/// </summary>
	public string? LocalizedForName => Part.LocalizedFor?.ToString();

	/// <summary>
	/// Gets a value indicating whether the part is unusable (fatal diagnostic).
	/// </summary>
	public bool IsBroken => Part.CombinedDiagnostic?.IsFatal == true;

	/// <summary>
	/// Gets the error message of a fatal diagnostic, if any.
	/// </summary>
	public string? Error
	{
		get
		{
			var diagnostic = Part.CombinedDiagnostic;
			if (diagnostic == null)
				return null;
			if (diagnostic.Exception != null)
				return diagnostic.Exception.Message;
			return diagnostic.Messages.Count > 0 ? string.Join(Environment.NewLine, diagnostic.Messages) : null;
		}
	}

	/// <summary>
	/// Gets the diagnostic flag chips of the prompt part.
	/// </summary>
	public ImmutableList<PromptPartDiagnosticFlagInfo> DiagnosticFlags { get; }

	/// <summary>
	/// Gets a value indicating whether the prompt part has any diagnostic flags.
	/// </summary>
	public bool HasDiagnostics => DiagnosticFlags.Count > 0;

	/// <summary>
	/// Gets or sets a value indicating whether the card is selected for the agent.
	/// For radio cards (slot elements) the setter selects/deselects the element,
	/// for checkbox cards (components) it toggles the component in the settings.
	/// </summary>
	public bool IsSelected
	{
		get => _parent.IsSelected(this);
		set
		{
			if (IsRadio)
			{
				if (value)
					_parent.SelectCard(this);
				else
					_parent.DeselectCard(this);
			}
			else
			{
				_parent.SetComponentSelected(this, value);
			}
		}
	}

	/// <summary>
	/// Gets the selection icon of the card (radio mode).
	/// </summary>
	public VisualIconKind SelectionIcon => IsSelected ? MaterialIconKind.RadioboxMarked : MaterialIconKind.RadioboxBlank;

	/// <summary>
	/// Gets a value indicating whether the parameter UI can be shown for this part.
	/// Parameters are only editable for the selected part with a schema.
	/// </summary>
	public bool CanShowParameters => Part.ParameterSchema is not null && IsSelected && Selection is not null;

	/// <summary>
	/// Gets or sets a value indicating whether the parameters section is visible.
	/// </summary>
	public bool IsParametersVisible
	{
		get => _isParametersVisible;
		set => SetProperty(ref _isParametersVisible, value);
	}

	/// <summary>
	/// Gets or sets a value indicating whether the details section is visible.
	/// </summary>
	public bool IsDetailsVisible
	{
		get => _isDetailsVisible;
		set => SetProperty(ref _isDetailsVisible, value);
	}

	/// <summary>
	/// Gets the LLT source code of the prompt part template.
	/// </summary>
	public string TemplateBody => Part.Template.SourceCode;

	/// <summary>
	/// Gets a value indicating whether the template body is non-empty.
	/// </summary>
	public bool HasTemplateBody => !string.IsNullOrWhiteSpace(TemplateBody);

	/// <summary>
	/// Gets the metadata entries of the prompt part.
	/// </summary>
	public ImmutableList<PromptPartMetadataItem> MetadataItems { get; }

	/// <summary>
	/// Gets a value indicating whether the details section has any content.
	/// </summary>
	public bool HasDetails => MetadataItems.Count > 0 || HasTemplateBody;

	/// <summary>
	/// Gets the command that selects this card (radio mode).
	/// </summary>
	public ICommand SelectCommand { get; }

	/// <summary>
	/// Gets the command that toggles the parameters section.
	/// </summary>
	public ICommand ToggleParametersCommand { get; }

	/// <summary>
	/// Gets the command that toggles the details section.
	/// </summary>
	public ICommand ToggleDetailsCommand { get; }

	/// <summary>
	/// Gets the command that filters the list by the category of this card.
	/// </summary>
	public ICommand FilterByCategoryCommand { get; }

	// TODO: the commands below are placeholders for the planned prompt part
	// management dialogs (edit / open / duplicate / delete). Not implemented yet.
	public ICommand EditCommand { get; }
	public ICommand OpenFileCommand { get; }
	public ICommand ShowInExplorerCommand { get; }
	public ICommand DuplicateCommand { get; }
	public ICommand DeleteCommand { get; }

	/// <summary>
	/// Sets the agent's selection object for this card (used when a component gets checked).
	/// </summary>
	public void SetSelection(PromptPartKeyedSelection<Guid>? selection)
	{
		if (ReferenceEquals(_selection, selection))
			return;
		if (_parameterValueSubscribed && _selection?.Parameters is { } oldValue)
			oldValue.PropertyChanged -= Parameters_PropertyChanged;
		_parameterValueSubscribed = false;
		_selection = selection;
		RaisePropertyChanged(nameof(Selection));
		RaisePropertyChanged(nameof(CanShowParameters));
	}

	/// <summary>
	/// Creates or fixes the parameter value from the part's schema for the current selection.
	/// </summary>
	public void EnsureParameters()
	{
		if (Part.ParameterSchema is null || Selection is null)
			return;

		var log = new AppendOnlyList<ParameterValidationLogEntry>();
		Selection.Parameters = Part.ParameterSchema.Root.CreateOrFixValue(Selection.Parameters, log);
		if (!_parameterValueSubscribed && Selection.Parameters is { } value)
		{
			_parameterValueSubscribed = true;
			value.PropertyChanged += Parameters_PropertyChanged;
		}
		RaisePropertyChanged(nameof(CanShowParameters));
	}

	/// <summary>
	/// Notifies the card that its selection state changed (called by the parent).
	/// </summary>
	public void NotifySelectionChanged()
	{
		var canShow = CanShowParameters;
		if (canShow != _lastCanShowParameters)
		{
			// Open the parameter editor automatically when the part becomes selectable
			// (selected and has a schema), and collapse it when it is deselected.
			_lastCanShowParameters = canShow;
			IsParametersVisible = canShow;
		}

		RaisePropertyChanged(nameof(IsSelected));
		RaisePropertyChanged(nameof(SelectionIcon));
		RaisePropertyChanged(nameof(CanShowParameters));
	}

	private void ToggleParameters()
	{
		if (!CanShowParameters)
			return;
		EnsureParameters();
		IsParametersVisible = !IsParametersVisible;
	}

	private void Parameters_PropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		_parent.RegeneratePreview();
	}

	private IEnumerable<PromptPartMetadataItem> BuildMetadata()
	{
		if (Kind is { } kind)
			yield return new PromptPartMetadataItem(Locale.GetKey("prompt.metadata.kind"), LocalizeKindValue(kind));
		yield return new PromptPartMetadataItem(Locale.GetKey("prompt.metadata.guid"), Part.Guid.ToString());
		if (!string.IsNullOrEmpty(Part.StrId))
			yield return new PromptPartMetadataItem(Locale.GetKey("prompt.metadata.strid"), Part.StrId);
		yield return new PromptPartMetadataItem(Locale.GetKey("prompt.metadata.language"), Part.Language.ToString());
		if (Part.LocalizedFor is { } localizedFor)
			yield return new PromptPartMetadataItem(Locale.GetKey("prompt.metadata.localized_for"), localizedFor.ToString());
		yield return new PromptPartMetadataItem(Locale.GetKey("prompt.metadata.source"), SourceNameKey.Value);
		if (HasCategory)
			yield return new PromptPartMetadataItem(Locale.GetKey("prompt.metadata.category"), Category);
	}

	private static string LocalizeKindValue(PromptSlotKind kind)
	{
		return Locale.Get($"prompt.kind.{kind.ToString().ToLower()}");
	}

	/// <inheritdoc/>
	protected override void Dispose(bool disposing)
	{
		base.Dispose(disposing);

		if (disposing && _parameterValueSubscribed && Selection?.Parameters is { } value)
			value.PropertyChanged -= Parameters_PropertyChanged;
	}
}
