using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using LLMDesktopAssistant.Addons;
using LLMDesktopAssistant.Agents;
using LLMDesktopAssistant.Agents.Settings;
using LLMDesktopAssistant.LLM.Services.Prompting;
using LLMDesktopAssistant.LLM.Settings;
using LLMDesktopAssistant.Localization;
using LLMDesktopAssistant.MVVM;
using LLMDesktopAssistant.Prompting;
using LLMDesktopAssistant.Prompting.Context;

namespace LLMDesktopAssistant.LLM.MVVM.Settings.Agents
{
	public class CheckpointKindItem : ObservableObject
	{
		private readonly AgentContextSettingsViewModel _parent;
		public ContextCheckpointKind Kind { get; }
		public LocaleKeyBase Name { get; }
		public LocaleKeyBase Description { get; }

		private bool _isEnabled;
		public bool IsEnabled
		{
			get => _isEnabled;
			set
			{
				if (SetProperty(ref _isEnabled, value))
					_parent.SetCheckpointKindEnabled(Kind, value);
			}
		}

		public CheckpointKindItem(AgentContextSettingsViewModel parent, ContextCheckpointKind kind,
			LocaleKeyBase name, LocaleKeyBase description, bool isEnabled)
		{
			_parent = parent;
			Kind = kind;
			Name = name;
			Description = description;
			_isEnabled = isEnabled;
		}
	}

	[ViewModelFor(typeof(AgentContextSettingsView))]
	public class AgentContextSettingsViewModel : ViewModelBase
	{
		private readonly ChatSettings _chatSettings;
		private readonly ChatAgentDescriptor _agent;
		private readonly IAddonSetCollector<PromptContextInfo> _promptContextCollector;

		public AgentContextSettings Settings { get; }

		public AgentContextSettingsViewModel(AgentContextSettings settings, ChatSettings chatSettings,
			ChatAgentDescriptor agent, IAddonSetCollector<PromptContextInfo> promptContextCollector)
		{
			Settings = settings;
			_chatSettings = chatSettings;
			_agent = agent;
			_promptContextCollector = promptContextCollector;

			RefreshSnapshotCommand = new RelayCommand(RefreshSnapshot);

			_selectedMaxVisibleRoundsInheritance = InheritanceLevelItem.AllAgent.First(i => i.Value == settings.MaxVisibleRoundsInheritance);
			_selectedDisabledFlagsInheritance = InheritanceLevelItem.AllAgent.First(i => i.Value == settings.DisabledFlagsInheritance);

			settings.PropertyChanged += Settings_PropertyChanged;

			InitializeCheckpointKinds();
		}

		
		private InheritanceLevelItem _selectedMaxVisibleRoundsInheritance;
		public InheritanceLevelItem SelectedMaxVisibleRoundsInheritance
		{
			get => _selectedMaxVisibleRoundsInheritance;
			set
			{
				if (SetProperty(ref _selectedMaxVisibleRoundsInheritance, value) && value != null)
					Settings.MaxVisibleRoundsInheritance = value.Value;
			}
		}

		private InheritanceLevelItem _selectedDisabledFlagsInheritance;
		public InheritanceLevelItem SelectedDisabledFlagsInheritance
		{
			get => _selectedDisabledFlagsInheritance;
			set
			{
				if (SetProperty(ref _selectedDisabledFlagsInheritance, value) && value != null)
					Settings.DisabledFlagsInheritance = value.Value;
			}
		}

		public int PromptModeIndex
		{
			get => (int)Settings.PromptMode;
			set
			{
				if (value < 0)
					return;
				Settings.PromptMode = (PromptContextMode)value;
				RaisePropertyChanged();
				RaisePropertyChanged(nameof(IsStaticMode));
			}
		}

		public bool IsHybridMode => PromptModeIndex == (int)PromptContextMode.Hybrid;
		public bool IsDynamicMode => PromptModeIndex == (int)PromptContextMode.Dynamic;
		public bool IsStaticMode => PromptModeIndex == (int)PromptContextMode.Static;

		/// <summary>
		/// Refreshes the static system prompt snapshot of the agent.
		/// </summary>
		public ICommand RefreshSnapshotCommand { get; }

