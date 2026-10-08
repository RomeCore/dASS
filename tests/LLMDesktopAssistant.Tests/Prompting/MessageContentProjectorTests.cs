using LLMDesktopAssistant.LLM.MVVM.Additional;
using LLMDesktopAssistant.LLM.Services.Prompting;
using LLMDesktopAssistant.SlashCommands;
using LLMDesktopAssistant.SlashCommands.Execution;

namespace LLMDesktopAssistant.Tests.Prompting;

/// <summary>
/// The model-facing content projection: the raw message content shaped by the producing command's
/// <see cref="ModelFacingMode"/>, with the command-injected content parts appended in every mode.
/// </summary>
[Collection("Prompting")]
public class MessageContentProjectorTests
{
	private static SlashCommandFingerprint Fingerprint(ModelFacingMode mode, string rawToken = "grilling") => new()
	{
		Token = "skill:" + rawToken,
		RawToken = rawToken,
		ModelFacingMode = mode
	};

	private static AdditionalMessageContentPart Part(string content) => new() { Content = content };

	private static string? Project(string content, MessagePartsFacet parts, params object[] additional)
	{
		var message = PromptingTestHelpers.User(content, 0).Message;
		// Raise the collection events synchronously (see SkillCommandExecutorTests): the default collection marshals
		// to the Avalonia UI thread, which deadlocks when two test classes do it in parallel.
		message.AdditionalData.RaiseInUIThread = false;
		foreach (var item in additional)
			message.AdditionalData.Add((AdditionalChatData)item);
		return MessageContentProjector.Project(message, parts);
	}

	[Fact]
	public void WhenTheContentFacetIsNotRequested_ProjectsNothing()
		=> Assert.Null(Project("hello", MessagePartsFacet.None));

	[Fact]
	public void Raw_ProjectsTheMessageContent()
		=> Assert.Equal("hello", Project("hello", MessagePartsFacet.Content, Fingerprint(ModelFacingMode.Raw)));

	[Fact]
	public void Raw_AppendsTheInjectedContentParts()
		=> Assert.Equal("hello\n\nSKILL BODY",
			Project("hello", MessagePartsFacet.Content, Fingerprint(ModelFacingMode.Raw), Part("SKILL BODY")));

	[Fact]
	public void Neutral_ReplacesTheRawContentWithTheBareToken()
		=> Assert.Equal("/grilling\n\nSKILL BODY",
			Project("/skill:grilling do it", MessagePartsFacet.Content,
				Fingerprint(ModelFacingMode.Neutral), Part("SKILL BODY")));

	[Fact]
	public void Hidden_DropsTheRawContent_ButKeepsTheContentParts()
		=> Assert.Equal("SKILL BODY",
			Project("/skill:grilling do it", MessagePartsFacet.Content,
				Fingerprint(ModelFacingMode.Hidden), Part("SKILL BODY")));

	[Fact]
	public void Hidden_WithNoContentParts_ProjectsNothing()
		=> Assert.Null(Project("/skill:grilling", MessagePartsFacet.Content, Fingerprint(ModelFacingMode.Hidden)));

	[Fact]
	public void EmptyContent_WithNoContentParts_ProjectsNothing()
		=> Assert.Null(Project(string.Empty, MessagePartsFacet.Content));
}
