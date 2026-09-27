namespace LLMDesktopAssistant.Agents
{
	public class AgentReadDefaultRows : NotifyPropertyChanged
	{
		/// <summary>
		/// The row to apply for agent by default.
		/// </summary>
		public AgentReadRow Agent
		{
			get;
			set => SetProperty(ref field, value);
		} = new();

		/// <summary>
		/// The row to apply for user by default.
		/// </summary>
		public AgentReadRow User
		{
			get;
			set => SetProperty(ref field, value);
		} = new();
	}
}
