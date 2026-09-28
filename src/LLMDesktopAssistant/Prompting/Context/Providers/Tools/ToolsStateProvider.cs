using LLMDesktopAssistant.Agents;
using LLMDesktopAssistant.LLM.Services;
using LLMDesktopAssistant.LLM.Services.Prompting;
using LLMDesktopAssistant.LLM.Services.Tools;

namespace LLMDesktopAssistant.Prompting.Context.Providers.Tools
{
	/// <summary>
	/// Captures the tools section state: the visible tools of the agent and the names of its hidden tools.
	/// The hidden names are captured in the hybrid mode only.
	/// The cache is invalidated by the prompt composer, not by this provider.
	/// </summary>
	[ChatService(typeof(IPromptSectionStateProvider<ToolsSectionState>))]
	public class ToolsStateProvider(
		IToolsetCacheService toolsetCache) : IPromptSectionStateProvider<ToolsSectionState>
	{
		/// <inheritdoc/>
		public ToolsSectionState CaptureState(ChatAgentDescriptor agent)
		{
			var items = new List<ToolItem>();
			var hiddenNames = new List<string>();

			// The hidden tools are listed in the prompt in the hybrid mode only:
			// outside of it deltas are not computed at all, so the hidden list has no purpose there.
			bool captureHiddenTools = agent.Context.PromptMode == PromptContextMode.Hybrid;

			foreach (var tool in toolsetCache.ValidTools.Values)
			{
				if (tool.Hidden ?? false)
				{
					if (captureHiddenTools)
						hiddenNames.Add(tool.Name);
				}
				else
				{
					items.Add(new ToolItem
					{
						Name = tool.Name,
						Description = tool.Description,
						ArgumentSchema = tool.ArgumentSchema.ToJsonString()
					});
				}
			}

			return new ToolsSectionState
			{
				Items = [.. items.OrderBy(t => t.Name, StringComparer.Ordinal)],
				HiddenNames = [.. hiddenNames.OrderBy(n => n, StringComparer.Ordinal)]
			};
		}
	}
}
