using AngleSharp.Common;
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
		IMCPManagementService mcpManager,
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

		protected override IEnumerable<ToolInfo> GetAdditionalAddons()
		{
			return _services.GetServices<ToolModule>()
				.Concat(chat.AdditionalTools ?? [])
				.Concat(mcpManager.GetMCPTools())
				.SelectMany(m => m.GetTools());
		}

		public override IEnumerable<ToolInfo> GetAddonsForAgent(ChatAgentDescriptor agent)
		{
			var settings = agent.Tools;
			if (!settings.EnableTools)
				return [];

			var toolset = settings.GetEffectiveToolset(chatSettings.Settings).GetEffectiveConfiguration();
			return GetAddonsWithChanges(toolset, agent);
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

		protected override void ApplyDefaults(ToolInfo target, AddonSetConfigurationBase<ToolChange> setConfig,
			ChatAgentDescriptor? agent)
		{
			base.ApplyDefaults(target, setConfig, agent);

			// The factory and the level are properties of the concrete agent toolset, so they must be stamped
			// on every clone, not only on the ones with an explicit change entry.
			target.ContextFactory = ContextFactory;
			target.ApprovalLevel = target.ApprovalLevel ??
				(setConfig as ToolsetConfiguration)?.DefaultApprovalLevel ??
				ToolApprovalLevel.PolicyBased;
		}
	}
}