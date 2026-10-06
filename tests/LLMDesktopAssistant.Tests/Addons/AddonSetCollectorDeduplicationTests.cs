using LLMDesktopAssistant.Addons;
using LLMDesktopAssistant.Utils;

namespace LLMDesktopAssistant.Tests.Addons
{
	public class AddonSetCollectorDeduplicationTests
	{
		private sealed class TestAddon : AddonChangedBase<TestAddon, AddonChangeBase>
		{
			public string Key
			{
				get => field ??= string.Empty;
				set => SetProperty(ref field, value);
			}
		}

		private sealed class DefaultCollector(IServiceProvider services)
			: AddonSetCollectorBase<TestAddon, AddonChangeBase>(services)
		{
		}

		private sealed class KeyedCollector(IServiceProvider services)
			: AddonSetCollectorBase<TestAddon, AddonChangeBase>(services)
		{
			// A composite key - the name is only unique together with the extra key.
			protected override object GetDeduplicationKey(TestAddon addon) => (addon.Name, addon.Key);
		}

		private sealed class FakeServiceProvider(IAddonAccessor<TestAddon> accessor) : IServiceProvider
		{
			public object? GetService(Type serviceType) =>
				serviceType == typeof(IAddonAccessor<TestAddon>) ? accessor : null;
		}

		private sealed class FakeAccessor(IEnumerable<TestAddon> addons) : IAddonAccessor<TestAddon>
		{
			public ReadOnlyObservableCollection<TestAddon> Addons { get; } = new(addons.ToList());
		}

		private static TestAddon Addon(string name, int overrideOrder = 0, string key = "") => new()
		{
			Name = name,
			OverrideOrder = overrideOrder,
			Key = key
		};

		[Fact]
		public void DefaultKey_GroupsByName()
		{
			var collector = new DefaultCollector(new FakeServiceProvider(new FakeAccessor(
			[
				Addon("shared", overrideOrder: 1),
				Addon("shared", overrideOrder: 0)
			])));

			var result = collector.GetAvailableAddons().ToList();

			var addon = Assert.Single(result);
			Assert.Equal("shared", addon.Name);
			Assert.Equal(1, addon.OverrideOrder);

			var loser = Assert.Single(addon.Overrides);
			Assert.Equal(0, loser.OverrideOrder);
		}

		[Fact]
		public void OverriddenKey_UsesCustomKey()
		{
			var collector = new KeyedCollector(new FakeServiceProvider(new FakeAccessor(
			[
				Addon("shared", overrideOrder: 0, key: "a"),
				Addon("shared", overrideOrder: 1, key: "a"),
				Addon("shared", overrideOrder: 0, key: "b")
			])));

			var result = collector.GetAvailableAddons().ToList();

			// (shared, a) and (shared, b) are two distinct addons; the two (shared, a) collapse.
			Assert.Equal(2, result.Count);

			var groupA = Assert.Single(result, addon => addon.Key == "a");
			Assert.Equal(1, groupA.OverrideOrder);
			var loser = Assert.Single(groupA.Overrides);
			Assert.Equal(0, loser.OverrideOrder);

			var groupB = Assert.Single(result, addon => addon.Key == "b");
			Assert.Empty(groupB.Overrides);
		}
	}
}
