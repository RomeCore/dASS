using System.Collections.Specialized;
using System.ComponentModel;
using LLMDesktopAssistant.Addons;
using LLMDesktopAssistant.Addons.Sources;
using LLMDesktopAssistant.Agents;
using LLMDesktopAssistant.Utils;
using Microsoft.Extensions.DependencyInjection;

namespace LLMDesktopAssistant.Tests.Addons
{
	/// <summary>
	/// The collector cache: agent-scoped identity, incremental patching from the accessor, the change bag,
	/// per-change edits, defaults, the gate and the availability predicates.
	/// </summary>
	public class AddonSetCollectorCacheTests
	{
		private sealed class TestChange : AddonChangeBase
		{
		}

		private sealed class TestConfig : AddonSetConfigurationBase<TestChange>
		{
		}

		private sealed class TestAddon : AddonChangedBase<TestAddon, TestChange>
		{
			public TestAddon()
			{
			}

			public TestAddon(string name)
			{
				Name = name;
			}
		}

		private sealed class TestGate : IAddonCategorySettings
		{
			private bool _enabled = true;

			public bool AddonsCategoryEnabled
			{
				get => _enabled;
				set
				{
					if (_enabled == value)
						return;
					_enabled = value;
					PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(AddonsCategoryEnabled)));
				}
			}

