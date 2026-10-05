using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using CommunityToolkit.Mvvm.Input;
using LLMDesktopAssistant.Addons.MVVM;
using LLMDesktopAssistant.Localization;
using LLMDesktopAssistant.Utils;
using Material.Icons;

namespace LLMDesktopAssistant.MVVM.Debug;

/// <summary>
/// View model for the addon cards debug page: builds a set of <see cref="AddonCardViewModel"/>
/// instances that cover every element kind, chip shape and override state of the unified addon card.
/// </summary>
[ViewModelFor(typeof(AddonCardsDebugPageView))]
public class AddonCardsDebugPageViewModel : ViewModelBase
{
	private const string DemoPath = "~/.dass/skills/demo-skill.skill";

	/// <summary>
	/// Gets the set of the demo cards.
	/// </summary>
	public RangeObservableCollection<AddonCardViewModel> Cards { get; } = [];

	/// <summary>
	/// Gets the command that resets overrides of every demo card.
	/// </summary>
	public IRelayCommand ResetAllCommand { get; }

	/// <summary>
	/// Gets the command executed by the demo tag chips.
	/// </summary>
	public IRelayCommand<string> TagClickCommand { get; }

	private string _status = Locale.Get("debug.addon_cards.hint");

	/// <summary>
	/// Gets or sets the text describing the last performed interaction.
	/// </summary>
	public string Status
	{
		get => _status;
		set => SetProperty(ref _status, value);
	}

	/// <summary>
	/// Initializes a new instance of the <see cref="AddonCardsDebugPageViewModel"/> class.
	/// </summary>
	public AddonCardsDebugPageViewModel()
	{
		ResetAllCommand = new RelayCommand(ResetAll);
		TagClickCommand = new RelayCommand<string>(tag => Report($"Tag clicked: {tag}"));

		Cards.Add(BuildEverythingCard());
		Cards.Add(BuildChatLevelCard());
		Cards.Add(BuildAgentLevelCard());
		Cards.Add(BuildChipZooCard());
		Cards.Add(BuildKitchenSinkCard());
		Cards.Add(BuildMinimalCard());
		Cards.Add(BuildSubtitleCard());
	}

	private void ResetAll()
	{
		foreach (var card in Cards)
			card.ResetCommand?.Execute(null);

		Report("All overrides reset.");
	}

	private void Report(string message) => Status = message;

	private IRelayCommand Action(string name) => new RelayCommand(() => Report($"Action invoked: {name}"));

	#region Cards

