using System.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LLMDesktopAssistant.Addons.MVVM;
using LLMDesktopAssistant.Localization;
using LLMDesktopAssistant.LLM.MVVM.Settings.Agents;
using LLMDesktopAssistant.Tools.Specifiers;
using LLMDesktopAssistant.Utils;
using Material.Icons;

namespace LLMDesktopAssistant.Tools.MVVM.Elements
{
	/// <summary>
	/// The collapsible block of a tool card that edits the specifier rules of the tool: the union and
	/// aggregation modes, the hint of the supported specifier parameters and the rule list. The block is
	/// shown only for tools that declare a specifier analyzer; the editor is active only while the
	/// effective approval level of the tool is policy-based.
	/// </summary>
	public class AddonCardToolSpecifiersBlock : AddonCardBlockChange
	{
		private readonly AddonCardContext<ToolInfo, ToolChange> _context;

		private ToolChange? _subscribedChange;

		/// <summary>
		/// Initializes a new instance of the <see cref="AddonCardToolSpecifiersBlock"/> class.
		/// </summary>
		/// <param name="context">The addon, its change set and the page that owns the card.</param>
		public AddonCardToolSpecifiersBlock(AddonCardContext<ToolInfo, ToolChange> context)
		{
			ArgumentNullException.ThrowIfNull(context);

			_context = context;

			AddCommand = new RelayCommand(AddRule);

			Visibility = AddonCardBlockVisibility.Collapsible;
			Title = Locale.GetKey("card.tools.specifiers");
			ToggleIcon = MaterialIconKind.FormatListBulleted;
			ToggleToolTip = Locale.GetKey("card.tools.specifiers.toggle");
			Content = new AddonCardToolSpecifiersViewModel(this);

			_context.PropertyChanged += Context_PropertyChanged;
			SubscribeChange();

			RebuildRules();
			SyncIsChanged();
		}

		/// <summary>
		/// Gets the specifier rules of the tool.
		/// </summary>
		public RangeObservableCollection<ToolSpecifierRuleRowViewModel> Rules { get; } = [];

		/// <summary>
		/// Gets the command that adds a new specifier rule.
		/// </summary>
		public ICommand AddCommand { get; }

		/// <summary>
		/// Gets a value indicating whether the specifier editor is applied by the current approval level.
		/// </summary>
		public bool IsSectionEnabled => EffectiveApprovalLevel.IsPolicyBased();

		/// <summary>
		/// Gets a value indicating whether the rule list is editable: the list is disabled when the
		/// specifiers are turned off by the union mode.
		/// </summary>
		public bool IsRulesEnabled => IsSectionEnabled && EffectiveUnionMode != SpecifierBehaviourUnionMode.Disabled;

		/// <summary>
		/// Gets the localized hint with the names of the specifier parameters supported by the tool,
		/// or <see langword="null"/> when the tool has no specifier parameters.
		/// </summary>
		public string? SpecifierParametersHint =>
			_context.Addon.Arguments.SpecifierParameters.Select(argument => argument.Name).ToList() is { Count: > 0 } names
				? Locale.Format("tool.specifier.parameters", string.Join(", ", names))
				: null;

		/// <summary>
		/// Gets a value indicating whether the tool declares specifier parameters.
		/// </summary>
		public bool HasParametersHint => SpecifierParametersHint is not null;

		/// <summary>
		/// Gets or sets the specifier behaviour union mode of the tool.
		/// </summary>
		public SpecifierUnionModeItem? UnionMode
		{
			get => SpecifierUnionModeItem.All.FirstOrDefault(item => item.Value == EffectiveUnionMode);
			set
			{
				if (value is null || UnionMode?.Value == value.Value)
					return;

				_context.EnsureChange().SpecifierUnionMode = value.Value;
				RaisePropertyChanged();
				RaisePropertyChanged(nameof(IsRulesEnabled));
				SyncIsChanged();
			}
		}

		/// <summary>
		/// Gets or sets the specifier aggregation mode of the tool.
		/// </summary>
		public SpecifierAggregationModeItem? AggregationMode
		{
			get => SpecifierAggregationModeItem.All.FirstOrDefault(item => item.Value == EffectiveAggregationMode);
			set
			{
				if (value is null || AggregationMode?.Value == value.Value)
					return;

				_context.EnsureChange().SpecifierAggregationMode = value.Value;
				RaisePropertyChanged();
				SyncIsChanged();
			}
		}

