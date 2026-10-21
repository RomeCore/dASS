namespace LLMDesktopAssistant.LLM.MVVM.Additional
{
	public class WorkingDirectorySnapshotData : AdditionalChatData
	{
		public required string Workdir { get; init; }

		public WorkingDirectorySnapshotData()
		{
			IsVisible = false;
		}
	}
}
