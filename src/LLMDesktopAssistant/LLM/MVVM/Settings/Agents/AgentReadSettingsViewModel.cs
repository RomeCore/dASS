using System.ComponentModel;
using System.Text;
using Avalonia.Media;
using CommunityToolkit.Mvvm.Input;
using LLMDesktopAssistant.Agents;
using LLMDesktopAssistant.Agents.Settings;
using LLMDesktopAssistant.LLM.Services.Prompting;
using LLMDesktopAssistant.LLM.Settings;
using LLMDesktopAssistant.Localization;
using LLMDesktopAssistant.MVVM;
using LLMDesktopAssistant.Settings;
using LLMDesktopAssistant.Utils;
using Material.Icons;

namespace LLMDesktopAssistant.LLM.MVVM.Settings.Agents
{
	/// <summary>
	/// A single visibility facet that can be toggled in an agent read/share matrix.
	/// </summary>
	public enum AgentVisibilityFacet
	{
		Visible,
		MessagesWithToolCalls,
		MessagesWithoutToolCalls,
		BriefReasoning,
		Reasoning,
		Content,
		NativeAttachments,
		Attachments,
		ToolCallFacts,
		BriefToolCallArguments,
		BriefToolCallResults,
		ToolCallArguments,
		ToolCallResults,
		ToolCallNativeAttachments,
		Identity,
	}

	/// <summary>
	/// Metadata (icons, colors and localized texts) for <see cref="AgentVisibilityFacet"/> values.
	/// </summary>
	public static class AgentVisibilityFacetInfo
	{
		public static LocaleKeyBase GetDisplayName(AgentVisibilityFacet facet)
			=> Locale.GetKey($"settings.agent.facet.{Slug(facet)}");

		public static LocaleKeyBase GetDescription(AgentVisibilityFacet facet)
			=> Locale.GetKey($"settings.agent.facet.{Slug(facet)}.hint");

		public static MaterialIconKind GetIcon(AgentVisibilityFacet facet, bool isOn) => facet switch
		{
			AgentVisibilityFacet.Visible => isOn ? MaterialIconKind.Eye : MaterialIconKind.EyeOff,
			AgentVisibilityFacet.MessagesWithToolCalls => MaterialIconKind.Wrench,
			AgentVisibilityFacet.MessagesWithoutToolCalls => MaterialIconKind.Chat,
			AgentVisibilityFacet.BriefReasoning => MaterialIconKind.LightbulbOn,
			AgentVisibilityFacet.Reasoning => MaterialIconKind.Brain,
			AgentVisibilityFacet.Content => MaterialIconKind.Text,
			AgentVisibilityFacet.NativeAttachments => MaterialIconKind.Image,
			AgentVisibilityFacet.Attachments => MaterialIconKind.FileDocument,
			AgentVisibilityFacet.ToolCallFacts => MaterialIconKind.Tools,
			AgentVisibilityFacet.BriefToolCallArguments => MaterialIconKind.CodeBraces,
			AgentVisibilityFacet.BriefToolCallResults => MaterialIconKind.FormatListBulleted,
			AgentVisibilityFacet.ToolCallArguments => MaterialIconKind.CodeJson,
			AgentVisibilityFacet.ToolCallResults => MaterialIconKind.ArchiveOutline,
			AgentVisibilityFacet.ToolCallNativeAttachments => MaterialIconKind.FileImage,
			AgentVisibilityFacet.Identity => MaterialIconKind.Account,
			_ => MaterialIconKind.HelpCircle,
		};

		public static IBrush GetColor(AgentVisibilityFacet facet) => facet switch
		{
			AgentVisibilityFacet.Visible => Brushes.LimeGreen,
			AgentVisibilityFacet.MessagesWithToolCalls => Brushes.Orange,
			AgentVisibilityFacet.MessagesWithoutToolCalls => Brushes.Orange,
			AgentVisibilityFacet.BriefReasoning => Brushes.MediumPurple,
			AgentVisibilityFacet.Reasoning => Brushes.MediumPurple,
			AgentVisibilityFacet.Content => Brushes.DodgerBlue,
			AgentVisibilityFacet.NativeAttachments => Brushes.Teal,
			AgentVisibilityFacet.Attachments => Brushes.Teal,
			AgentVisibilityFacet.ToolCallFacts => Brushes.Orange,
			AgentVisibilityFacet.BriefToolCallArguments => Brushes.Goldenrod,
			AgentVisibilityFacet.BriefToolCallResults => Brushes.DarkOrange,
			AgentVisibilityFacet.ToolCallArguments => Brushes.Goldenrod,
			AgentVisibilityFacet.ToolCallResults => Brushes.DarkOrange,
			AgentVisibilityFacet.ToolCallNativeAttachments => Brushes.Orange,
			AgentVisibilityFacet.Identity => Brushes.MediumPurple,
			_ => Brushes.Gray,
		};

		public static IBrush GetIdentityColor(MessageAuthorIdentity identity) => identity switch
		{
			MessageAuthorIdentity.Default => Brushes.Gray,
			MessageAuthorIdentity.Anon => Brushes.MediumPurple,
			MessageAuthorIdentity.UnnamedUser => Brushes.DodgerBlue,
			MessageAuthorIdentity.UnnamedAgent => Brushes.DodgerBlue,
			MessageAuthorIdentity.NamedUser => Brushes.LimeGreen,
			MessageAuthorIdentity.NamedAgent => Brushes.LimeGreen,
			_ => Brushes.Gray,
		};

