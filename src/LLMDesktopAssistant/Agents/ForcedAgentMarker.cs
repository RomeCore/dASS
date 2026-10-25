using LLMDesktopAssistant.LLM.MVVM.Additional;

namespace LLMDesktopAssistant.Agents
{
	/// <summary>
	/// Marks a user message whose turn must be run by a specific chat agent, regardless of the execution-stage
	/// pipeline.
	/// </summary>
	/// <remarks>
	/// A pipeline directive carried on a message: whoever decides to force an agent (a command, a script, ...) only
	/// attaches it, while the agent ordering (<c>AgentOrderingService</c>) consumes it - the pipeline never needs to
	/// know who produced the marker. Data-only (<see cref="AdditionalChatData.IsVisible"/> is <see langword="false"/>,
	/// so it is never rendered into the prompt and never appears in the UI) but persisted, so it survives a reload. The
	/// marker is never consumed; the ordering honours it only while the forced agent still has not spoken since the
	/// marked message.
	/// </remarks>
	public class ForcedAgentMarker : AdditionalChatData
	{
		/// <summary>
		/// The id of the chat agent to force.
		/// </summary>
		public Guid AgentId { get; set; }

		public ForcedAgentMarker()
		{
			IsVisible = false;
		}
	}
}
