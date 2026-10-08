using System.Collections.Immutable;
using LLMDesktopAssistant.Addons;
using LLMDesktopAssistant.Agents.SubAgents;
using LLMDesktopAssistant.Agents.Tasks;
using LLMDesktopAssistant.LLM.Services;
using LLMDesktopAssistant.LLM.Services.Agents;
using LLMDesktopAssistant.Localization;
using LLMDesktopAssistant.SlashCommands.Arguments;
using LLMDesktopAssistant.SlashCommands.Execution;

namespace LLMDesktopAssistant.SlashCommands.Providers
{
	/// <summary>
	/// Derives an <c>/agent:&lt;name&gt;</c> command from every available sub-agent.
	/// </summary>
	[ChatService(typeof(ISlashCommandProvider))]
	public class SubAgentSlashCommandProvider(
		IAddonSetCollector<SubAgentInfo> sources,
		ISubAgentTaskParamsResolver paramsResolver,
		IAgentTaskExecutor agentTaskExecutor,
		IChatSettingsService chatSettings)
		: DerivedSlashCommandProvider<SubAgentInfo>(sources)
	{
		protected override string TypeNamespace => "agent";

		protected override SlashCommandSource SourceKind => SlashCommandSource.SubAgent;

		/// <remarks>
		/// The sub-agent input is the rest positional; <c>wait</c> is an optional boolean with a string default,
		/// validated and converted by the boolean format provider.
		/// </remarks>
		protected override SlashCommandArgumentSchema CreateArgumentSchema(SubAgentInfo source)
		{
			var keyed = ImmutableDictionary.CreateBuilder<string, SlashCommandArgument>();
			keyed[SubAgentCommandExecutor.WaitArgumentKey] = new SlashCommandArgument
			{
				Name = Locale.GetKey("command.argument.wait"),
				Description = Locale.GetKey("command.argument.wait.description"),
				Required = false,
				Default = "false",
				Format = SlashCommandBooleanFormatProvider.Instance
			};

			return new SlashCommandArgumentSchema
			{
				HasRestPositional = true,
				Keyed = keyed.ToImmutable()
			};
		}

		protected override ISlashCommandExecutor CreateCommandExecutor(SubAgentInfo source)
			=> new SubAgentCommandExecutor(source, paramsResolver, agentTaskExecutor, chatSettings);
	}
}
