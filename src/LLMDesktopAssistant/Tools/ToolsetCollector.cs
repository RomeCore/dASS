using LLMDesktopAssistant.Addons;
using LLMDesktopAssistant.Agents;
using LLMDesktopAssistant.LLM.Domain;
using LLMDesktopAssistant.LLM.Services;
using LLMDesktopAssistant.LLM.Services.Agents;
using LLMDesktopAssistant.Tools.Consents;
using LLMDesktopAssistant.Tools.Execution.Contexts;

namespace LLMDesktopAssistant.Tools
{
	[ChatService(typeof(IAddonSetCollector<ToolInfo>))]
	public class ToolsetCollector(
		Chat chat,
		IChatSettingsService chatSettings,
		IServiceProvider services
		) : AddonSetCollectorBase<ToolInfo, ToolChange>(services)
	{
		private readonly IServiceProvider _services = services;
		private IToolExecutionContextFactory? _contextFactory;

		private IToolExecutionContextFactory ContextFactory => _contextFactory ??= new ChatToolContextFactory(
			chat,
			_services.GetRequiredService<IToolMemorizationService>(),
			_services.GetRequiredService<IAgentManagementService>(),
			chatSettings);

		protected override AddonTarget ResolveAgentTarget(ChatAgentDescriptor agent)
		{
			var toolset = agent.Tools.GetEffectiveToolset(chatSettings.Settings).GetEffectiveConfiguration();
			return new(toolset, agent.Tools);
		}

		protected override void ApplyChange(ToolInfo target, ToolChange change, ChatAgentDescriptor? agent)
		{
			base.ApplyChange(target, change, agent);
			target.ApprovalHelper = change.ApprovalHelper ?? target.ApprovalHelper;
			target.ApprovalLevel = change.ApprovalLevel ?? target.ApprovalLevel;
			target.PolicyMask = change.PolicyMask ?? target.PolicyMask;
			target.SpecifierUnionMode = change.SpecifierUnionMode ?? target.SpecifierUnionMode;
			target.SpecifierAggregationMode = change.SpecifierAggregationMode ?? target.SpecifierAggregationMode;
			target.Specifiers = [.. target.Specifiers, .. change.Specifiers];
		}

		protected override void ApplyDefaults(ToolInfo target, AddonSetConfigurationBase<ToolChange> config,
			ChatAgentDescriptor? agent)
		{
			base.ApplyDefaults(target, config, agent);

			// The factory and the level are properties of the concrete agent toolset, so they must be stamped
			// on every clone, not only on the ones with an explicit change entry.
			target.ContextFactory = ContextFactory;
			target.ApprovalLevel = target.ApprovalLevel ??
				(config as ToolsetConfiguration)?.DefaultApprovalLevel ??
				ToolApprovalLevel.PolicyBased;
		}

		protected override bool IsAffectedByDefault(ToolInfo addon, ToolChange? change, string? propertyName)
		{
			if (propertyName == nameof(ToolsetConfiguration.DefaultApprovalLevel))
				return change?.ApprovalLevel is null && addon.ApprovalLevel is null;
			return base.IsAffectedByDefault(addon, change, propertyName);
		}
	}
}
