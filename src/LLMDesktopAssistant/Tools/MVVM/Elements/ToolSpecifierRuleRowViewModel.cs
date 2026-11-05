using CommunityToolkit.Mvvm.Input;
using LLMDesktopAssistant.Controls.Text;
using LLMDesktopAssistant.LLM.MVVM.Settings.Agents;
using LLMDesktopAssistant.Localization;
using LLMDesktopAssistant.Tools.Execution.Domain;
using LLMDesktopAssistant.Tools.Specifiers;
using LLMDesktopAssistant.Utils.Patterns;

namespace LLMDesktopAssistant.Tools.MVVM.Elements
{
	/// <summary>
	/// The view model of a single specifier rule row of a tool card: the pattern, its validation, the
	/// decision and the remove command. Every change is persisted to the tool change immediately; a broken
	/// pattern persists too, and stays inert at runtime - which is why the row shows the error right away.
	/// </summary>
	public class ToolSpecifierRuleRowViewModel : NotifyPropertyChanged
	{
		private readonly AddonCardToolSpecifiersBlock _owner;
		private readonly ToolArgumentsDescriptor _arguments;
		private readonly SpecifierPatternHighlighter _highlighter;
		private bool _enabled;
		private string _pattern;
		private SpecifierDecisionItem? _decision;

		/// <summary>
		/// Initializes a new instance of the <see cref="ToolSpecifierRuleRowViewModel"/> class.
		/// </summary>
		/// <param name="owner">The block the row belongs to.</param>
		/// <param name="arguments">The arguments of the tool, which the pattern is validated against.</param>
		/// <param name="enabled">Whether the rule is applied.</param>
		/// <param name="pattern">The specifier pattern of the rule.</param>
		/// <param name="decision">The decision applied when the pattern matches.</param>
		public ToolSpecifierRuleRowViewModel(AddonCardToolSpecifiersBlock owner,
			ToolArgumentsDescriptor arguments, bool enabled, string pattern, SpecifierDecision decision)
		{
			_owner = owner;
			_arguments = arguments;
			_enabled = enabled;
			_pattern = pattern;
			_highlighter = new SpecifierPatternHighlighter(arguments);
			_decision = Decisions.FirstOrDefault(item => item.Value == decision) ?? Decisions.FirstOrDefault();

			HighlightProvider = _highlighter.CreateProvider();
			RemoveCommand = new RelayCommand(() => owner.RemoveRule(this));

			Validate();
		}

		public bool Enabled
		{
			get => _enabled;
			set
			{
				if (_enabled == value)
					return;

				_enabled = value;
				_owner.SyncSpecifiers();
				RaisePropertyChanged();
			}
		}

		/// <summary>
		/// Gets or sets the specifier pattern. Changes are persisted to the tool change immediately and
		/// revalidate the row.
		/// </summary>
		public string Pattern
		{
			get => _pattern;
			set
			{
				if (_pattern == value)
					return;

				_pattern = value;
				Validate();
				_owner.SyncSpecifiers();
				RaisePropertyChanged();
			}
		}

		/// <summary>
		/// The localized problem of the pattern, or <see langword="null"/> when it is valid.
		/// </summary>
		public string? Error
		{
			get => field;
			private set
			{
				if (SetProperty(ref field, value))
					RaisePropertyChanged(nameof(HasError));
			}
		}

		/// <summary>
		/// Whether the pattern has a problem.
		/// </summary>
		public bool HasError => Error is not null;

		/// <summary>
		/// The provider that colours the pattern text and underlines a broken literal.
		/// </summary>
		public IHighlightTransformProvider HighlightProvider { get; }

		/// <summary>
		/// Gets all available specifier decisions with localized display names.
		/// </summary>
		public ImmutableList<SpecifierDecisionItem> Decisions { get; } = SpecifierDecisionItem.All;

		/// <summary>
		/// Gets or sets the decision applied when the pattern matches. Changes are persisted to the tool change immediately.
		/// </summary>
		public SpecifierDecisionItem? Decision
		{
			get => _decision;
			set
			{
				if (value is null || _decision?.Value == value.Value)
					return;

				_decision = value;
				_owner.SyncSpecifiers();
				RaisePropertyChanged();
			}
		}

		/// <summary>
		/// Gets the command that removes this specifier rule from the tool.
		/// </summary>
		public ICommand RemoveCommand { get; }

		/// <summary>
		/// Revalidates the pattern and turns the first problem into a localized message.
		/// </summary>
		private void Validate()
		{
			Error = SpecifierPatternValidator.Validate(_pattern ?? string.Empty, _arguments)
				.Select(Message)
				.FirstOrDefault();
		}

		/// <summary>
		/// Localizes one diagnostic. The technical glob message is only used by the fallback, because the
		/// engine speaks English while the card speaks the interface language.
		/// </summary>
		private static string Message(SpecifierDiagnostic diagnostic) => diagnostic.Kind switch
		{
			SpecifierDiagnosticKind.Empty => Locale.Get("card.tools.specifiers.error.empty"),
			SpecifierDiagnosticKind.Syntax => Locale.Format("card.tools.specifiers.error.syntax", diagnostic.Position),
			SpecifierDiagnosticKind.NoTarget => Locale.Get("card.tools.specifiers.error.no_target"),
			_ => Locale.Format($"card.tools.specifiers.error.{Suffix(diagnostic.Error)}", diagnostic.Detail)
		};

		/// <summary>
		/// The locale key suffix of a pattern error, or the generic one for the errors this dialect does
		/// not produce.
		/// </summary>
		private static string Suffix(GlobError? error) => error switch
		{
			GlobError.TooLong => "too_long",
			GlobError.TooManyAlternatives => "too_many_alternatives",
			GlobError.TooManyUnits => "too_many_units",
			GlobError.InvalidClassRange => "invalid_class_range",
			GlobError.DoubleStarNotWholeSegment => "double_star_not_whole_segment",
			GlobError.DoubleStarUnsupported => "double_star",
			GlobError.ArgumentTailNotTerminal => "argument_tail",
			_ => "generic"
		};
	}
}