		public static MaterialIconKind GetIdentityIcon(MessageAuthorIdentity identity) => identity switch
		{
			MessageAuthorIdentity.Default => MaterialIconKind.AccountQuestion,
			MessageAuthorIdentity.Anon => MaterialIconKind.Incognito,
			MessageAuthorIdentity.UnnamedUser => MaterialIconKind.AccountOutline,
			MessageAuthorIdentity.UnnamedAgent => MaterialIconKind.RobotOutline,
			MessageAuthorIdentity.NamedUser => MaterialIconKind.Account,
			MessageAuthorIdentity.NamedAgent => MaterialIconKind.Robot,
			_ => MaterialIconKind.Account,
		};

		public static LocaleKeyBase GetIdentityName(MessageAuthorIdentity identity)
			=> Locale.GetKey($"settings.agent.identity.{SlugIdentity(identity)}");

		public static MessageAuthorIdentity GetNextIdentity(MessageAuthorIdentity identity) => identity switch
		{
			MessageAuthorIdentity.Default => MessageAuthorIdentity.Anon,
			MessageAuthorIdentity.Anon => MessageAuthorIdentity.UnnamedUser,
			MessageAuthorIdentity.UnnamedUser => MessageAuthorIdentity.UnnamedAgent,
			MessageAuthorIdentity.UnnamedAgent => MessageAuthorIdentity.NamedUser,
			MessageAuthorIdentity.NamedUser => MessageAuthorIdentity.NamedAgent,
			_ => MessageAuthorIdentity.Default,
		};

		private static string SlugIdentity(MessageAuthorIdentity identity) => identity switch
		{
			MessageAuthorIdentity.Default => "default",
			MessageAuthorIdentity.Anon => "anon",
			MessageAuthorIdentity.UnnamedUser => "unnamed_user",
			MessageAuthorIdentity.UnnamedAgent => "unnamed_agent",
			MessageAuthorIdentity.NamedUser => "named_user",
			MessageAuthorIdentity.NamedAgent => "named_agent",
			_ => "default",
		};

		private static string Slug(AgentVisibilityFacet facet)
		{
			var name = facet.ToString();
			var builder = new StringBuilder(name.Length + 8);
			for (int i = 0; i < name.Length; i++)
			{
				var c = name[i];
				if (char.IsUpper(c) && i > 0)
					builder.Append('_');
				builder.Append(char.ToLowerInvariant(c));
			}
			return builder.ToString();
		}
	}

	/// <summary>
	/// A single facet toggle in an agent visibility matrix row.
	/// </summary>
	public class AgentVisibilityCellViewModel : NotifyPropertyChanged
	{
		private readonly Action<bool> _onToggle;
		private readonly Action _onReset;
		private bool _isOn;

		public AgentVisibilityFacet Facet { get; }
		public LocaleKeyBase DisplayName { get; }
		public LocaleKeyBase Description { get; }
		public MaterialIconKind Icon { get; }
		public IBrush Color { get; }
		public string StateText { get; }
		public IBrush StateColor { get; }

		/// <summary>
		/// Whether this cell supports per-element overrides (shows the accent reset marker).
		/// </summary>
		public bool CanOverride { get; }

		/// <summary>
		/// Whether this element is currently overridden.
		/// </summary>
		public bool IsOverridden { get; }

		/// <summary>
		/// Whether a group separator should be drawn before this cell.
		/// </summary>
		public bool IsGroupStart { get; }

		/// <summary>
		/// Whether this cell is blocked by the agent's read restriction (toggling it cannot change what is read anyway).
		/// </summary>
		public bool IsBlocked { get; }

		public IRelayCommand ResetCommand { get; }

		public bool IsOn
		{
			get => _isOn;
			set
			{
				if (_isOn == value)
					return;
				_isOn = value;
				RaisePropertyChanged();
				_onToggle(value);
			}
		}

		public AgentVisibilityCellViewModel(AgentVisibilityFacet facet, bool isOn, bool isOverridden,
			bool canOverride, bool isGroupStart, bool isBlocked, string stateText, IBrush stateColor,
			Action<bool> onToggle, Action onReset)
		{
			Facet = facet;
			_isOn = isOn;
			IsOverridden = isOverridden;
			CanOverride = canOverride;
			IsGroupStart = isGroupStart;
			IsBlocked = isBlocked;
			StateText = stateText;
			StateColor = stateColor;
			_onToggle = onToggle;
			_onReset = onReset;

			DisplayName = AgentVisibilityFacetInfo.GetDisplayName(facet);
			Description = AgentVisibilityFacetInfo.GetDescription(facet);
			Icon = AgentVisibilityFacetInfo.GetIcon(facet, isOn);
			Color = AgentVisibilityFacetInfo.GetColor(facet);
			ResetCommand = new RelayCommand(onReset);
		}
	}

	/// <summary>
	/// The cyclic identity toggle in an agent visibility matrix row.
	/// </summary>
	public class AgentIdentityCellViewModel : NotifyPropertyChanged
	{
		public LocaleKeyBase DisplayName { get; }
		public LocaleKeyBase Description { get; }
		public MessageAuthorIdentity Identity { get; }
		public MaterialIconKind Icon { get; }
		public IBrush Color { get; }
		public string StateText { get; }
		public IBrush StateColor { get; }
		public bool CanOverride { get; }
		public bool IsOverridden { get; }
		public IRelayCommand CycleCommand { get; }
		public IRelayCommand ResetCommand { get; }

