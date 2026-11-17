using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using LLMDesktopAssistant.Utils;

namespace LLMDesktopAssistant.Agents.Tasks
{
	/// <summary>
	/// Represents a single AI agent execution task.
	/// </summary>
	public class AgentTask : NotifyPropertyChanged
	{
		/// <summary>
		/// The unique identifier for this agentic task.
		/// </summary>
		public required Guid Id { get; init; }

		/// <summary>
		/// The executor responsible for running this agentic task.
		/// </summary>
		public required IAgentTaskExecutor Executor { get; init; }

		/// <summary>
		/// The parent of this agentic task, if any.
		/// </summary>
		public required AgentTask? Parent { get; init; }

		/// <summary>
		/// The parameters used to launch this agentic task.
		/// </summary>
		public required AgentTaskLaunchParameters LaunchParameters { get; init; }

		/// <summary>
		/// The completion token used to await the completion of this agentic task.
		/// </summary>
		public required Task<AgentTask> Completion
		{
			get;
			set => SetProperty(ref field, value);
		}
		public TaskAwaiter<AgentTask> GetAwaiter() => Completion.GetAwaiter();

		/// <summary>
		/// The cancellation token source used to cancel this agentic task.
		/// </summary>
		public required CancellationTokenSource CancellationTokenSource
		{
			get;
			set => SetProperty(ref field, value);
		}

		internal AgentTaskExecutionInfo? ExecutionInfo { get; set; }

		/// <summary>
		/// The queue of follow-up messages for this agentic task.
		/// If any message is put to this queue (ONLY while running a task), it will be dequeued
		/// right after the next message or tool batch is completed.
		/// </summary>
		public ConcurrentQueue<AgentChatMessage> FollowUpMessages { get; } = [];

		/// <summary>
		/// A collection of sub-tasks associated with this agentic task.
		/// </summary>
		public RangeObservableCollection<AgentTask> SubTasks { get; } = [];

		/// <summary>
		/// A collection of chat messages associated with this agentic task.
		/// </summary>
		public RangeObservableCollection<AgentChatMessage> Messages { get; } = [];

		/// <summary>
		/// How many leading <see cref="Messages"/> were supplied as the task's initial context.
		/// </summary>
		public int InitialMessageCount
		{
			get;
			internal set => SetProperty(ref field, value);
		}

		/// <summary>
		/// The last generated message by the agent associated with this task.
		/// </summary>
		public AgentAssistantMessage? LastGeneratedMessage
		{
			get;
			internal set => SetProperty(ref field, value);
		}

		/// <summary>
		/// The last generated message's content by the agent associated with this task.
		/// </summary>
		public string? LastGeneratedContent
		{
			get;
			internal set => SetProperty(ref field, value);
		}

		/// <summary>
		/// The current status of this agentic task.
		/// </summary>
		public AgentTaskStatus Status
		{
			get;
			internal set => SetProperty(ref field, value);
		} = AgentTaskStatus.Pending;

		/// <summary>
		/// Indicates whether this agentic task has completed.
		/// </summary>
		public bool Completed
		{
			get;
			internal set => SetProperty(ref field, value);
		}

		/// <summary>
		/// The number of feedback rounds completed by this task, i.e. how many times the task
		/// continued execution after the model produced a response without tool calls
		/// via <see cref="AgentTaskLaunchParameters.FeedbackFunc"/>.
		/// Zero when no feedback rounds were performed.
		/// </summary>
		public int FeedbackIterations
		{
			get;
			internal set => SetProperty(ref field, value);
		}

		/// <summary>
		/// The moment the initial launch of this task started (e.g. a task was created and launched for the first time).
		/// </summary>
		public DateTime StartedAt
		{
			get;
			internal set => SetProperty(ref field, value);
		}

		/// <summary>
		/// The moment the current launch finished, or <see langword="null"/> while the task is still running.
		/// </summary>
		public DateTime? CompletedAt
		{
			get;
			internal set => SetProperty(ref field, value);
		}

		/// <summary>
		/// The moment the current launch of this task started (e.g. this task was launched both at first time and subsequent times).
		/// </summary>
		public DateTime ContinuedAt
		{
			get;
			internal set => SetProperty(ref field, value);
		}

		/// <summary>
		/// The exception that occurred during the execution of this agentic task.
		/// </summary>
		public Exception? Exception
		{
			get;
			internal set => SetProperty(ref field, value);
		}

		/// <summary>
		/// Why the current launch was blocked before it ran, or <see langword="null"/> when it was not blocked.
		/// </summary>
		/// <remarks>
		/// A block is not an error: this field is the outcome that matters, and <see cref="Exception"/> carries the same
		/// reason only because a caller that waited receives it as a thrown exception. The field belongs to the status,
		/// not to whoever produced it - a <c>task.start</c> hook is the only producer today. A later launch of the same
		/// task clears it.
		/// </remarks>
		public string? BlockReason
		{
			get;
			internal set => SetProperty(ref field, value);
		}

		/// <summary>
		/// The usage statistics for this agentic task.
		/// This is sum for all processed messages usage.
		/// </summary>
		public AgentUsageStatistics? UsageStatistics
		{
			get;
			internal set => SetProperty(ref field, value);
		}

		/// <summary>
		/// The usage statistics for this agentic task, but only for last launch (continuing a task resets it).
		/// This is sum for all processed messages usage.
		/// </summary>
		public AgentUsageStatistics? NewUsageStatistics
		{
			get;
			internal set => SetProperty(ref field, value);
		}

		/// <summary>
		/// A collection of tool calls that require user confirmation before execution.
		/// </summary>
		public RangeObservableCollection<AgentToolCallConfirmationRequest> ToolCallConfirmationRequests { get; } = [];

		/// <summary>
		/// Continues the execution of this agentic task with the specified messages.
		/// </summary>
		/// <param name="messages">The messages to continue the execution with.</param>
		public void Continue(IEnumerable<AgentChatMessage> messages, CancellationToken cancellationToken = default)
		{
			ContinuedAt = DateTime.Now;

			foreach (var message in messages)
				FollowUpMessages.Enqueue(message);

			if (Completed)
				Executor.Continue(this, [], cancellationToken);
			else if (cancellationToken.CanBeCanceled && !cancellationToken.IsCancellationRequested)
				CancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(
					CancellationTokenSource.Token, cancellationToken);
		}
	}
}
