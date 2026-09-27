using LLMDesktopAssistant.Agents;
using LLMDesktopAssistant.LLM.Services.Prompting;
using LLMDesktopAssistant.Prompting.Context;
using LLMDesktopAssistant.Prompting.Context.Providers.Skills;

namespace LLMDesktopAssistant.Tests.Prompting;

/// <summary>
/// Tests for the skills section delta (built on the common addon delta engine):
/// description, path and body changes.
/// </summary>
[Collection("Prompting")]
public class SkillsDeltaProviderTests
{
	private sealed class FakeStateProvider : IPromptSectionStateProvider<SkillsSectionState>
	{
		public SkillsSectionState? State { get; init; }

		public SkillsSectionState? CaptureState(ChatAgentDescriptor agent) => State;
	}

	private static SkillsDeltaProvider CreateProvider(SkillsSectionState? state)
		=> new(new FakeStateProvider { State = state });

	private static EffectiveChatContext CreateContext() => new()
	{
		Agent = new ChatAgentDescriptor(),
		Messages = [],
		Checkpoints = [],
		EffectiveMessagesStartIndex = 0,
		LastCutIndex = -1,
		LastCheckpointIndex = -1
	};

	private static SkillItem Skill(string name,
		string description = "description",
		string? path = "path",
		string? body = null) => new()
	{
		Name = name,
		Description = description,
		Path = path,
		Body = body
	};

	private static SkillsSectionState State(params SkillItem[] skills)
		=> new() { Items = [.. skills] };

	[Fact]
	public void NothingChanged_ReturnsNull()
	{
		var provider = CreateProvider(State(Skill("code-review")));

		var delta = provider.CalculateDelta(State(Skill("code-review")), [], CreateContext());

		Assert.Null(delta);
	}

	[Fact]
	public void ChangedPath_IsReported()
	{
		var provider = CreateProvider(State(Skill("code-review", path: "new/path.md")));

		var delta = provider.CalculateDelta(State(Skill("code-review", path: "old/path.md")), [], CreateContext());

		Assert.NotNull(delta);
		var updated = Assert.Single(delta!.UpdatedItems);
		Assert.True(updated.Changes!.PathChanged);
		Assert.Equal("new/path.md", updated.Changes.NewPath);
		Assert.False(updated.Changes.DescriptionChanged);
		Assert.False(updated.Changes.BodyChanged);
	}

	[Fact]
	public void ChangedBody_IsReported()
	{
		var provider = CreateProvider(State(Skill("code-review", body: "new body")));

		var delta = provider.CalculateDelta(State(Skill("code-review", body: "old body")), [], CreateContext());

		Assert.NotNull(delta);
		var updated = Assert.Single(delta!.UpdatedItems);
		Assert.True(updated.Changes!.BodyChanged);
		Assert.Equal("new body", updated.Changes.NewBody);
	}

	[Fact]
	public void NewSkill_CarriesFullDefinition()
	{
		var provider = CreateProvider(State(Skill("code-review")));

		var delta = provider.CalculateDelta(State(), [], CreateContext());

		Assert.NotNull(delta);
		var added = Assert.Single(delta!.AddedItems);
		Assert.Equal("code-review", added.Name);
		Assert.NotNull(added.Definition);
		Assert.Equal("path", added.Definition!.Path);
		Assert.Empty(delta.RemovedItems);
	}
}
