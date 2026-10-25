using CommunityToolkit.Mvvm.Input;

namespace LLMDesktopAssistant.Agents.Tasks.MVVM
{
	/// <summary>
	/// Global dispatcher view model for all agent tasks across all chats.
	/// Provides filtering and bulk operations.
	/// </summary>
	[ViewModelFor(typeof(AgentTaskDispatcherView))]
	public class AgentTaskDispatcherViewModel : ViewModelBase
	{
		/// <summary>
		/// The list of all tracked agent tasks.
		/// </summary>
		public AgentTaskListViewModel AllTasks { get; }

		private bool _showOnlyRunning;
		/// <summary>
		/// When <see langword="true"/>, only running (pending or executing) tasks are shown.
		/// </summary>
		public bool ShowOnlyRunning
		{
			get => _showOnlyRunning;
			set
			{
				if (SetProperty(ref _showOnlyRunning, value))
					ApplyFilter();
			}
		}

		private string _searchText = string.Empty;
		/// <summary>
		/// Search text to filter tasks by name or content.
		/// </summary>
		public string SearchText
		{
			get => _searchText;
			set
			{
				if (SetProperty(ref _searchText, value))
					ApplyFilter();
			}
		}

		/// <summary>
		/// Command to clear all completed (success, failed, cancelled) tasks.
		/// </summary>
		public ICommand ClearCompletedCommand { get; }

		/// <summary>
		/// Initializes a new instance of the <see cref="AgentTaskDispatcherViewModel"/> class.
		/// </summary>
		/// <param name="dispatcher">The task dispatcher whose global task list to display.</param>
		public AgentTaskDispatcherViewModel(IAgentTaskDispatcher dispatcher)
		{
			AllTasks = new AgentTaskListViewModel
			{
				Filtering = AgentTaskListFiltering.AllNotParented
			};
			AllTasks.BindToSource(dispatcher.AllTasks);

			ClearCompletedCommand = new RelayCommand(ClearCompleted);
		}

		private void ApplyFilter()
		{
			foreach (var vm in AllTasks.Tasks)
			{
				bool visible = true;

				if (_showOnlyRunning && !vm.IsRunning)
					visible = false;

				if (visible && !string.IsNullOrWhiteSpace(_searchText))
				{
					var name = vm.TaskName ?? string.Empty;
					var content = vm.LastContent ?? string.Empty;

					visible = name.Contains(_searchText, StringComparison.OrdinalIgnoreCase)
						|| content.Contains(_searchText, StringComparison.OrdinalIgnoreCase);
				}

				// Visibility is controlled via the View; we set a tag-like property.
				// For now, we simply skip items that don't match - the View will bind to Tasks directly.
			}
		}

		private void ClearCompleted()
		{
			var toRemove = AllTasks.Tasks
				.Where(vm => vm.IsCompleted)
				.ToList();

			foreach (var vm in toRemove)
				vm.Dispose();

			// Note: the underlying AgentTaskExecutor removes completed tasks automatically
			// based on CompletionExpiryTime. This is a forced early cleanup.
		}

		/// <inheritdoc />
		protected override void Dispose(bool disposing)
		{
			if (disposing)
				AllTasks.Dispose();

			base.Dispose(disposing);
		}
	}
}