		public AgentIdentityCellViewModel(MessageAuthorIdentity identity, bool isOverridden, bool canOverride,
			string stateText, IBrush stateColor, Action<MessageAuthorIdentity> onCycle, Action onReset)
		{
			Identity = identity;
			IsOverridden = isOverridden;
			CanOverride = canOverride;
			StateText = stateText;
			StateColor = stateColor;

			DisplayName = AgentVisibilityFacetInfo.GetDisplayName(AgentVisibilityFacet.Identity);
			Description = AgentVisibilityFacetInfo.GetDescription(AgentVisibilityFacet.Identity);
			Icon = AgentVisibilityFacetInfo.GetIdentityIcon(identity);
			Color = AgentVisibilityFacetInfo.GetIdentityColor(identity);
			CycleCommand = new RelayCommand(() => onCycle(AgentVisibilityFacetInfo.GetNextIdentity(Identity)));
			ResetCommand = new RelayCommand(onReset);
		}
	}

	/// <summary>
	/// A single row (participant or default audience) of an agent visibility matrix.
	/// </summary>
	public class AgentVisibilityRowViewModel : NotifyPropertyChanged
	{
		public string Header { get; }
		public bool ShowGlobalBadge { get; }
		public bool ShowRowReset { get; }
		public bool HasOverrides { get; }
		public RangeObservableCollection<AgentVisibilityCellViewModel> Cells { get; } = [];
		public AgentIdentityCellViewModel IdentityCell { get; }
		public IRelayCommand? ResetRowCommand { get; }

		public AgentVisibilityRowViewModel(string header, bool showGlobalBadge, bool showRowReset,
			bool hasOverrides, IRelayCommand? resetRowCommand, AgentIdentityCellViewModel identityCell)
		{
			Header = header;
			ShowGlobalBadge = showGlobalBadge;
			ShowRowReset = showRowReset;
			HasOverrides = hasOverrides;
			ResetRowCommand = resetRowCommand;
			IdentityCell = identityCell;
		}
	}

	[ViewModelFor(typeof(AgentReadSettingsView))]
	public class AgentReadSettingsViewModel : ViewModelBase
	{
		private readonly AgentReadSettings _settings;
		private readonly ChatAgentDescriptor _agent;
		private readonly ICollection<ChatAgentDescriptor> _chatAgents;
		private readonly ChatSettings _chatSettings;
		private readonly List<(ChatAgentDescriptor Descriptor, bool IsGlobal)> _otherAgents;
		private readonly List<Disposable> _subscriptions = [];

		/// <summary>
		/// Gets the underlying agent read settings.
		/// </summary>
		public AgentReadSettings ReadSettings => _settings;

		private InheritanceLevelItem _selectedReadFiltersInheritance;
		/// <summary>
		/// Gets or sets the inheritance level for the read filters.
		/// </summary>
		public InheritanceLevelItem SelectedReadFiltersInheritance
		{
			get => _selectedReadFiltersInheritance;
			set
			{
				if (SetProperty(ref _selectedReadFiltersInheritance, value) && value != null)
					_settings.ReadFiltersInheritance = value.Value;
			}
		}

		private InheritanceLevelItem _selectedDefaultShareFiltersInheritance;
		/// <summary>
		/// Gets or sets the inheritance level for the default share filters.
		/// </summary>
		public InheritanceLevelItem SelectedDefaultShareFiltersInheritance
		{
			get => _selectedDefaultShareFiltersInheritance;
			set
			{
				if (SetProperty(ref _selectedDefaultShareFiltersInheritance, value) && value != null)
					_settings.DefaultShareFiltersInheritance = value.Value;
			}
		}

		/// <summary>
		/// The "what I read" default rows: users and agents.
		/// </summary>
		public RangeObservableCollection<AgentVisibilityRowViewModel> ReadDefaultRows { get; } = [];

		/// <summary>
		/// The per-agent "what I read from them" sugar matrix (assembled from their share filters).
		/// </summary>
		public RangeObservableCollection<AgentVisibilityRowViewModel> ReadAgentRows { get; } = [];

		/// <summary>
		/// The "what others read from me" default rows: users and agents.
		/// </summary>
		public RangeObservableCollection<AgentVisibilityRowViewModel> ShareDefaultRows { get; } = [];

		/// <summary>
		/// The per-agent "what they read from me" override matrix.
		/// </summary>
		public RangeObservableCollection<AgentVisibilityRowViewModel> ShareAgentRows { get; } = [];

		public bool HasReadAgentRows => ReadAgentRows.Count > 0;
		public bool HasShareAgentRows => ShareAgentRows.Count > 0;

		/// <summary>
		/// Initializes a new instance of the <see cref="AgentReadSettingsViewModel"/> class.
		/// </summary>
		/// <param name="settings">The agent read settings to edit.</param>
		/// <param name="chatAgents">The chat-local agent descriptors.</param>
		/// <param name="agent">The agent being edited.</param>
		/// <param name="chatSettings">The chat settings used to resolve inherited settings.</param>
		public AgentReadSettingsViewModel(AgentReadSettings settings,
			ICollection<ChatAgentDescriptor> chatAgents, ChatAgentDescriptor agent, ChatSettings chatSettings)
		{
			_settings = settings;
			_chatAgents = chatAgents;
			_agent = agent;
			_chatSettings = chatSettings;

			_selectedReadFiltersInheritance = InheritanceLevelItem.AllAgent.First(i => i.Value == settings.ReadFiltersInheritance);
			_selectedDefaultShareFiltersInheritance = InheritanceLevelItem.AllAgent.First(i => i.Value == settings.DefaultShareFiltersInheritance);

			_otherAgents = BuildOtherAgentsList();

			_settings.PropertyChanged += Settings_PropertyChanged;
			_subscriptions.Add(new Disposable(() => _settings.PropertyChanged -= Settings_PropertyChanged));

			_settings.ParticipantsShareFilters.CollectionChanged += ParticipantsShareFilters_CollectionChanged;
			_subscriptions.Add(new Disposable(() => _settings.ParticipantsShareFilters.CollectionChanged -= ParticipantsShareFilters_CollectionChanged));

			foreach (var (descriptor, _) in _otherAgents)
			{
				var other = descriptor;
				void OnOtherSettingsChanged(object? sender, PropertyChangedEventArgs e) => RefreshReadAgentRows();

				other.Read.PropertyChanged += OnOtherSettingsChanged;
				_subscriptions.Add(new Disposable(() => other.Read.PropertyChanged -= OnOtherSettingsChanged));

				void OnOtherSharesChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e) => RefreshReadAgentRows();

				other.Read.ParticipantsShareFilters.CollectionChanged += OnOtherSharesChanged;
				_subscriptions.Add(new Disposable(() => other.Read.ParticipantsShareFilters.CollectionChanged -= OnOtherSharesChanged));
			}