	/// <summary>
	/// A skill-like card: every element kind is present, both header slots are overridden.
	/// </summary>
	private AddonCardViewModel BuildEverythingCard()
	{
		var enabled = DemoAddonChange.Toggle(definitionValue: false, isShownLeft: true);
		enabled.Value = true;

		var injection = DemoAddonChange.Selector(["Auto", "Manual", "Never"], "Auto", isShownLeft: false);
		injection.Value = "Manual";

		var emojis = DemoAddonBlockChange.Toggle(definitionValue: true, order: 0);
		emojis.Title = T("enable_emojis");

		var resetBlock = DemoAddonBlockChange.Toggle(definitionValue: false, order: 1, visibility: AddonCardBlockVisibility.Collapsible);
		resetBlock.Title = T("Resettable block");
		resetBlock.ToggleIcon = MaterialIconKind.Restore;
		resetBlock.ToggleToolTip = T("Toggle the resettable block");

		var elements = new List<IAddonCardElement>
		{
			enabled,
			injection,

			new AddonCardChip { Order = 0, Icon = MaterialIconKind.Wrench, Label = T("3 tools"), ToolTip = T("Tools provided by this skill") },
			new AddonCardChip { Order = 1, Icon = MaterialIconKind.Tune, Label = T("Manual injection") },
			new AddonCardChip { Order = 2, HasBorder = false, Opacity = 0.5, Icon = MaterialIconKind.FolderOutline, Label = T("~/.dass/skills") },
			new AddonCardChip { Order = 3, Brush = Brushes.OrangeRed, Icon = MaterialIconKind.AlertCircle, Label = T("Bad parameter default"), ToolTip = T("The 'formatting' parameter has an invalid default value") },

			new AddonCardTagChip { Order = 10, Icon = MaterialIconKind.Tag, Label = T("code"), Command = TagClickCommand, CommandParameter = "code" },
			new AddonCardTagChip { Order = 11, Icon = MaterialIconKind.Tag, Label = T("formatting"), Command = TagClickCommand, CommandParameter = "formatting" },
			new AddonCardTagChip { Order = 12, Icon = MaterialIconKind.Tag, Label = T("obsolete"), Command = TagClickCommand, CommandParameter = "obsolete" },

			new AddonCardActionRowElement { Order = 0, Content = PathLabel(DemoPath) },

			new AddonCardBlock
			{
				Order = 0,
				Title = T("Parameters"),
				Visibility = AddonCardBlockVisibility.Collapsible,
				ToggleIcon = MaterialIconKind.Tune,
				ToggleToolTip = T("Show parameters"),
				Content = MonoText("{\n  \"formatting\": \"bullet points\",\n  \"max_repetitions\": 3,\n  \"enable_emojis\": true\n}")
			},
			new AddonCardBlock { Order = 10, Title = T("Body"), Visibility = AddonCardBlockVisibility.Details, Content = MonoText("@template demo_skill\n{\n\t@metadata { ... }\n\tYou are a helpful assistant that answers in bullet points.\n}") },
			new AddonCardBlock
			{
				Order = 11,
				Title = T("Metadata"),
				Visibility = AddonCardBlockVisibility.Details,
				Chips =
				[
					new AddonCardChip { Icon = MaterialIconKind.Account, Label = T("author: dass") },
					new AddonCardChip { Icon = MaterialIconKind.Calendar, Label = T("2026-09-14") }
				]
			},

			emojis,
			resetBlock,

			new AddonCardAction { Order = 0, Icon = MaterialIconKind.Pencil, ToolTip = T("Open file"), Command = Action("open file") },
			new AddonCardAction { Order = 1, Icon = MaterialIconKind.FolderOpen, ToolTip = T("Show in explorer"), Command = Action("show in explorer") },
			new AddonCardAction { Order = 2, Icon = MaterialIconKind.Delete, ToolTip = T("Delete file"), Command = Action("delete file") }
		};

		return new AddonCardViewModel(elements)
		{
			Icon = MaterialIconKind.Cards,
			Name = T("demo-skill"),
			Subtitle = T("Demo skill (everything on)"),
			Description = T("Every element kind: overridden header slots and blocks with reset markers, colored chips, tags, collapsible parameters, details and file actions.")
		};
	}

	/// <summary>
	/// A chat-level card: no override slots at all, so no reset button ever appears.
	/// </summary>
	private AddonCardViewModel BuildChatLevelCard()
	{
		var elements = new List<IAddonCardElement>
		{
			new AddonCardChip { Order = 0, Icon = MaterialIconKind.Wrench, Label = T("1 toolset") },
			new AddonCardChip { Order = 1, Icon = MaterialIconKind.Database, Label = T("2 memory blocks") },
			new AddonCardChip { Order = 2, HasBorder = false, Opacity = 0.5, Icon = MaterialIconKind.FolderOutline, Label = T("~/.dass/skills") },
			new AddonCardTagChip { Order = 10, Icon = MaterialIconKind.Tag, Label = T("chat-level"), Command = TagClickCommand, CommandParameter = "chat-level" },

			new AddonCardActionRowElement { Order = 0, Content = PathLabel("~/.dass/skills/chat-skill.skill") },
			new AddonCardAction { Order = 0, Icon = MaterialIconKind.Pencil, ToolTip = T("Open file"), Command = Action("open (chat-level)") },

			new AddonCardBlock { Order = 0, Title = T("Body"), Visibility = AddonCardBlockVisibility.Details, Content = MonoText("A chat-level skill has a definition only, it cannot be overridden.") }
		};

		return new AddonCardViewModel(elements)
		{
			Icon = MaterialIconKind.Cards,
			Name = T("Chat-level skill"),
			Description = T("No IAddonCardChange elements: the reset button must not be shown even when the details are expanded.")
		};
	}

