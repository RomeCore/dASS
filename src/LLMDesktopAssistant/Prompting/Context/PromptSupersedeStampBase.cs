namespace LLMDesktopAssistant.Prompting.Context
{
	public class PromptSupersedeStampBase : NotifyPropertyChanged
	{
		public string Discriminator
		{
			get;
			set => SetProperty(ref field, value);
		} = string.Empty;

		public string Snapshot
		{
			get;
			set => SetProperty(ref field, value);
		} = string.Empty;
	}
}