			RefreshAll();
		}

		private List<(ChatAgentDescriptor Descriptor, bool IsGlobal)> BuildOtherAgentsList()
		{
			var result = new List<(ChatAgentDescriptor Descriptor, bool IsGlobal)>();

			var globalConfig = SettingsManager.Get<AgentsConfiguration>();
			foreach (var descriptor in globalConfig.Agents)
				result.Add((descriptor, true));

			foreach (var descriptor in _chatAgents)
			{
				if (!result.Any(a => a.Descriptor.Id == descriptor.Id))
					result.Add((descriptor, false));
			}

			return [.. result.Where(a => a.Descriptor.Id != _agent.Id)];
		}

		private void Settings_PropertyChanged(object? sender, PropertyChangedEventArgs e)
		{
			switch (e.PropertyName)
			{
				case nameof(AgentReadSettings.ReadFiltersInheritance):
					_selectedReadFiltersInheritance = InheritanceLevelItem.AllAgent.First(i => i.Value == _settings.ReadFiltersInheritance);
					RaisePropertyChanged(nameof(SelectedReadFiltersInheritance));
					RefreshReadDefaultRows();
					RefreshReadAgentRows();
					break;

				case nameof(AgentReadSettings.DefaultShareFiltersInheritance):
					_selectedDefaultShareFiltersInheritance = InheritanceLevelItem.AllAgent.First(i => i.Value == _settings.DefaultShareFiltersInheritance);
					RaisePropertyChanged(nameof(SelectedDefaultShareFiltersInheritance));
					RefreshShareDefaultRows();
					RefreshShareAgentRows();
					break;

				case nameof(AgentReadSettings.ReadFilters):
					RefreshReadDefaultRows();
					RefreshReadAgentRows();
					break;

				case nameof(AgentReadSettings.DefaultShareFilters):
					RefreshShareDefaultRows();
					RefreshShareAgentRows();
					break;
			}
		}

		private void ParticipantsShareFilters_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
			=> RefreshShareAgentRows();

		private void RefreshAll()
		{
			RefreshReadDefaultRows();
			RefreshShareDefaultRows();
			RefreshReadAgentRows();
			RefreshShareAgentRows();
		}

		#region Read defaults

		private void RefreshReadDefaultRows()
		{
			var rows = _settings.GetEffectiveReadFilters(_chatSettings);

			void RefreshReadSection()
			{
				RefreshReadDefaultRows();
				RefreshReadAgentRows();
			}

			ReadDefaultRows.Reset(
			[
				BuildDefaultRow(rows.User, "agent.row.users", false, RefreshReadSection),
				BuildDefaultRow(rows.Agent, "agent.row.agents", false, RefreshReadSection),
			]);
		}

		private void RefreshShareDefaultRows()
		{
			var rows = _settings.GetEffectiveDefaultShareFilters(_chatSettings);

			void RefreshShareSection()
			{
				RefreshShareDefaultRows();
				RefreshShareAgentRows();
			}

			ShareDefaultRows.Reset(
			[
				BuildDefaultRow(rows.User, "agent.row.users", true, RefreshShareSection),
				BuildDefaultRow(rows.Agent, "agent.row.agents", true, RefreshShareSection),
			]);
		}

		private AgentVisibilityRowViewModel BuildDefaultRow(AgentReadRow row, string headerKey, bool isShare, Action refresh)
		{
			var rowRef = row;
			var identityCell = BuildIdentityCell(
				() => rowRef.Identity,
				identity => { rowRef.Identity = identity; refresh(); },
				() => { rowRef.Identity = MessageAuthorIdentity.Default; refresh(); },
				canOverride: false,
				isOverridden: false,
				resolvedDefault: ResolveDefaultIdentity(isShare ? _agent.Info.IdentifyAsUser : false, isShare, headerKey));

			var result = new AgentVisibilityRowViewModel(Locale.Get("settings." + headerKey), false, false, false, null, identityCell);
			foreach (var cell in BuildFacetCells(
				isOn: facet => GetRowFacetValue(rowRef, facet),
				onToggle: (facet, value) => { SetRowFacetValue(rowRef, facet, value); refresh(); },
				onReset: _ => { },
				canOverride: false,
				isOverridden: _ => false))
			{
				result.Cells.Add(cell);
			}
			return result;
		}

		#endregion

		#region Read agents (sugar)

