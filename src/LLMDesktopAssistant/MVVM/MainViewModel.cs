using LLMDesktopAssistant.Agents.Tasks;
using LLMDesktopAssistant.Agents.Tasks.MVVM;
using LLMDesktopAssistant.Help;
using LLMDesktopAssistant.LLM.MVVM;
using LLMDesktopAssistant.LLM.Services;
using LLMDesktopAssistant.Localization;
using LLMDesktopAssistant.MCP;
using LLMDesktopAssistant.MVVM.Debug;
using LLMDesktopAssistant.Prompting;
using LLMDesktopAssistant.Services;
using LLMDesktopAssistant.Settings.Application;
using LLMDesktopAssistant.Settings.Application.MVVM;
using LLMDesktopAssistant.Utils;
using Material.Icons;

namespace LLMDesktopAssistant.MVVM
{
	public class MainViewModelSidebarItemViewModel : NotifyPropertyChanged
	{
		public required VisualIconKind Icon { get; init; }

		public required LocaleKeyBase Title { get; init; }

		public required object? Content { get; init; }

		private bool _isSelected;
		public bool IsSelected
		{
			get => _isSelected;
			set => SetProperty(ref _isSelected, value);
		}
	}

	public class MainViewModel : ViewModelBase
	{
		public RangeObservableCollection<MainViewModelSidebarItemViewModel> TopSidebarItems { get; }

		public RangeObservableCollection<MainViewModelSidebarItemViewModel> BottomSidebarItems { get; }

		private MainViewModelSidebarItemViewModel? _selectedTopSidebarItem;
		/// <summary>
		/// Gets or sets the currently selected top sidebar item. Selecting an item clears the bottom selection.
		/// </summary>
		public MainViewModelSidebarItemViewModel? SelectedTopSidebarItem
		{
			get => _selectedTopSidebarItem;
			set
			{
				if (SetProperty(ref _selectedTopSidebarItem, value))
				{
					if (value is not null)
					{
						SelectedBottomSidebarItem = null;
						SelectSidebarItem(value);
					}
				}
			}
		}

		private MainViewModelSidebarItemViewModel? _selectedBottomSidebarItem;
		/// <summary>
		/// Gets or sets the currently selected bottom sidebar item. Selecting an item clears the top selection.
		/// </summary>
		public MainViewModelSidebarItemViewModel? SelectedBottomSidebarItem
		{
			get => _selectedBottomSidebarItem;
			set
			{
				if (SetProperty(ref _selectedBottomSidebarItem, value))
				{
					if (value is not null)
					{
						SelectedTopSidebarItem = null;
						SelectSidebarItem(value);
					}
				}
			}
		}

		private MainViewModelSidebarItemViewModel? _selectedSidebarItem;
		/// <summary>
		/// Gets the currently selected sidebar item.
		/// </summary>
		public MainViewModelSidebarItemViewModel? SelectedSidebarItem => _selectedSidebarItem;

		private void SelectSidebarItem(MainViewModelSidebarItemViewModel item)
		{
			var prev = _selectedSidebarItem;
			if (SetProperty(ref _selectedSidebarItem, item))
			{
				prev?.IsSelected = false;
				item.IsSelected = true;
			}
		}

		public ChatManagerViewModel ChatManager { get; }
		public MCPManagerViewModel MCPManager { get; }
		public AgentTaskDispatcherViewModel AgentTaskDispatcher { get; }
		public HelpViewModel Help { get; }
		public ApplicationSettingsViewModel ApplicationSettings { get; }

#if DEBUG
		public DebugPagesViewModel Debug { get; }
#endif

		public MainViewModel()
		{
			TopSidebarItems = [];
			BottomSidebarItems = [];

			ChatManager = new ChatManagerViewModel(ChatServices.ManagementService);
			TopSidebarItems.Add(new MainViewModelSidebarItemViewModel
			{
				Icon = MaterialIconKind.Message,
				Title = Locale.GetKey("tab.title.chat"),
				Content = ChatManager
			});

			MCPManager = new MCPManagerViewModel();
			TopSidebarItems.Add(new MainViewModelSidebarItemViewModel
			{
				Icon = MaterialIconKind.Connection,
				Title = Locale.GetKey("tab.title.mcp_manager"),
				Content = MCPManager
			});

			AgentTaskDispatcher = new AgentTaskDispatcherViewModel(
				ServiceRegistry.Provider.GetRequiredService<IAgentTaskDispatcher>());
			TopSidebarItems.Add(new MainViewModelSidebarItemViewModel
			{
				Icon = MaterialIconKind.TimerSandComplete,
				Title = Locale.GetKey("tab.title.agent_tasks"),
				Content = AgentTaskDispatcher
			});

			Help = new HelpViewModel(ServiceRegistry.Provider.GetRequiredService<HelpDocumentStore>());
			BottomSidebarItems.Add(new MainViewModelSidebarItemViewModel
			{
				Icon = MaterialIconKind.BookOpen,
				Title = Locale.GetKey("tab.title.help"),
				Content = Help
			});

			ApplicationSettings = new ApplicationSettingsViewModel();
			BottomSidebarItems.Add(new MainViewModelSidebarItemViewModel
			{
				Icon = MaterialIconKind.Cog,
				Title = Locale.GetKey("tab.title.settings"),
				Content = ApplicationSettings
			});

#if DEBUG
			Debug = new DebugPagesViewModel();
			BottomSidebarItems.Add(new MainViewModelSidebarItemViewModel
			{
				Icon = MaterialIconKind.Bug,
				Title = Locale.GetKey("tab.title.debug"),
				Content = Debug
			});
#endif

			SelectedTopSidebarItem = TopSidebarItems[0];
		}
	}
}
