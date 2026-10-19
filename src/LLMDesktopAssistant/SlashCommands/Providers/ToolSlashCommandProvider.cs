using LLMDesktopAssistant.Addons;
using LLMDesktopAssistant.LLM.Services;
using LLMDesktopAssistant.SlashCommands.Execution;
using LLMDesktopAssistant.Tools;

namespace LLMDesktopAssistant.SlashCommands.Providers
{
	/// <summary>
	/// Derives a <c>/tool:&lt;name&gt;</c> command from every available tool.
	/// </summary>
	/// <remarks>
	/// The provider reads the <em>available</em> addons — the chat's own tool surface (native modules, the chat's
	/// additional tools, MCP) — and deliberately does not go through an agent's toolset: a command is the user's own
	/// action, so the per-agent approval level, policy mask and specifiers never apply to it. A tool that no agent has
	/// enabled is therefore still runnable by hand.
	/// </remarks>
	[ChatService(typeof(ISlashCommandProvider))]
	public class ToolSlashCommandProvider(IAddonSetCollector<ToolInfo> sources)
		: DerivedSlashCommandProvider<ToolInfo>(sources)
	{
		protected override string TypeNamespace => "tool";

		protected override SlashCommandSource SourceKind => SlashCommandSource.Tool;

		protected override ISlashCommandExecutor CreateCommandExecutor(ToolInfo source)
			=> new ToolCommandExecutor(source);
	}
}
