using LLMDesktopAssistant.Addons;
using LLMDesktopAssistant.Utils;

namespace LLMDesktopAssistant.Tests.Addons
{
	public class AddonSetCollectorDeduplicationTests
	{
		private class TestAddon : AddonChangedBase<TestAddon, AddonChangeBase>
		{
		}

		private sealed class GroupedAddon : AddonChangedBase<GroupedAddon, AddonChangeBase>
		{
			public string Group
			{
				get => field ??= string.Empty;
				set => SetProperty(ref field, value);
			}

			// The name is only unique together with the extra group.
			public override string Key => $"{Name}:{Group}";
		}

		private sealed class DefaultCollector(IServiceProvider services)
			: AddonSetCollectorBase<TestAddon, AddonChangeBase>(services)
		{
		}

		private sealed class GroupedCollector(IServiceProvider services)
			: AddonSetCollectorBase<GroupedAddon, AddonChangeBase>(services)
		{
		}

		private sealed class FakeServiceProvider<TAddon>(IAddonAccessor<TAddon> accessor) : IServiceProvider
		{
			public object? GetService(Type serviceType) =>
				serviceType == typeof(IAddonAccessor<TAddon>) ? accessor : null;
		}

		private sealed class FakeAccessor<TAddon>(IEnumerable<TAddon> addons) : IAddonAccessor<TAddon>
		{
			public ReadOnlyObservableCollection<TAddon> Addons { get; } = new(addons.ToList());
		}

		[Fact]
		public void DefaultKey_GroupsByName()
		{
			var collector = new DefaultCollector(new FakeServiceProvider<TestAddon>(new FakeAccessor<TestAddon>(
			[
				new TestAddon { Name = "shared", OverrideOrder = 1 },
				new TestAddon { Name = "shared", OverrideOrder = 0 }
			])));

			var result = collector.GetAvailableAddons().ToList();

			var addon = Assert.Single(result);
			Assert.Equal("shared", addon.Name);
			Assert.Equal(1, addon.OverrideOrder);

			var loser = Assert.Single(addon.Overrides);
			Assert.Equal(0, loser.OverrideOrder);
		}

		[Fact]
		public void OverriddenKey_UsesTheAddonKey()
		{
			var collector = new GroupedCollector(new FakeServiceProvider<GroupedAddon>(new FakeAccessor<GroupedAddon>(
			[
				new GroupedAddon { Name = "shared", OverrideOrder = 0, Group = "a" },
				new GroupedAddon { Name = "shared", OverrideOrder = 1, Group = "a" },
				new GroupedAddon { Name = "shared", OverrideOrder = 0, Group = "b" }
			])));

			var result = collector.GetAvailableAddons().ToList();

			// "shared:a" and "shared:b" are two distinct addons; the two "shared:a" collapse.
			Assert.Equal(2, result.Count);

			var groupA = Assert.Single(result, addon => addon.Group == "a");
			Assert.Equal(1, groupA.OverrideOrder);
			var loser = Assert.Single(groupA.Overrides);
			Assert.Equal(0, loser.OverrideOrder);

			var groupB = Assert.Single(result, addon => addon.Group == "b");
			Assert.Empty(groupB.Overrides);
		}
	}
}