			public event PropertyChangedEventHandler? PropertyChanged;
		}

		private sealed class TestAccessor : IFileAddonAccessor<TestAddon>
		{
			public RangeObservableCollection<TestAddon> Items { get; } = [];

			public ReadOnlyObservableCollection<TestAddon> Addons { get; }

			public TestAccessor(params TestAddon[] addons)
			{
				Items.AddRange(addons);
				Addons = new(Items);
			}
		}

		private sealed class TestSource : IAdditionalAddonSource<TestAddon>
		{
			public RangeObservableCollection<TestAddon> Items { get; } = [];

			public ReadOnlyObservableCollection<TestAddon> Addons { get; }

			public TestSource()
			{
				Addons = new(Items);
			}

			public bool IsStatic => false;
		}

		private sealed class TestCollector : AddonSetCollectorBase<TestAddon, TestChange>
		{
			public TestCollector(IServiceProvider services) : base(services)
			{
			}

			public TestConfig ChatConfig { get; set; } = new();

			public TestConfig AgentConfig { get; set; } = new();

			public TestGate ChatGate { get; } = new();

			public TestGate AgentGate { get; } = new();

			public int ApplyCalls { get; private set; }

			protected override AddonTarget ResolveChatTarget() => new(ChatConfig, ChatGate);

			protected override AddonTarget ResolveAgentTarget(ChatAgentDescriptor agent) => new(AgentConfig, AgentGate);

			protected override void ApplyChange(TestAddon target, TestChange change, ChatAgentDescriptor? agent)
				=> ApplyCalls++;
		}

		private sealed class Fixture
		{
			public TestAccessor Accessor { get; }

			public TestSource Source { get; } = new();

			public TestCollector Collector { get; }

			public Fixture(params TestAddon[] addons)
			{
				Accessor = new TestAccessor(addons);

				var services = new ServiceCollection();
				services.AddSingleton<IFileAddonAccessor<TestAddon>>(Accessor);
				services.AddSingleton<IAdditionalAddonSource<TestAddon>>(Source);
				Collector = new TestCollector(services.BuildServiceProvider());
			}
		}

		private static ChatAgentDescriptor Agent() => new();

		private static List<NotifyCollectionChangedEventArgs> Record(AddonSet<TestAddon> set)
		{
			var events = new List<NotifyCollectionChangedEventArgs>();
			set.Addons.CollectionChanged += (_, e) => events.Add(e);
			return events;
		}

		[Fact]
		public void AgentSet_IsCachedPerAgent_AndSilentOnRepeat()
		{
			var fixture = new Fixture(new TestAddon("a"), new TestAddon("b"));
			var agent = Agent();
			var first = fixture.Collector.GetAddonsForAgent(agent);
			var events = Record(first);

			var second = fixture.Collector.GetAddonsForAgent(agent);

			Assert.Same(first, second);
			Assert.Empty(events);
			Assert.Equal(2, first.Count);
			Assert.All(first, addon => Assert.True(addon.IsFrozen));
		}

		[Fact]
		public void AgentSet_ContainsClonesOfTheAvailableAddons()
		{
			var fixture = new Fixture(new TestAddon("a"), new TestAddon("b"));
			var available = fixture.Collector.GetAvailableAddons();

			var set = fixture.Collector.GetAddonsForAgent(Agent());

			Assert.Equal(2, set.Count);
			Assert.All(set, addon => Assert.DoesNotContain(available, candidate => ReferenceEquals(candidate, addon)));
			Assert.All(set, addon => Assert.Null(addon.Change));
			Assert.All(set, addon => Assert.True(addon.Enabled));
		}

		[Fact]
		public void Change_DisablesOnlyThatAddon_AndReenablesIt()
		{
			var fixture = new Fixture(new TestAddon("a"), new TestAddon("b"));
			var set = fixture.Collector.GetAddonsForAgent(Agent());
			var untouched = set.GetByName("b");

			fixture.Collector.AgentConfig.Changes["a"] = new TestChange { Enabled = false };

			Assert.Equal(1, set.Count);
			Assert.Null(set.GetByName("a"));
			Assert.Same(untouched, set.GetByName("b"));

			fixture.Collector.AgentConfig.Changes["a"] = new TestChange { Enabled = true };

			Assert.Equal(2, set.Count);
			Assert.NotNull(set.GetByName("a"));
			Assert.Equal(1, fixture.Collector.ApplyCalls);
		}

		[Fact]
		public void EditingAChangeProperty_RebuildsThatAddon()
		{
			var fixture = new Fixture(new TestAddon("a"));
			var change = new TestChange { Enabled = true };
			fixture.Collector.AgentConfig.Changes["a"] = change;
			var set = fixture.Collector.GetAddonsForAgent(Agent());
			Assert.Single(set);

			change.Enabled = false;

			Assert.Empty(set);
		}

		[Fact]
		public void AccessorAdd_ReachesTheAgentSet()
		{
			var fixture = new Fixture(new TestAddon("a"));
			var set = fixture.Collector.GetAddonsForAgent(Agent());
			var events = Record(set);

			fixture.Accessor.Items.Add(new TestAddon("b"));

			Assert.Equal(2, set.Count);
			Assert.NotNull(set.GetByName("b"));
			var raised = Assert.Single(events);
			Assert.Equal(NotifyCollectionChangedAction.Add, raised.Action);
		}

		[Fact]
		public void AccessorRemove_ReachesTheAgentSet()
		{
			var a = new TestAddon("a");
			var fixture = new Fixture(a, new TestAddon("b"));
			var set = fixture.Collector.GetAddonsForAgent(Agent());

			fixture.Accessor.Items.Remove(a);

			Assert.Equal(1, set.Count);
			Assert.Null(set.GetByName("a"));
			Assert.Null(set.GetByKey("a"));
		}

		[Fact]
		public void SourceAdd_ReachesTheAgentSet()
		{
			var fixture = new Fixture(new TestAddon("a"));
			var set = fixture.Collector.GetAddonsForAgent(Agent());

			fixture.Source.Items.Add(new TestAddon("s"));

			Assert.Equal(2, set.Count);
			Assert.NotNull(set.GetByName("s"));
		}

		[Fact]
		public void PredicateToggledBetweenCalls_ChangesMembershipWithoutCloning()
		{
			var enabled = true;
			var addon = new TestAddon("a") { ChatAvailablePredicate = (_, _) => enabled };
			var fixture = new Fixture(addon);
			var agent = Agent();
			var set = fixture.Collector.GetAddonsForAgent(agent);
			var clone = set.GetByName("a");

			enabled = false;
			fixture.Collector.GetAddonsForAgent(agent);
			Assert.Empty(set);

			enabled = true;
			fixture.Collector.GetAddonsForAgent(agent);
			Assert.Same(clone, set.GetByName("a"));
		}

		[Fact]
		public void Gate_DisablesAndRefillsWithoutCloning()
		{
			var fixture = new Fixture(new TestAddon("a"));
			var agent = Agent();
			var set = fixture.Collector.GetAddonsForAgent(agent);
			var clone = set.GetByName("a");

			fixture.Collector.AgentGate.AddonsCategoryEnabled = false;
			Assert.Empty(set);

			fixture.Collector.AgentGate.AddonsCategoryEnabled = true;
			Assert.Same(clone, set.GetByName("a"));
			Assert.Equal(0, fixture.Collector.ApplyCalls);
		}

		[Fact]
		public void AgentPredicate_DoesNotLeakBetweenAgents()
		{
			var blocked = Agent();
			var addon = new TestAddon("a")
			{
				AgentAvailablePredicate = (_, _, agent) => agent.Id != blocked.Id
			};
			var fixture = new Fixture(addon);

			var allowedSet = fixture.Collector.GetAddonsForAgent(Agent());
			var blockedSet = fixture.Collector.GetAddonsForAgent(blocked);

			Assert.Single(allowedSet);
			Assert.Empty(blockedSet);
			Assert.NotSame(allowedSet, blockedSet);
		}

		[Fact]
		public void ConfigSwitch_RebuildsWithOneChange()
		{
			var fixture = new Fixture(new TestAddon("a"), new TestAddon("b"));
			var agent = Agent();
			var set = fixture.Collector.GetAddonsForAgent(agent);
			var events = Record(set);

			fixture.Collector.AgentConfig = new TestConfig();
			fixture.Collector.GetAddonsForAgent(agent);

			var raised = Assert.Single(events);
			Assert.Equal(NotifyCollectionChangedAction.Replace, raised.Action);
			Assert.Equal(2, set.Count);
		}

		[Fact]
		public void EnabledByDefault_AffectsOnlyImplicitAddons()
		{
			var fixture = new Fixture(new TestAddon("implicit"), new TestAddon("explicit"));
			var config = fixture.Collector.AgentConfig;
			config.EnabledByDefault = false;
			config.Changes["explicit"] = new TestChange { Enabled = true };

			var set = fixture.Collector.GetAddonsForAgent(Agent());
			Assert.Single(set);
			Assert.Equal("explicit", set[0].Name);
			var applyCalls = fixture.Collector.ApplyCalls;

			config.EnabledByDefault = true;

			Assert.Equal(2, set.Count);
			Assert.Equal(applyCalls, fixture.Collector.ApplyCalls);
		}

		[Fact]
		public void ChatSet_UsesTheChatGate()
		{
			var fixture = new Fixture(new TestAddon("a"));
			var set = fixture.Collector.GetAddonsForChat();
			Assert.Single(set);

			fixture.Collector.ChatGate.AddonsCategoryEnabled = false;

			Assert.Empty(set);
		}

		[Fact]
		public void AvailableSet_IgnoresPredicatesAndConfiguration()
		{
			var addon = new TestAddon("a")
			{
				ChatAvailablePredicate = (_, _) => false,
				Enabled = false
			};
			var fixture = new Fixture(addon);

			var available = fixture.Collector.GetAvailableAddons();

			Assert.Single(available);
			Assert.Same(addon, available[0]);
		}

		[Fact]
		public void SourceReset_RemovesKeysThatDisappear()
		{
			var fixture = new Fixture(new TestAddon("a"));
			fixture.Source.Items.Add(new TestAddon("s"));
			var set = fixture.Collector.GetAddonsForAgent(Agent());
			Assert.Equal(2, set.Count);

			fixture.Source.Items.PreferResetForRangeOperations = true;
			fixture.Source.Items.Clear();

			Assert.Single(set);
			Assert.Null(set.GetByName("s"));
			Assert.NotNull(set.GetByName("a"));
		}
	}
}