		private void RefreshReadAgentRows()
		{
			var rows = new List<AgentVisibilityRowViewModel>();
			var readerIsUserLike = _agent.Info.IdentifyAsUser;
			var myReadFilters = _settings.GetEffectiveReadFilters(_chatSettings);

			foreach (var (descriptor, isGlobal) in _otherAgents)
			{
				var other = descriptor;
				var baseRows = other.Read.GetEffectiveDefaultShareFilters(_chatSettings);
				var baseRow = readerIsUserLike ? baseRows.User : baseRows.Agent;
				var gateRow = other.Info.IdentifyAsUser ? myReadFilters.User : myReadFilters.Agent;
				var dictionary = other.Read.ParticipantsShareFilters;
				var key = _agent.Id;

				AgentReadOverrideRow? Override() => dictionary.TryGetValue(key, out var row) ? row : null;
				AgentReadOverrideRow EnsureOverride()
				{
					if (dictionary.TryGetValue(key, out var row))
						return row;
					row = new AgentReadOverrideRow();
					dictionary.Add(key, row);
					return row;
				}

				var resolvedIdentity = other.Info.IdentifyAsUser ? MessageAuthorIdentity.NamedUser : MessageAuthorIdentity.NamedAgent;

				var identityCell = BuildIdentityCell(
					() => Override()?.Identity ?? MessageAuthorIdentity.Default,
					identity => { EnsureOverride().Identity = identity; RefreshReadAgentRows(); },
					() => { var ov = Override(); if (ov != null) ov.Identity = MessageAuthorIdentity.Default; RefreshReadAgentRows(); },
					canOverride: true,
					isOverridden: (Override() is { } ov && ov.Identity is not MessageAuthorIdentity.Default),
					resolvedDefault: resolvedIdentity);

				var result = new AgentVisibilityRowViewModel(
					descriptor.Info.Name ?? descriptor.Id.ToString()[..8],
					isGlobal, showRowReset: true,
					hasOverrides: Override() != null,
					resetRowCommand: new RelayCommand(() => { dictionary.Remove(key); RefreshReadAgentRows(); }),
					identityCell);

				foreach (var cell in BuildFacetCells(
					isOn: facet => GetReadCellValue(Override(), baseRow, gateRow, facet),
					onToggle: (facet, value) => { SetOverrideFacetValue(EnsureOverride(), facet, value); RefreshReadAgentRows(); },
					onReset: facet => { ResetOverrideFacet(Override(), facet); RefreshReadAgentRows(); },
					canOverride: true,
					isOverridden: facet => IsOverrideFacetOverridden(Override(), facet),
					isBlocked: facet => !gateRow.Visible || !GetRowFacetValue(gateRow, facet)))
				{
					result.Cells.Add(cell);
				}

				rows.Add(result);
			}

			ReadAgentRows.Reset(rows);
			RaisePropertyChanged(nameof(HasReadAgentRows));
		}

		#endregion

		#region Share agents (my overrides)

		private void RefreshShareAgentRows()
		{
			var rows = new List<AgentVisibilityRowViewModel>();
			var myRows = _settings.GetEffectiveDefaultShareFilters(_chatSettings);
			var myResolvedIdentity = _agent.Info.IdentifyAsUser ? MessageAuthorIdentity.NamedUser : MessageAuthorIdentity.NamedAgent;

			foreach (var (descriptor, isGlobal) in _otherAgents)
			{
				var other = descriptor;
				var baseRow = other.Info.IdentifyAsUser ? myRows.User : myRows.Agent;
				var dictionary = _settings.ParticipantsShareFilters;
				var key = other.Id;

				AgentReadOverrideRow? Override() => dictionary.TryGetValue(key, out var row) ? row : null;
				AgentReadOverrideRow EnsureOverride()
				{
					if (dictionary.TryGetValue(key, out var row))
						return row;
					row = new AgentReadOverrideRow();
					dictionary.Add(key, row);
					return row;
				}

				var identityCell = BuildIdentityCell(
					() => Override()?.Identity ?? MessageAuthorIdentity.Default,
					identity => { EnsureOverride().Identity = identity; RefreshShareAgentRows(); },
					() => { var ov = Override(); if (ov != null) ov.Identity = MessageAuthorIdentity.Default; RefreshShareAgentRows(); },
					canOverride: true,
					isOverridden: (Override() is { } ov && ov.Identity is not MessageAuthorIdentity.Default),
					resolvedDefault: myResolvedIdentity);

				var result = new AgentVisibilityRowViewModel(
					descriptor.Info.Name ?? descriptor.Id.ToString()[..8],
					isGlobal, showRowReset: true,
					hasOverrides: Override() != null,
					resetRowCommand: new RelayCommand(() => { dictionary.Remove(key); RefreshShareAgentRows(); }),
					identityCell);

				foreach (var cell in BuildFacetCells(
					isOn: facet => GetOverrideFacetValue(Override(), baseRow, facet),
					onToggle: (facet, value) => { SetOverrideFacetValue(EnsureOverride(), facet, value); RefreshShareAgentRows(); },
					onReset: facet => { ResetOverrideFacet(Override(), facet); RefreshShareAgentRows(); },
					canOverride: true,
					isOverridden: facet => IsOverrideFacetOverridden(Override(), facet)))
				{
					result.Cells.Add(cell);
				}

				rows.Add(result);
			}

			ShareAgentRows.Reset(rows);
			RaisePropertyChanged(nameof(HasShareAgentRows));
		}

		#endregion

		#region Cell building

