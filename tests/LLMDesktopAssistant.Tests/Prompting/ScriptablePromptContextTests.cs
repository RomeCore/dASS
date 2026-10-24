using LLMDesktopAssistant.Prompting.Context.Scripting;

namespace LLMDesktopAssistant.Tests.Prompting;

/// <summary>
/// Tests for the shared rules of the scriptable prompt contexts: the XML wrapper and the namespaced
/// discriminator.
/// </summary>
/// <remarks>
/// The providers themselves are per language and only compose the interpreter call with these rules, so they are
/// not unit-tested: there is nothing left to break that is not the interpreter's own behaviour.
/// </remarks>
[Collection("Prompting")]
public class ScriptablePromptContextTests
{
	[Fact]
	public void Wrap_UsesTheNameAsTheXmlElement()
	{
		Assert.Equal("<git-status>\nclean\n</git-status>", ScriptPromptContextHelper.Wrap("git-status", "clean"));
	}

	[Fact]
	public void Wrap_PrefixesNamesStartingWithADigit()
	{
		// An XML element name cannot start with a digit.
		Assert.Equal("<_2fa>\non\n</_2fa>", ScriptPromptContextHelper.Wrap("2fa", "on"));
	}

	[Fact]
	public void Discriminator_IsNamespacedWithScript()
	{
		Assert.Equal("script:git-status", ScriptPromptContextHelper.GetDiscriminator("git-status"));
	}
}
