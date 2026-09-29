namespace LLMDesktopAssistant.Prompting.Context
{
	public class PromptSectionDeltaBase : NotifyPropertyChanged
	{
		public string Discriminator
		{
			get;
			set => SetProperty(ref field, value);
		} = string.Empty;
	}

}
