using System.Collections.Immutable;
using LLMDesktopAssistant.Addons;
using LLMDesktopAssistant.Agents;
using LLMDesktopAssistant.Agents.SubAgents;
using LLMDesktopAssistant.Prompting.Skills;
using LLMDesktopAssistant.SlashCommands;
using LLMDesktopAssistant.SlashCommands.Execution;
using LLMDesktopAssistant.SlashCommands.Providers;

namespace LLMDesktopAssistant.Tests.SlashCommands
{
	/// <summary>
	/// The derived-command mapping of the skill and sub-agent providers.
	/// </summary>
	public class SlashCommandProviderTests
	{
		private sealed class FakeCollector<T>(IEnumerable<T> addons) : IAddonSetCollector<T>
		{
			public IEnumerable<T> GetAvailableAddons() => addons;

			public IEnumerable<T> GetAddonsForChat() => addons;

			public IEnumerable<T> GetAddonsForAgent(ChatAgentDescriptor agent) => addons;
		}

		private static AddonPackInfo Pack(string name) => new()
		{
			Name = name,
			Path = $"/packs/{name}",
			Source = AddonPackSource.Scanned,
			IsConfigurable = true
		};

		private static SkillInfo Skill(string name, AddonPackInfo? pack = null, AddonDiagnostic? diagnostic = null)
		{
			var skill = new SkillInfo
			{
				Name = name,
				Description = $"{name} description",
				Aliases = [$"alias-{name}"],
				Order = 7,
				Path = $"/skills/{name}/SKILL.md",
				AddonSource = AddonSource.Pack,
				SourcePack = pack
			};

			if (diagnostic is not null)
				skill.Diagnostic = diagnostic;

			return skill;
		}

		[Fact]
		public void SkillProvider_MapsTheSourceOntoTheCommand()
		{
			var pack = Pack("matt-pocock");
			var skill = Skill("grilling", pack);
			var provider = new SkillSlashCommandProvider(new FakeCollector<SkillInfo>([skill]));

			var command = Assert.Single(provider.GetCommands());

			Assert.Equal("grilling", command.Name);
			Assert.Equal("grilling description", command.Description);
			Assert.Equal(new[] { "alias-grilling" }, command.Aliases);
			Assert.Equal(7, command.Order);
			Assert.Equal(new[] { "skill", "matt-pocock" }, command.Namespaces);
			Assert.Equal(SlashCommandOrderTiers.Derived, command.OverrideOrder);
			Assert.Null(command.Enabled);
			Assert.Null(command.Hidden);
			Assert.Equal(ModelFacingMode.Raw, command.ModelFacingMode);
			Assert.Null(command.Generate);
			Assert.Same(StubCommandExecutor.Instance, command.Executor);

			// Provenance is carried over from the source.
			Assert.Same(pack, command.SourcePack);
			Assert.Equal("/skills/grilling/SKILL.md", command.Path);
			Assert.Equal(AddonSource.Pack, command.AddonSource);
			Assert.Same(skill, command.Source);
		}

		[Fact]
		public void Provider_WithoutAPack_HasATypeOnlyNamespace()
		{
			var provider = new SkillSlashCommandProvider(new FakeCollector<SkillInfo>([Skill("lonely")]));

			var command = Assert.Single(provider.GetCommands());

			Assert.Equal(new[] { "skill" }, command.Namespaces);
		}

		[Fact]
		public void Provider_DerivesOneCommandPerAvailableAddon()
		{
			var provider = new SkillSlashCommandProvider(new FakeCollector<SkillInfo>(
				[Skill("a"), Skill("b"), Skill("c")]));

			var names = provider.GetCommands().Select(c => c.Name).ToList();

			Assert.Equal(new[] { "a", "b", "c" }, names);
		}

		[Fact]
		public void Provider_SkipsInvalidAddons()
		{
			var provider = new SkillSlashCommandProvider(new FakeCollector<SkillInfo>(
			[
				Skill("broken", diagnostic: new AddonDiagnostic { IsFatal = true }),
				Skill("ok")
			]));

			var command = Assert.Single(provider.GetCommands());

			Assert.Equal("ok", command.Name);
		}

		[Fact]
		public void Provider_CommandsAreFrozen()
		{
			var provider = new SkillSlashCommandProvider(new FakeCollector<SkillInfo>([Skill("grilling")]));

			var command = Assert.Single(provider.GetCommands());

			Assert.True(command.IsFrozen);
			Assert.Throws<InvalidOperationException>(() => command.Name = "changed");
		}

		[Fact]
		public void SkillSchema_IsASingleRestPositional()
		{
			var provider = new SkillSlashCommandProvider(new FakeCollector<SkillInfo>([Skill("grilling")]));

			var schema = Assert.Single(provider.GetCommands()).ArgumentSchema!;

			Assert.True(schema.HasRestPositional);
			Assert.Empty(schema.Positionals);
			Assert.Empty(schema.Keyed);
		}

		[Fact]
		public void AgentSchema_IsARestPositionalPlusAnOptionalWait()
		{
			var subAgent = new SubAgentInfo { Name = "web-searcher", Description = "searches the web" };
			var provider = new SubAgentSlashCommandProvider(new FakeCollector<SubAgentInfo>([subAgent]));

			var command = Assert.Single(provider.GetCommands());
			var schema = command.ArgumentSchema!;

			Assert.Equal(new[] { "agent" }, command.Namespaces);
			Assert.True(schema.HasRestPositional);
			Assert.Empty(schema.Positionals);

			var wait = schema.Keyed["wait"];
			Assert.False(wait.Required);
			Assert.Equal("false", wait.Default);
			Assert.Equal("command.argument.wait", wait.Name.Key);
			Assert.Equal("command.argument.wait.description", wait.Description!.Key);
		}
	}
}
