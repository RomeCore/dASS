using LiteDB;
using LLMDesktopAssistant.LLM.MVVM.Additional;
using LLMDesktopAssistant.LLM.MVVM.Additional.Context;
using LLMDesktopAssistant.MVVM;

namespace LLMDesktopAssistant.Prompting
{
	/// <summary>
	/// A context checkpoint: marks a position in the chat history where the agent context
	/// is cut (shield/summary) or compacted (tool/reasoning compaction).
	/// </summary>
	[ViewModelFor(typeof(ContextCheckpointView))]
	public class ContextCheckpoint : AdditionalChatData
	{
		/// <summary>
		/// Gets or sets the kind of context checkpoint this is.
		/// </summary>
		public ContextCheckpointKind Kind
		{
			get;
			set
			{
				if (SetProperty(ref field, value))
				{
					RaisePropertyChanged(nameof(IsShield));
					RaisePropertyChanged(nameof(IsSummary));
					RaisePropertyChanged(nameof(IsToolCompaction));
					RaisePropertyChanged(nameof(IsForcedToolCompaction));
					RaisePropertyChanged(nameof(IsReasoningCompaction));
				}
			}
		}

		/// <summary>
		/// Gets or sets the textual context associated with this checkpoint.
		/// This can be a summary if the kind is 'summary'.
		/// </summary>
		public string? Context
		{
			get;
			set => SetProperty(ref field, value);
		}

		private bool _isCompletedAndEnabled = true;
		/// <summary>
		/// Gets or sets a value indicating whether the checkpoint is completed (e.g. summary generation has finished)
		/// and enabled (manual on/off toggle). Disabled checkpoints stay in history but have no effect.
		/// </summary>
		public bool IsCompletedAndEnabled
		{
			get => _isCompletedAndEnabled;
			set => SetProperty(ref _isCompletedAndEnabled, value);
		}

		/// <summary>
		/// Display order - shown below message content.
		/// </summary>
		public override int Order => 50;

		/// <summary>
		/// Whether this checkpoint is a context shield (plain cut).
		/// </summary>
		[BsonIgnore]
		public bool IsShield => Kind.HasFlag(ContextCheckpointKind.Shield);

		/// <summary>
		/// Whether this checkpoint is a summary (cut with an agent-readable summary).
		/// </summary>
		[BsonIgnore]
		public bool IsSummary => Kind.HasFlag(ContextCheckpointKind.Summary);

		/// <summary>
		/// Whether this checkpoint compacts tool results of the upper messages.
		/// </summary>
		[BsonIgnore]
		public bool IsToolCompaction => Kind.HasFlag(ContextCheckpointKind.ToolCompaction);

		/// <summary>
		/// Whether this checkpoint force-compacts tool results of the upper messages.
		/// </summary>
		[BsonIgnore]
		public bool IsForcedToolCompaction => Kind.HasFlag(ContextCheckpointKind.ForcedToolCompaction);

		/// <summary>
		/// Whether this checkpoint compacts reasoning of the upper messages.
		/// </summary>
		[BsonIgnore]
		public bool IsReasoningCompaction => Kind.HasFlag(ContextCheckpointKind.ReasoningCompaction);
	}
}
