using System.ComponentModel;
using LLMDesktopAssistant.Agents.Memory;
using LLMDesktopAssistant.LLM.Domain;
using LLMDesktopAssistant.LLM.Services;
using LLMDesktopAssistant.LLM.Services.Agents;
using LLMDesktopAssistant.Tools.Execution.Domain;
using LLMDesktopAssistant.Tools.Execution.Support;
using Material.Icons;
using LLMDesktopAssistant.Localization;

namespace LLMDesktopAssistant.Tools.Implementations.Memory
{
	[ToolModule]
	public class MemoryClearToolModule : MemoryToolModuleBase
	{
		private readonly IMemoryFactStore _memoryFactStore;
		private readonly IMemoryLogStore _memoryLogStore;

		/// <summary>
		/// Initializes a new instance of the <see cref="MemoryClearToolModule"/> class.
		/// </summary>
		/// <param name="chat">The chat instance where the tools are executed.</param>
		/// <param name="chatSettings">The settings service of the chat.</param>
		/// <param name="agentManager">The agent management service used to resolve agent memory attachments.</param>
		/// <param name="memoryFactStore">The store used to clear semantic memory facts.</param>
		/// <param name="memoryLogStore">The store used to clear episodic memory logs.</param>
		public MemoryClearToolModule(Chat chat, IChatSettingsService chatSettings, IAgentManagementService agentManager,
			IMemoryFactStore memoryFactStore, IMemoryLogStore memoryLogStore)
			: base(chat, chatSettings, agentManager)
		{
			_memoryFactStore = memoryFactStore;
			_memoryLogStore = memoryLogStore;

			AddTool(new ToolInitializationInfo
			{
				Executor = ClearAsync,
				Name = "memory-clear",
				Description = """
					Clears the stored facts and/or episodic logs from the specified memory block.
					The block itself and its configuration are preserved.
					""",
				DefaultExpectedBehaviour = ToolBehaviour.SemanticMemoryClear,
				NameKey = Locale.GetKey("tool.name.memory-clear"),
				DescriptionKey = Locale.GetKey("tool.description.memory-clear"),
				CategoryKey = Locale.GetKey("tool.category.memory")
			});
		}

		private async Task<ToolExecutionResult> ClearAsync(
			[Description("The memory block to clear")] string block,
			IToolExecutionContext ctx,
			[Description("Whether to clear the semantic facts. Defaults to true")] bool clearFacts = true,
			[Description("Whether to clear the episodic logs. Defaults to false")] bool clearLogs = false,
			CancellationToken cancellationToken = default)
		{
			ctx.Call.SetStatus(MaterialIconKind.DatabaseRefresh, $"**{block}**");

			var (targetBlock, error) = GetBlock(ctx, block, requireReading: false, requireWriting: true);
			if (targetBlock is null)
			{
				ctx.Call.SetResult(error ?? $"Memory block '{block}' is not available.");
				return ToolExecutionResult.Failed;
			}

			try
			{
				int removedFacts = 0;
				if (clearFacts && targetBlock.FactsEnabled)
					removedFacts = await _memoryFactStore.ClearAsync(targetBlock, cancellationToken);

				int removedLogs = 0;
				if (clearLogs && targetBlock.LogsEnabled)
					removedLogs = await _memoryLogStore.ClearAsync(targetBlock, cancellationToken);

				ctx.Call.SetStatusIcon(MaterialIconKind.DatabaseCheck);
				ctx.Call.SetResult($"Memory block '{block}' cleared. Removed facts: {removedFacts}, removed logs: {removedLogs}.");
				return ToolExecutionResult.Succeeded;
			}
			catch (Exception ex)
			{
				ctx.Call.SetStatusIcon(MaterialIconKind.DatabaseOff);
				ctx.Call.SetResult($"Failed to clear memory block '{block}'. Error: {ex.Message}");
				return ToolExecutionResult.Failed;
			}
		}
	}
}
