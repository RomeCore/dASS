using System.Runtime.CompilerServices;
using LLMDesktopAssistant.Services;
using LLMDesktopAssistant.Utils;
using Serilog;

namespace LLMDesktopAssistant.Agents.Tasks
{
	[Service(typeof(IAgentTaskDispatcher))]
	public class AgentTaskDispatcher : IAgentTaskDispatcher
	{
		private readonly RangeObservableCollection<AgentTask> _allTasks;
		// The cancellation tokens that are used to cancel expiration upon renewal.
		private readonly ConditionalWeakTable<AgentTask, CancellationTokenSource> _expirationCancellations = [];

		public ReadOnlyObservableCollection<AgentTask> AllTasks { get; }

		public AgentTaskDispatcher()
		{
			_allTasks = [];
			AllTasks = new ReadOnlyObservableCollection<AgentTask>(_allTasks);
		}

		public void OnBeginTask(AgentTask task)
		{
			// If task is added while not expired, cancel previous expiration.
			if (_expirationCancellations.TryGetValue(task, out var cts))
				cts.Cancel();

			if (!_allTasks.Contains(task))
			{
				_allTasks.Add(task);
				task.Parent?.SubTasks.Add(task);
				task.LaunchParameters.TriggeredChat?.AgentTasks.Add(task);
				task.LaunchParameters.TriggeredMessage?.AgentTasks.Add(task);
			}
		}

		public async void OnEndTask(AgentTask task)
		{
			try
			{
				if (task.LaunchParameters.CompletionExpiryTime != null)
				{
					if (task.LaunchParameters.CompletionExpiryTime.Value > TimeSpan.Zero)
					{
						try
						{
							using var cts = new CancellationTokenSource();
							_expirationCancellations.Add(task, cts);
							await Task.Delay(task.LaunchParameters.CompletionExpiryTime.Value, cts.Token);
						}
						finally
						{
							_expirationCancellations.Remove(task);
						}
					}

					_allTasks.Remove(task);
					task.Parent?.SubTasks.Remove(task);
					task.LaunchParameters.TriggeredChat?.AgentTasks.Remove(task);
					task.LaunchParameters.TriggeredMessage?.AgentTasks.Remove(task);
				}
			}
			catch (Exception ex)
			{
				Log.Error(ex, "Error while ending task: {Message}", ex.Message);
			}
		}
	}
}
