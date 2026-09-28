using LLMDesktopAssistant.LLM.MVVM.Messages;
using LLMDesktopAssistant.Prompting;

namespace LLMDesktopAssistant.UIExtensions.MessageExtensions
{
	/// <summary>
	/// Shared helpers for message extension buttons that toggle context checkpoints.
	/// A message carries at most one checkpoint whose Kind is a mask; toggling flips the bit
	/// and removes the checkpoint once the mask becomes empty.
	/// </summary>
	internal static class MessageExtensionHelpers
	{
		public static void ToggleCheckpoint(MessageViewModelBase viewModel, ContextCheckpointKind kind)
		{
			var additionalData = viewModel.Message.AdditionalData;
			var checkpoint = additionalData.TryGet<ContextCheckpoint>();

			if (checkpoint == null)
			{
				additionalData.Add(new ContextCheckpoint { Kind = kind });
				return;
			}

			bool enabling = !checkpoint.Kind.HasFlag(kind);
			var newKind = enabling ? checkpoint.Kind | kind : checkpoint.Kind & ~kind;

			// Cut kinds are mutually exclusive: enabling one disables the other.
			if (enabling && kind is ContextCheckpointKind.Shield or ContextCheckpointKind.Summary)
			{
				newKind &= ~(ContextCheckpointKind.Shield | ContextCheckpointKind.Summary);
				newKind |= kind;
			}

			checkpoint.Kind = newKind;

			if (checkpoint.Kind == ContextCheckpointKind.None)
				additionalData.Remove(checkpoint);
		}
	}
}
