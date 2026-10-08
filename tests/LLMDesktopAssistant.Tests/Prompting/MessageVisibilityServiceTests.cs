using LLMDesktopAssistant.LLM.Services.Prompting;

namespace LLMDesktopAssistant.Tests.Prompting;

/// <summary>
/// The disabled-for-agents rule: a message carrying the flag is invisible to every agent, including its own
/// sender, because the rule is checked first — before the type branches.
/// </summary>
[Collection("Prompting")]
public class MessageVisibilityServiceTests
{
	// The disabled path returns before any dependency is touched, so they may be null here.
	private static MessageVisibilityService CreateService() => new(null!, null!);

	[Fact]
	public void DisabledUserMessage_IsInvisibleToAnAgent()
	{
		var service = CreateService();
		var message = PromptingTestHelpers.User("u0", 0);
		message.Message.IsDisabledForAgents = true;
		var agent = PromptingTestHelpers.CreateAgent();

		var result = service.CheckVisibility(message, agent);

		Assert.False(result.Visible);
		Assert.False(result.EffectiveVisible);
	}

	[Fact]
	public void DisabledAssistantMessage_IsInvisibleEvenToItsOwnSender()
	{
		var service = CreateService();
		var agent = PromptingTestHelpers.CreateAgent();
		var message = PromptingTestHelpers.Assistant("a0", 0, senderAgentId: agent.Id);
		message.Message.IsDisabledForAgents = true;

		var result = service.CheckVisibility(message, agent);

		// The same-agent shortcut of the assistant branch must not resurrect a disabled message.
		Assert.False(result.EffectiveVisible);
	}
}
