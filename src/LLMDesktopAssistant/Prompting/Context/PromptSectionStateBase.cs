using LiteDB;

namespace LLMDesktopAssistant.Prompting.Context
{
	public class PromptSectionStateBase : NotifyPropertyChanged
	{
		public string Discriminator
		{
			get;
			set => SetProperty(ref field, value);
		} = string.Empty;
	}
}
