using LLMDesktopAssistant.Agents;
using LLMDesktopAssistant.LLM.Services.Prompting;
using LLMDesktopAssistant.Prompting.Context;
using LLMDesktopAssistant.Prompting.Context.Scripting;
using LLMDesktopAssistant.Scripting;

namespace LLMDesktopAssistant.Tests.Prompting;

/// <summary>
/// Tests for the scriptable prompt contexts: the XML wrapper, the "empty output means no context" rule,
/// the timeout isolation and the byte-exact change detection of the supersede stamps.
/// </summary>
[Collection("Prompting")]
public class ScriptablePromptContextTests
{
	private sealed class FakeEngine(string output, TimeSpan? delay = null) : IScriptablePromptContextEngine
	{
		public string? LastPrevious { get; private set; }

		public ScriptLanguageType Language => ScriptLanguageType.Lua;

		public IScriptablePromptContextEngineDescriptor Descriptor { get; } = new FakeDescriptor();

		public async Task<string?> RunAsync(IServiceProvider services, string body, string? previous,
			CancellationToken cancellationToken = default)
		{
			LastPrevious = previous;
			if (delay is { } d)
				await Task.Delay(d, cancellationToken);

			return output;
		}
	}

	private sealed class FakeDescriptor : IScriptablePromptContextEngineDescriptor
	{
		public ScriptLanguageType Language => ScriptLanguageType.Lua;

		public string[] Extensions => [".lua"];

		public string FrontmatterStart => "--[[";

		public string FrontmatterEnd => "]]";
	}

	private static PromptContextInfo CreateAddon(string name, int timeoutMs = 2000) => new()
	{
		Name = name,
		Description = name,
		BodyGetter = _ => "print('x')",
		TimeoutMs = timeoutMs
	};

	private static EffectiveChatContext CreateContext() => new()
	{
		Chat = PromptingTestHelpers.CreateChat(),
		Services = new EmptyServiceProvider(),
		Agent = new ChatAgentDescriptor(),
		Messages = [],
		Checkpoints = [],
		EffectiveMessagesStartIndex = 0,
		LastCutIndex = -1,
		LastCheckpointIndex = -1
	};

	// =====================================================
	// === Wrapper                                      ===
	// =====================================================

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

	// =====================================================
	// === Live tail                                    ===
	// =====================================================

	[Fact]
	public async Task LiveTail_WrapsTheScriptOutput()
	{
		var context = new LiveTailScriptContextProvider(new FakeEngine("hello"), CreateAddon("greeting"));

		Assert.Equal("<greeting>\nhello\n</greeting>", await context.ProvideAsync(CreateContext()));
	}

	[Fact]
	public async Task LiveTail_WhitespaceOutput_ProducesNoContext()
	{
		var context = new LiveTailScriptContextProvider(new FakeEngine("   \n"), CreateAddon("greeting"));

		Assert.Null(await context.ProvideAsync(CreateContext()));
	}

	[Fact]
	public async Task LiveTail_Timeout_ProducesNoContext()
	{
		var engine = new FakeEngine("late", TimeSpan.FromSeconds(30));
		var context = new LiveTailScriptContextProvider(engine, CreateAddon("slow", timeoutMs: 10));

		Assert.Null(await context.ProvideAsync(CreateContext()));
	}

	// =====================================================
	// === Supersede                                    ===
	// =====================================================

	[Fact]
	public async Task Supersede_FirstRun_EmitsAStampAndPassesNoPrevious()
	{
		var engine = new FakeEngine("v1");
		var context = new SupersedeScriptContextProvider(engine, CreateAddon("state"));

		var stamp = await context.GetNewStampAsync(null, CreateContext());

		Assert.NotNull(stamp);
		Assert.Equal("<state>\nv1\n</state>", stamp!.Snapshot);
		Assert.Null(engine.LastPrevious);
	}

	[Fact]
	public async Task Supersede_UnchangedOutput_EmitsNoStamp()
	{
		var engine = new FakeEngine("v1");
		var context = new SupersedeScriptContextProvider(engine, CreateAddon("state"));
		var previous = new ScriptPromptContextStamp { Snapshot = "<state>\nv1\n</state>" };

		Assert.Null(await context.GetNewStampAsync(previous, CreateContext()));
		// The script still receives the previously injected text.
		Assert.Equal("<state>\nv1\n</state>", engine.LastPrevious);
	}

	[Fact]
	public async Task Supersede_ChangedOutput_EmitsAStamp()
	{
		var engine = new FakeEngine("v2");
		var context = new SupersedeScriptContextProvider(engine, CreateAddon("state"));
		var previous = new ScriptPromptContextStamp { Snapshot = "<state>\nv1\n</state>" };

		var stamp = await context.GetNewStampAsync(previous, CreateContext());

		Assert.NotNull(stamp);
		Assert.Equal("<state>\nv2\n</state>", stamp!.Snapshot);
	}

	[Fact]
	public async Task Supersede_EmptyOutput_EmitsNoStamp()
	{
		var context = new SupersedeScriptContextProvider(new FakeEngine(string.Empty), CreateAddon("state"));

		Assert.Null(await context.GetNewStampAsync(null, CreateContext()));
	}

	[Fact]
	public void Supersede_Render_ReturnsThePreRenderedSnapshot()
	{
		var context = new SupersedeScriptContextProvider(new FakeEngine("v1"), CreateAddon("state"));
		var stamp = new ScriptPromptContextStamp { Snapshot = "WRAPPED" };

		Assert.Equal("WRAPPED", context.Render(stamp));
		Assert.Equal("script:state", context.Discriminator);
	}
}