		private ToolApprovalLevel EffectiveApprovalLevel =>
			_context.Change?.ApprovalLevel
			?? _context.Addon.ApprovalLevel
			?? (_context.SetConfig as ToolsetConfiguration)?.DefaultApprovalLevel
			?? ToolApprovalLevel.PolicyBased;

		private SpecifierBehaviourUnionMode EffectiveUnionMode =>
			_context.Change?.SpecifierUnionMode
			?? _context.Addon.SpecifierUnionMode
			?? SpecifierBehaviourUnionMode.CombineSoft;

		private SpecifierAggregationMode EffectiveAggregationMode =>
			_context.Change?.SpecifierAggregationMode
			?? _context.Addon.SpecifierAggregationMode;

		/// <inheritdoc/>
		protected override void Dispose(bool disposing)
		{
			base.Dispose(disposing);

			if (disposing)
			{
				(Content as IDisposable)?.Dispose();

				_context.PropertyChanged -= Context_PropertyChanged;

				if (_subscribedChange is not null)
					_subscribedChange.PropertyChanged -= Change_PropertyChanged;
			}
		}

		/// <inheritdoc/>
		protected override void ResetCore()
		{
			base.ResetCore();

			if (_context.Change is { } change)
			{
				change.SpecifierUnionMode = null;
				change.SpecifierAggregationMode = null;
				change.Specifiers.Clear();
			}

			RebuildRules();
			RaiseDerived();
			SyncIsChanged();
		}

		/// <summary>
		/// Persists the current rule list to the tool change.
		/// </summary>
		internal void SyncSpecifiers()
		{
			_context.EnsureChange().Specifiers.Reset(Rules.Select(row => new ToolSpecifierRule
			{
				Enabled = row.Enabled,
				Pattern = row.Pattern,
				Decision = row.Decision?.Value ?? SpecifierDecision.Allow,
				
			}));

			SyncIsChanged();
		}

		/// <summary>
		/// Removes the specified rule from the tool.
		/// </summary>
		/// <param name="row">The rule row to remove.</param>
		internal void RemoveRule(ToolSpecifierRuleRowViewModel row)
		{
			if (!Rules.Remove(row))
				return;

			SyncSpecifiers();
		}

		private void AddRule()
		{
			Rules.Add(new ToolSpecifierRuleRowViewModel(this, true, string.Empty, SpecifierDecision.Allow));
			SyncSpecifiers();
		}

		private void RebuildRules()
		{
			IEnumerable<ToolSpecifierRule> source = _context.Change is { } change
				? change.Specifiers
				: _context.Addon.Specifiers;

			Rules.Reset(source.Select(rule => new ToolSpecifierRuleRowViewModel(this, rule.Enabled, rule.Pattern, rule.Decision)));
		}

		private void SubscribeChange()
		{
			if (ReferenceEquals(_subscribedChange, _context.Change))
				return;

			if (_subscribedChange is not null)
				_subscribedChange.PropertyChanged -= Change_PropertyChanged;

			_subscribedChange = _context.Change;

			if (_subscribedChange is not null)
				_subscribedChange.PropertyChanged += Change_PropertyChanged;
		}

		private void Context_PropertyChanged(object? sender, PropertyChangedEventArgs e)
		{
			if (e.PropertyName is not nameof(AddonCardContext<,>.Change))
				return;

			SubscribeChange();

			if (_context.Change is null)
				RebuildRules();

			RaiseDerived();
			SyncIsChanged();
		}

		private void Change_PropertyChanged(object? sender, PropertyChangedEventArgs e)
		{
			switch (e.PropertyName)
			{
				case nameof(ToolChange.ApprovalLevel):
					RaisePropertyChanged(nameof(IsSectionEnabled));
					RaisePropertyChanged(nameof(IsRulesEnabled));
					break;

				case nameof(ToolChange.SpecifierUnionMode):
					RaisePropertyChanged(nameof(UnionMode));
					RaisePropertyChanged(nameof(IsRulesEnabled));
					break;

				case nameof(ToolChange.SpecifierAggregationMode):
					RaisePropertyChanged(nameof(AggregationMode));
					break;
			}
		}

		private void RaiseDerived()
		{
			RaisePropertyChanged(nameof(IsSectionEnabled));
			RaisePropertyChanged(nameof(IsRulesEnabled));
			RaisePropertyChanged(nameof(UnionMode));
			RaisePropertyChanged(nameof(AggregationMode));
		}

		private void SyncIsChanged()
		{
			IsChanged = _context.Change is { } change
				&& (change.SpecifierUnionMode is not null
					|| change.SpecifierAggregationMode is not null
					|| change.Specifiers.Count > 0);
		}
	}
}
