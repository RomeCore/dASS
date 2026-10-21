#pragma warning disable CS9113 // Parameter has not been used.

using LLMDesktopAssistant.LLM.MVVM.Additional;

namespace LLMDesktopAssistant.LLM.Services
{
	/// <summary>
	/// Chat execution hook that sets working directory snapshot to messages.
	/// </summary>
	[ChatService(typeof(IChatExecutionHook))]
	public class WorkingDirectorySettingHook(
		IWorkingDirectoryAccessService wdAccess
	) : IChatExecutionHook
	{
		Task IChatExecutionHook.OnResponsePrepareAsync(ChatPrepareExecutionHookContext context,
			CancellationToken cancellationToken)
		{
			int i = context.Chat.Messages.Count - 1;
			while (i >= 0 && context.Chat.Messages[i].Message.AdditionalData.TryGet<WorkingDirectorySnapshotData>() is null)
			{
				context.Chat.Messages[i].Message.AdditionalData.Add(new WorkingDirectorySnapshotData
				{
					Workdir = wdAccess.GetWorkingDirectory()
				});
				i--;
			}

			return Task.CompletedTask;
		}
	}
}