		private static readonly AgentVisibilityFacet[] ToggledFacets =
		[
			AgentVisibilityFacet.Visible,
			AgentVisibilityFacet.MessagesWithToolCalls,
			AgentVisibilityFacet.MessagesWithoutToolCalls,
			AgentVisibilityFacet.BriefReasoning,
			AgentVisibilityFacet.Reasoning,
			AgentVisibilityFacet.Content,
			AgentVisibilityFacet.NativeAttachments,
			AgentVisibilityFacet.Attachments,
			AgentVisibilityFacet.ToolCallFacts,
			AgentVisibilityFacet.BriefToolCallArguments,
			AgentVisibilityFacet.BriefToolCallResults,
			AgentVisibilityFacet.ToolCallArguments,
			AgentVisibilityFacet.ToolCallResults,
			AgentVisibilityFacet.ToolCallNativeAttachments,
		];

		private IEnumerable<AgentVisibilityCellViewModel> BuildFacetCells(
			Func<AgentVisibilityFacet, bool> isOn,
			Action<AgentVisibilityFacet, bool> onToggle,
			Action<AgentVisibilityFacet> onReset,
			bool canOverride,
			Func<AgentVisibilityFacet, bool> isOverridden,
			Func<AgentVisibilityFacet, bool>? isBlocked = null)
		{
			foreach (var facet in ToggledFacets)
			{
				var value = isOn(facet);
				var overridden = canOverride && isOverridden(facet);
				var blocked = isBlocked?.Invoke(facet) ?? false;

				string stateText;
				if (blocked)
					stateText = Locale.Get("settings.agent.state.blocked");
				else if (canOverride)
					stateText = Locale.Get(overridden ? "settings.agent.state.overridden_prefix" : "settings.agent.state.inherited_prefix")
				+ ": " + Locale.Get(value ? "settings.agent.state.on" : "settings.agent.state.off");
				else
					stateText = Locale.Get(value ? "settings.agent.state.on" : "settings.agent.state.off");

				var stateColor = blocked ? Brushes.Gray : (value ? Brushes.Green : Brushes.Red);
				var groupStart = facet is AgentVisibilityFacet.MessagesWithToolCalls
					or AgentVisibilityFacet.BriefReasoning
					or AgentVisibilityFacet.Content
					or AgentVisibilityFacet.NativeAttachments
					or AgentVisibilityFacet.ToolCallFacts;

				yield return new AgentVisibilityCellViewModel(facet, value, overridden, canOverride, groupStart, blocked,
					stateText, stateColor,
					newValue => onToggle(facet, newValue),
					() => onReset(facet));
			}
		}

		private static AgentIdentityCellViewModel BuildIdentityCell(
			Func<MessageAuthorIdentity> getIdentity,
			Action<MessageAuthorIdentity> setIdentity,
			Action resetIdentity,
			bool canOverride,
			bool isOverridden,
			MessageAuthorIdentity resolvedDefault)
		{
			var identity = getIdentity();
			var isAuto = identity is MessageAuthorIdentity.Default;
			var identityName = AgentVisibilityFacetInfo.GetIdentityName(isAuto ? resolvedDefault : identity).Value;

			string stateText;
			if (isOverridden)
				stateText = Locale.Get("settings.agent.state.overridden_prefix") + ": " + identityName;
			else if (isAuto)
				stateText = Locale.Get("settings.agent.state.auto_prefix") + ": " + identityName;
			else
				stateText = identityName;

			var stateColor = isOverridden ? Brushes.Green : (canOverride ? Brushes.Gray : Brushes.Green);
			return new AgentIdentityCellViewModel(identity, isOverridden, canOverride, stateText, stateColor, setIdentity, resetIdentity);
		}

		private static MessageAuthorIdentity ResolveDefaultIdentity(bool isUserLike, bool isShare, string headerKey)
			=> !isShare ? (headerKey == "agent.row.users" ? MessageAuthorIdentity.NamedUser : MessageAuthorIdentity.NamedAgent)
				: (isUserLike ? MessageAuthorIdentity.NamedUser : MessageAuthorIdentity.NamedAgent);

		#endregion

		#region Facet values on plain rows

		private static bool GetRowFacetValue(AgentReadRow row, AgentVisibilityFacet facet) => facet switch
		{
			AgentVisibilityFacet.Visible => row.Visible,
			AgentVisibilityFacet.MessagesWithToolCalls => row.VisibleMessages.HasFlag(MessageVisibilityFacet.MessagesWithToolCalls),
			AgentVisibilityFacet.MessagesWithoutToolCalls => row.VisibleMessages.HasFlag(MessageVisibilityFacet.MessagesWithoutToolCalls),
			AgentVisibilityFacet.BriefReasoning => row.VisibleParts.HasFlag(MessagePartsFacet.BriefReasoning),
			AgentVisibilityFacet.Reasoning => row.VisibleParts.HasFlag(MessagePartsFacet.Reasoning),
			AgentVisibilityFacet.Content => row.VisibleParts.HasFlag(MessagePartsFacet.Content),
			AgentVisibilityFacet.NativeAttachments => row.VisibleParts.HasFlag(MessagePartsFacet.NativeAttachments),
			AgentVisibilityFacet.Attachments => row.VisibleParts.HasFlag(MessagePartsFacet.Attachments),
			AgentVisibilityFacet.ToolCallFacts => row.VisibleParts.HasFlag(MessagePartsFacet.ToolCallFacts),
			AgentVisibilityFacet.BriefToolCallArguments => row.VisibleParts.HasFlag(MessagePartsFacet.BriefToolCallArguments),
			AgentVisibilityFacet.BriefToolCallResults => row.VisibleParts.HasFlag(MessagePartsFacet.BriefToolCallResults),
			AgentVisibilityFacet.ToolCallArguments => row.VisibleParts.HasFlag(MessagePartsFacet.ToolCallArguments),
			AgentVisibilityFacet.ToolCallResults => row.VisibleParts.HasFlag(MessagePartsFacet.ToolCallResults),
			AgentVisibilityFacet.ToolCallNativeAttachments => row.VisibleParts.HasFlag(MessagePartsFacet.ToolCallNativeAttachments),
			_ => false,
		};