	/// <summary>
	/// An agent-level card whose override slots exist but hold definition values, so the reset button
	/// must stay hidden until a value is actually overridden.
	/// </summary>
	private AddonCardViewModel BuildAgentLevelCard()
	{
		var enabled = DemoAddonChange.Toggle(definitionValue: true, isShownLeft: true);
		var model = DemoAddonChange.Selector(["gpt-5", "claude-opus-4", "gemini-3-pro"], "gpt-5", isShownLeft: false);

		var elements = new List<IAddonCardElement>
		{
			enabled,
			model,
			new AddonCardChip { Order = 0, Icon = MaterialIconKind.Wrench, Label = T("5 tools") },
			new AddonCardChip { Order = 1, Icon = MaterialIconKind.Cards, Label = T("2 skills") },
			new AddonCardChip { Order = 2, Icon = MaterialIconKind.RobotHappy, Label = T("1 sub-agent") },

			new AddonCardActionRowElement { Order = 0, Content = PathLabel("~/.dass/agents/demo-agent.agent") },

			new AddonCardBlock
			{
				Order = 0,
				Title = T("Parameters"),
				Visibility = AddonCardBlockVisibility.Collapsible,
				ToggleIcon = MaterialIconKind.Tune,
				ToggleToolTip = T("Show parameters"),
				Content = MonoText("{ }")
			},
			new AddonCardBlock { Order = 10, Title = T("System prompt"), Visibility = AddonCardBlockVisibility.Details, Content = MonoText("You are a sub-agent responsible for reviewing code.") }
		};

		return new AddonCardViewModel(elements)
		{
			Icon = MaterialIconKind.RobotHappy,
			NamePrefix = T("Meta"),
			NamePrefixBrush = Brushes.MediumPurple,
			Name = T("Agent-level sub-agent"),
			Description = T("Override slots are present but untouched: no accent markers, the reset button appears only after you change something.")
		};
	}

	/// <summary>
	/// Every chip shape in a single card.
	/// </summary>
	private AddonCardViewModel BuildChipZooCard()
	{
		var elements = new List<IAddonCardElement>
		{
			new AddonCardChip { Order = 0, Icon = MaterialIconKind.Wrench, Label = T("icon + label") },
			new AddonCardChip { Order = 1, Icon = MaterialIconKind.Wrench, ToolTip = T("icon only") },
			new AddonCardChip { Order = 2, Label = T("label only") },
			new AddonCardChip { Order = 3, HasBorder = false, Label = T("no border") },
			new AddonCardChip { Order = 4, HasBorder = false, Opacity = 0.5, Icon = MaterialIconKind.FolderOutline, Label = T("no border + dimmed") },
			new AddonCardChip { Order = 5, Opacity = 0.5, Label = T("bordered + dimmed") },
			new AddonCardChip { Order = 6, Brush = Brushes.LimeGreen, Icon = MaterialIconKind.Check, Label = T("valid") },
			new AddonCardChip { Order = 7, Brush = Brushes.Orange, Icon = MaterialIconKind.AlertCircle, Label = T("warning") },
			new AddonCardChip { Order = 8, Brush = Brushes.OrangeRed, Icon = MaterialIconKind.Close, Label = T("error") },
			new AddonCardChip { Order = 9, Brush = Brushes.DeepSkyBlue, Icon = MaterialIconKind.Information, Label = T("info") },
			new AddonCardChip { Order = 10, Brush = Brushes.MediumPurple, Label = T("colored label only") },
			new AddonCardChip
			{
				Order = 11,
				Content = new TextBlock { Text = "3/5", FontWeight = FontWeight.Bold, FontSize = 11 },
				Label = T("compound content + label")
			},
			new AddonCardChip { Order = 12, Icon = MaterialIconKind.Database, Label = T("a chip with a deliberately very long label to check wrapping inside the row") },

			new AddonCardTagChip { Order = 20, Icon = MaterialIconKind.Tag, Label = T("clickable tag"), Command = TagClickCommand, CommandParameter = "clickable tag" },
			new AddonCardTagChip { Order = 21, Label = T("tag without icon"), Command = TagClickCommand, CommandParameter = "tag without icon" },
			new AddonCardTagChip { Order = 22, Icon = MaterialIconKind.Tag, Label = T("dimmed tag"), Opacity = 0.6, Command = TagClickCommand, CommandParameter = "dimmed tag" },
			new AddonCardTagChip { Order = 23, HasBorder = false, Icon = MaterialIconKind.Tag, Label = T("borderless tag"), Command = TagClickCommand, CommandParameter = "borderless tag" }
		};

		return new AddonCardViewModel(elements)
		{
			Icon = MaterialIconKind.Tag,
			Name = T("Chip zoo"),
			Description = T("All supported chip and tag shapes.")
		};
	}

