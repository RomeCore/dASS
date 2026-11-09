namespace LLMDesktopAssistant.Services.Instances
{
	/// <summary>
	/// Interface for handling app closing events.
	/// </summary>
	public interface IAppClosingHandler
	{
		/// <summary>
		/// Called when an app is closing.
		/// </summary>
		void OnAppClosing();
	}
}
