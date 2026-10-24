using LLMDesktopAssistant.Agents;
using LLMDesktopAssistant.LLM.Services.Prompting;
using LLMDesktopAssistant.Prompting.ContextExpanders;
using LLMDesktopAssistant.Prompting.Plugins;
using LLTSharp;

namespace LLMDesktopAssistant.Prompting.Context.Providers.Reminder
{
	public class SystemReminderSection(
		IServiceProvider services
	) : IPromptAnchoredSectionProvider
	{
		readonly ITemplateLibraryAccessor _templates = services.GetRequiredService<ITemplateLibraryAccessor>();
		readonly IEnumerable<IPromptSystemContextExpander> _promptSystemContextExpanders = services.GetServices<IPromptSystemContextExpander>();
		readonly IEnumerable<IPromptTemplatePlugin> _promptTemplatePlugins = services.GetServices<IPromptTemplatePlugin>();

		public string Discriminator => "system-reminder";

		public Task<PromptSectionStateBase?> CaptureStateAsync(ChatAgentDescriptor agent,
			CancellationToken cancellationToken = default)
		{
			// Return the placeholder, because rendering not depends on data.
			return Task.FromResult<PromptSectionStateBase?>(new PromptSectionStateBase());
		}

		public Task<PromptSectionDeltaBase?> CalculateDeltaAsync(PromptSectionStateBase? anchorState,
			IEnumerable<PromptSectionDeltaBase> existingDeltas, PromptContextExecutionContext context,
			CancellationToken cancellationToken = default)
		{
			// No deltas, just the anchor.
			return Task.FromResult<PromptSectionDeltaBase?>(null);
		}

		public SystemPromptSnapshot RenderState(PromptSectionStateBase state)
		{
			return _templates.GetTextTemplate("reminder_system_section").Render();
		}

		public string RenderDelta(PromptSectionDeltaBase delta)
		{
			return string.Empty;
		}
	}
}