		private static void SetRowFacetValue(AgentReadRow row, AgentVisibilityFacet facet, bool value)
		{
			switch (facet)
			{
				case AgentVisibilityFacet.Visible:
					row.Visible = value;
					break;
				case AgentVisibilityFacet.MessagesWithToolCalls:
					row.VisibleMessages = SetFlag(row.VisibleMessages, MessageVisibilityFacet.MessagesWithToolCalls, value);
					break;
				case AgentVisibilityFacet.MessagesWithoutToolCalls:
					row.VisibleMessages = SetFlag(row.VisibleMessages, MessageVisibilityFacet.MessagesWithoutToolCalls, value);
					break;
				default:
					var part = GetPartsBit(facet);
					row.VisibleParts = CascadeParts(row.VisibleParts, part, value);
					break;
			}
		}

		#endregion

		#region Facet values on override rows

		/// <summary>
		/// Gets the value displayed in a read-sugar cell: the read restriction gates everything the agent reads.
		/// </summary>
		private static bool GetReadCellValue(AgentReadOverrideRow? row, AgentReadRow baseRow, AgentReadRow gateRow, AgentVisibilityFacet facet)
			=> gateRow.Visible && GetRowFacetValue(gateRow, facet) && GetOverrideFacetValue(row, baseRow, facet);

		private static bool GetOverrideFacetValue(AgentReadOverrideRow? row, AgentReadRow baseRow, AgentVisibilityFacet facet)
		{
			if (row == null)
				return GetRowFacetValue(baseRow, facet);

			return facet switch
			{
				AgentVisibilityFacet.Visible => row.OverrideVisible ? row.Visible : baseRow.Visible,
				AgentVisibilityFacet.MessagesWithToolCalls => GetOverrideFlag(row, baseRow, facet),
				AgentVisibilityFacet.MessagesWithoutToolCalls => GetOverrideFlag(row, baseRow, facet),
				_ => GetOverridePartsFlag(row, baseRow, facet),
			};
		}

		private static bool GetOverrideFlag(AgentReadOverrideRow row, AgentReadRow baseRow, AgentVisibilityFacet facet)
		{
			var mask = facet switch
			{
				AgentVisibilityFacet.MessagesWithToolCalls => MessageVisibilityFacet.MessagesWithToolCalls,
				AgentVisibilityFacet.MessagesWithoutToolCalls => MessageVisibilityFacet.MessagesWithoutToolCalls,
				_ => MessageVisibilityFacet.Unknown,
			};
			return row.OverridenVisibleMessages.HasFlag(mask)
				? row.VisibleMessages.HasFlag(mask)
				: baseRow.VisibleMessages.HasFlag(mask);
		}

		private static bool GetOverridePartsFlag(AgentReadOverrideRow row, AgentReadRow baseRow, AgentVisibilityFacet facet)
		{
			var part = GetPartsBit(facet);
			return row.OverridenVisibleParts.HasFlag(part)
				? row.VisibleParts.HasFlag(part)
				: baseRow.VisibleParts.HasFlag(part);
		}

		private static void SetOverrideFacetValue(AgentReadOverrideRow row, AgentVisibilityFacet facet, bool value)
		{
			switch (facet)
			{
				case AgentVisibilityFacet.Visible:
					row.OverrideVisible = true;
					row.Visible = value;
					break;
				case AgentVisibilityFacet.MessagesWithToolCalls:
					row.OverridenVisibleMessages |= MessageVisibilityFacet.MessagesWithToolCalls;
					row.VisibleMessages = SetFlag(row.VisibleMessages, MessageVisibilityFacet.MessagesWithToolCalls, value);
					break;
				case AgentVisibilityFacet.MessagesWithoutToolCalls:
					row.OverridenVisibleMessages |= MessageVisibilityFacet.MessagesWithoutToolCalls;
					row.VisibleMessages = SetFlag(row.VisibleMessages, MessageVisibilityFacet.MessagesWithoutToolCalls, value);
					break;
				default:
					var part = GetPartsBit(facet);
					row.OverridenVisibleParts |= GetAffectedParts(part, value);
					row.VisibleParts = CascadeParts(row.VisibleParts, part, value);
					break;
			}
		}

		private static void ResetOverrideFacet(AgentReadOverrideRow? row, AgentVisibilityFacet facet)
		{
			if (row == null)
				return;

			switch (facet)
			{
				case AgentVisibilityFacet.Visible:
					row.OverrideVisible = false;
					break;
				case AgentVisibilityFacet.MessagesWithToolCalls:
					row.OverridenVisibleMessages &= ~MessageVisibilityFacet.MessagesWithToolCalls;
					break;
				case AgentVisibilityFacet.MessagesWithoutToolCalls:
					row.OverridenVisibleMessages &= ~MessageVisibilityFacet.MessagesWithoutToolCalls;
					break;
				default:
					row.OverridenVisibleParts &= ~GetPartsBit(facet);
					break;
			}
		}

