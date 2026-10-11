using LLMDesktopAssistant.LLM.Services.Agents;
using LLMDesktopAssistant.Localization;

namespace LLMDesktopAssistant.SlashCommands.Providers
{
	/// <summary>
	/// The built-in <c>/call &lt;agent&gt;</c> command: forces the selected chat agent to run, bypassing the router and
	/// the execution-stage pipeline.
	/// </summary>
	[NativeSlashCommandProvider]
	public sealed class CallCommandProvider : NativeSlashCommandProvider
	{
		public CallCommandProvider(IAgentManagementService agentManager)
		{
			AddCommand(new SlashCommandInfo
			{
				Name = "call",
				NameKey = Locale.GetKey("command.call.name"),
				DescriptionKey = Locale.GetKey("command.call.description"),
				Executor = new CallCommandExecutor(agentManager)
			});
		}
	}
}