	/// <summary>
	/// A card with lots of everything, to check layout under pressure.
	/// </summary>
	private AddonCardViewModel BuildKitchenSinkCard()
	{
		var left = DemoAddonChange.Toggle(definitionValue: false, isShownLeft: true, order: 0);
		left.Value = true;
		var rightFirst = DemoAddonChange.Selector(["Default", "Fast", "Thorough"], "Default", isShownLeft: false, order: 0);
		rightFirst.Value = "Thorough";
		var rightSecond = DemoAddonChange.Toggle(definitionValue: true, isShownLeft: false, order: 1);

		var elements = new List<IAddonCardElement> { left, rightFirst, rightSecond };

		for (var i = 0; i < 24; i++)
			elements.Add(new AddonCardChip { Order = 100 + i, Icon = MaterialIconKind.Tag, Label = T($"chip #{i + 1}") });

		elements.Add(new AddonCardActionRowElement { Order = 0, Content = PathLabel("~/.dass/skills/a-very-long-path/that/should/be/trimmed/gracefully/demo-skill.skill") });

		for (var i = 0; i < 3; i++)
		{
			elements.Add(new AddonCardBlock
			{
				Order = i,
				Title = T($"Collapsible block #{i + 1}"),
				Visibility = AddonCardBlockVisibility.Collapsible,
				ToggleIcon = i == 0 ? MaterialIconKind.Tune : i == 1 ? MaterialIconKind.CodeBraces : MaterialIconKind.Database,
				ToggleToolTip = T($"Toggle block #{i + 1}"),
				Content = MonoText($"// collapsible block #{i + 1}\n// each block has its own toggle button in the action row")
			});
		}

		for (var i = 0; i < 4; i++)
		{
			elements.Add(new AddonCardBlock
			{
				Order = 10 + i,
				Title = T($"Detail block #{i + 1}"),
				Visibility = AddonCardBlockVisibility.Details,
				Content = MonoText($"// detail block #{i + 1}"),
				Chips = i == 1
					?
					[
						new AddonCardChip { Icon = MaterialIconKind.Database, Label = T("memory: chat") },
						new AddonCardChip { Icon = MaterialIconKind.Database, Label = T("memory: project") }
					]
					: null
			});
		}

		elements.Add(new AddonCardBlock
		{
			Order = 0,
			Title = T("Inline block"),
			Visibility = AddonCardBlockVisibility.Inline,
			Content = MonoText("An inline block is always visible, right below the chips row."),
			Chips = [new AddonCardChip { Icon = MaterialIconKind.Check, Label = T("inline is always visible") }]
		});

		for (var i = 0; i < 5; i++)
			elements.Add(new AddonCardAction { Order = i, Icon = ActionIcons[i], ToolTip = T($"Action #{i + 1}"), Command = Action($"action #{i + 1}") });

		return new AddonCardViewModel(elements)
		{
			Icon = MaterialIconKind.Tag,
			Name = T("Kitchen sink"),
			Description = T("A long description that should wrap across multiple lines without breaking the card layout, followed by 24 chips that should wrap into several rows, three collapsible blocks, four detail blocks, one inline block and five action buttons.")
		};
	}

	/// <summary>
	/// A card with no elements at all and an empty description: nothing but the header must be rendered.
	/// </summary>
	private AddonCardViewModel BuildMinimalCard()
	{
		return new AddonCardViewModel([])
		{
			Icon = MaterialIconKind.Cards,
			Name = T("Minimal card")
		};
	}

	/// <summary>
	/// A card with a subtitle and a long name, to check trimming.
	/// </summary>
	private AddonCardViewModel BuildSubtitleCard()
	{
		var elements = new List<IAddonCardElement>
		{
			new AddonCardChip { Order = 0, Icon = MaterialIconKind.Information, Label = T("subtitle + trimming") }
		};

		return new AddonCardViewModel(elements)
		{
			Icon = MaterialIconKind.Tag,
			Name = T("A card with an extremely long display name that must be trimmed instead of pushing the header slots away"),
			Subtitle = T("very-long-addon-identifier.without-any-spaces.in-the-name"),
			Description = T("The subtitle is shown next to the name and is also trimmed.")
		};
	}

	#endregion

	#region Helpers

	private static readonly VisualIconKind[] ActionIcons =
	[
		MaterialIconKind.Pencil,
		MaterialIconKind.FolderOpen,
		MaterialIconKind.ContentCopy,
		MaterialIconKind.Eye,
		MaterialIconKind.Delete
	];

	private static LocaleKeyBase T(string text) => Locale.GetConstKey(text);

	private static TextBlock PathLabel(string path) => new()
	{
		Text = path,
		Opacity = 0.5,
		FontSize = 11,
		TextTrimming = TextTrimming.CharacterEllipsis,
		VerticalAlignment = VerticalAlignment.Center
	};

	private static TextBlock MonoText(string text) => new()
	{
		Text = text,
		FontFamily = new FontFamily("Cascadia Mono, Consolas, monospace"),
		FontSize = 12,
		Opacity = 0.85,
		TextWrapping = TextWrapping.Wrap
	};

	#endregion
}