		private static bool IsOverrideFacetOverridden(AgentReadOverrideRow? row, AgentVisibilityFacet facet)
		{
			if (row == null)
				return false;

			return facet switch
			{
				AgentVisibilityFacet.Visible => row.OverrideVisible,
				AgentVisibilityFacet.MessagesWithToolCalls => row.OverridenVisibleMessages.HasFlag(MessageVisibilityFacet.MessagesWithToolCalls),
				AgentVisibilityFacet.MessagesWithoutToolCalls => row.OverridenVisibleMessages.HasFlag(MessageVisibilityFacet.MessagesWithoutToolCalls),
				_ => row.OverridenVisibleParts.HasFlag(GetPartsBit(facet)),
			};
		}

		#endregion

		#region Helpers

		private static MessageVisibilityFacet SetFlag(MessageVisibilityFacet value, MessageVisibilityFacet flag, bool set)
			=> set ? value | flag : value & ~flag;

		private static MessagePartsFacet GetPartsBit(AgentVisibilityFacet facet) => facet switch
		{
			AgentVisibilityFacet.BriefReasoning => MessagePartsFacet.BriefReasoning,
			AgentVisibilityFacet.Reasoning => MessagePartsFacet.Reasoning,
			AgentVisibilityFacet.Content => MessagePartsFacet.Content,
			AgentVisibilityFacet.NativeAttachments => MessagePartsFacet.NativeAttachments,
			AgentVisibilityFacet.Attachments => MessagePartsFacet.Attachments,
			AgentVisibilityFacet.ToolCallFacts => MessagePartsFacet.ToolCallFacts,
			AgentVisibilityFacet.BriefToolCallArguments => MessagePartsFacet.BriefToolCallArguments,
			AgentVisibilityFacet.BriefToolCallResults => MessagePartsFacet.BriefToolCallResults,
			AgentVisibilityFacet.ToolCallArguments => MessagePartsFacet.ToolCallArguments,
			AgentVisibilityFacet.ToolCallResults => MessagePartsFacet.ToolCallResults,
			AgentVisibilityFacet.ToolCallNativeAttachments => MessagePartsFacet.ToolCallNativeAttachments,
			_ => MessagePartsFacet.None,
		};

		/// <summary>
		/// Gets the full set of <see cref="MessagePartsFacet"/> bits enabled together with <paramref name="bit"/>:
		/// every tool-call facet implies tool call facts, full tool arguments/results additionally imply their
		/// brief counterparts, and full reasoning implies brief reasoning.
		/// </summary>
		private static MessagePartsFacet GetEnabledWith(MessagePartsFacet bit) => bit switch
		{
			MessagePartsFacet.Reasoning => MessagePartsFacet.Reasoning | MessagePartsFacet.BriefReasoning,
			MessagePartsFacet.BriefToolCallArguments => MessagePartsFacet.BriefToolCallArguments | MessagePartsFacet.ToolCallFacts,
			MessagePartsFacet.BriefToolCallResults => MessagePartsFacet.BriefToolCallResults | MessagePartsFacet.ToolCallFacts,
			MessagePartsFacet.ToolCallArguments => MessagePartsFacet.ToolCallArguments | MessagePartsFacet.BriefToolCallArguments | MessagePartsFacet.ToolCallFacts,
			MessagePartsFacet.ToolCallResults => MessagePartsFacet.ToolCallResults | MessagePartsFacet.BriefToolCallResults | MessagePartsFacet.ToolCallFacts,
			MessagePartsFacet.ToolCallNativeAttachments => MessagePartsFacet.ToolCallNativeAttachments | MessagePartsFacet.ToolCallFacts,
			_ => bit,
		};

		/// <summary>
		/// Gets the full set of <see cref="MessagePartsFacet"/> bits disabled together with <paramref name="bit"/> —
		/// the contrapositive of <see cref="GetEnabledWith"/>: turning a flag off turns off everything that implies it.
		/// </summary>
		private static MessagePartsFacet GetDisabledWith(MessagePartsFacet bit) => bit switch
		{
			MessagePartsFacet.BriefReasoning => MessagePartsFacet.BriefReasoning | MessagePartsFacet.Reasoning,
			MessagePartsFacet.BriefToolCallArguments => MessagePartsFacet.BriefToolCallArguments | MessagePartsFacet.ToolCallArguments,
			MessagePartsFacet.BriefToolCallResults => MessagePartsFacet.BriefToolCallResults | MessagePartsFacet.ToolCallResults,
			MessagePartsFacet.ToolCallFacts => MessagePartsFacet.ToolCallFacts | MessagePartsFacet.BriefToolCallArguments
				| MessagePartsFacet.BriefToolCallResults | MessagePartsFacet.ToolCallArguments | MessagePartsFacet.ToolCallResults
				| MessagePartsFacet.ToolCallNativeAttachments,
			_ => bit,
		};

		/// <summary>
		/// Gets the full set of bits affected by toggling <paramref name="bit"/> (applied both to the value and to the override mask).
		/// </summary>
		private static MessagePartsFacet GetAffectedParts(MessagePartsFacet bit, bool value)
			=> value ? GetEnabledWith(bit) : GetDisabledWith(bit);

		/// <summary>
		/// Applies a parts facet toggle with the implication cascade:
		/// enabling a flag also enables everything it implies, disabling a flag disables everything that implies it.
		/// </summary>
		private static MessagePartsFacet CascadeParts(MessagePartsFacet current, MessagePartsFacet bit, bool value)
			=> value ? current | GetEnabledWith(bit) : current & ~GetDisabledWith(bit);

		#endregion

		/// <inheritdoc/>
		protected override void Dispose(bool disposing)
		{
			base.Dispose(disposing);

			if (disposing)
			{
				foreach (var subscription in _subscriptions)
					subscription.Dispose();
				_subscriptions.Clear();
			}
		}
	}
}
