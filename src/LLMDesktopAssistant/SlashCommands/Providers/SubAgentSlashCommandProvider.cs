using System.Collections.Immutable;
using LLMDesktopAssistant.Addons;
using LLMDesktopAssistant.Agents.SubAgents;
using LLMDesktopAssistant.LLM.Services;
using LLMDesktopAssistant.Localization;
using LLMDesktopAssistant.SlashCommands.Arguments;
using LLMDesktopAssistant.SlashCommands.Execution;

namespace LLMDesktopAssistant.SlashCommands.Providers
{
	/// <summary>
	/// Derives an <c>/agent:&lt;name&gt;</c> command from every available sub-agent.
	/// </summary>
	[ChatService(typeof(ISlashCommandProvider))]
	public class SubAgentSlashCommandProvider(IAddonSetCollector<SubAgentInfo> sources)
		: DerivedSlashCommandProvider<SubAgentInfo>(sources)
	{
		protected override string TypeNamespace => "agent";

		protected override SlashCommandSource SourceKind => SlashCommandSource.SubAgent;

		/// <remarks>
		/// The sub-agent input is the rest positional; <c>wait</c> is an optional boolean that is left with a string
		/// default — its format provider arrives with the executor (Stage 3).
		/// </remarks>
		protected override SlashCommandArgumentSchema CreateArgumentSchema(SubAgentInfo source)
		{
			var keyed = ImmutableDictionary.CreateBuilder<string, SlashCommandArgument>();
			keyed["wait"] = new SlashCommandArgument
			{
				Name = Locale.GetKey("command.argument.wait"),
				Description = Locale.GetKey("command.argument.wait.description"),
				Required = false,
				Default = "false"
			};

			return new SlashCommandArgumentSchema
			{
				HasRestPositional = true,
				Keyed = keyed.ToImmutable()
			};
		}

		protected override ISlashCommandExecutor CreateCommandExecutor(SubAgentInfo source)
			=> StubCommandExecutor.Instance;
	}
}