		private void RefreshSnapshot()
		{
			Settings.Snapshot = _promptContextCollector.GetAddonsForAgent(_agent).Select(c => c.Provider).Anchored().RenderHeader(_agent);
		}

		public int MaxVisibleRounds
		{
			get => Settings.GetEffectiveMaxVisibleRounds(_chatSettings);
			set
			{
				Settings.SetEffectiveMaxVisibleRounds(_chatSettings, value);
				RaisePropertyChanged();
			}
		}

		public ObservableCollection<CheckpointKindItem> CheckpointKindItems { get; } = [];

		private void InitializeCheckpointKinds()
		{
			CheckpointKindItems.Clear();
			var disabled = Settings.GetEffectiveDisabledFlags(_chatSettings);

			CheckpointKindItems.Add(new CheckpointKindItem(this, ContextCheckpointKind.Shield,
				Locale.GetKey("settings.agent.checkpoint.shield"),
				Locale.GetKey("settings.agent.checkpoint.shield.hint"),
				!disabled.HasFlag(ContextCheckpointKind.Shield)));

			CheckpointKindItems.Add(new CheckpointKindItem(this, ContextCheckpointKind.Summary,
				Locale.GetKey("settings.agent.checkpoint.summary"),
				Locale.GetKey("settings.agent.checkpoint.summary.hint"),
				!disabled.HasFlag(ContextCheckpointKind.Summary)));

			CheckpointKindItems.Add(new CheckpointKindItem(this, ContextCheckpointKind.ToolCompaction,
				Locale.GetKey("settings.agent.checkpoint.tool_compaction"),
				Locale.GetKey("settings.agent.checkpoint.tool_compaction.hint"),
				!disabled.HasFlag(ContextCheckpointKind.ToolCompaction)));

			CheckpointKindItems.Add(new CheckpointKindItem(this, ContextCheckpointKind.ForcedToolCompaction,
				Locale.GetKey("settings.agent.checkpoint.forced_tool_compaction"),
				Locale.GetKey("settings.agent.checkpoint.forced_tool_compaction.hint"),
				!disabled.HasFlag(ContextCheckpointKind.ForcedToolCompaction)));

			CheckpointKindItems.Add(new CheckpointKindItem(this, ContextCheckpointKind.ReasoningCompaction,
				Locale.GetKey("settings.agent.checkpoint.reasoning_compaction"),
				Locale.GetKey("settings.agent.checkpoint.reasoning_compaction.hint"),
				!disabled.HasFlag(ContextCheckpointKind.ReasoningCompaction)));
		}

		internal void SetCheckpointKindEnabled(ContextCheckpointKind kind, bool enabled)
		{
			var disabled = Settings.GetEffectiveDisabledFlags(_chatSettings);
			disabled = enabled ? (disabled & ~kind) : (disabled | kind);
			Settings.SetEffectiveDisabledFlags(_chatSettings, disabled);
		}

		private void Settings_PropertyChanged(object? sender, PropertyChangedEventArgs e)
		{
			switch (e.PropertyName)
			{
				case nameof(AgentContextSettings.PromptMode):
					RaisePropertyChanged(nameof(PromptModeIndex));
					RaisePropertyChanged(nameof(IsHybridMode));
					RaisePropertyChanged(nameof(IsDynamicMode));
					RaisePropertyChanged(nameof(IsStaticMode));
					break;

				case nameof(AgentContextSettings.MaxVisibleRoundsInheritance):
					_selectedMaxVisibleRoundsInheritance = InheritanceLevelItem.AllAgent.First(i => i.Value == Settings.MaxVisibleRoundsInheritance);
					RaisePropertyChanged(nameof(SelectedMaxVisibleRoundsInheritance));
					RaisePropertyChanged(nameof(MaxVisibleRounds));
					break;

				case nameof(AgentContextSettings.DisabledFlagsInheritance):
					_selectedDisabledFlagsInheritance = InheritanceLevelItem.AllAgent.First(i => i.Value == Settings.DisabledFlagsInheritance);
					RaisePropertyChanged(nameof(SelectedDisabledFlagsInheritance));
					InitializeCheckpointKinds();
					break;
			}
		}

		protected override void Dispose(bool disposing)
		{
			base.Dispose(disposing);

			if (disposing)
				Settings.PropertyChanged -= Settings_PropertyChanged;
		}
	}
}
