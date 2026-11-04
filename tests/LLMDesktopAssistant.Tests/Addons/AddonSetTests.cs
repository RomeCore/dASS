using System.Collections.Specialized;
using LLMDesktopAssistant.Addons;

namespace LLMDesktopAssistant.Tests.Addons
{
	/// <summary>
	/// The resolved addon set: the flat ordered list, the name/key lookup and the granular mutations.
	/// </summary>
	public class AddonSetTests
	{
		private sealed class Addon : AddonChangedBase<Addon, AddonChangeBase>
		{
			private string? _keyOverride;

			public Addon()
			{
			}

			public Addon(string name, int order = 0)
			{
				Name = name;
				Order = order;
			}

			public string? KeyOverride
			{
				get => _keyOverride;
				set => _keyOverride = value;
			}

			public override string Key => _keyOverride ?? base.Key;
		}

		private static AddonSet<Addon> Set(params Addon[] addons) => new(addons);

		private static List<NotifyCollectionChangedEventArgs> Record(AddonSet<Addon> set)
		{
			var events = new List<NotifyCollectionChangedEventArgs>();
			set.Addons.CollectionChanged += (_, e) => events.Add(e);
			return events;
		}

		[Fact]
		public void Upsert_NewItems_AreKeptInOrder()
		{
			var set = Set(new Addon("b", 2));

			set.Upsert(new Addon("a", 1));

			Assert.Equal(new[] { "a", "b" }, set.Addons.Select(addon => addon.Name));
			Assert.Equal(2, set.Count);
			Assert.Equal("a", set[0].Name);
		}

		[Fact]
		public void Upsert_SameKeyAndPosition_ReplacesInPlace()
		{
			var set = Set(new Addon("a", 1));
			var events = Record(set);
			var replacement = new Addon("a", 1);

			set.Upsert(replacement);

			Assert.Equal(1, set.Count);
			Assert.Same(replacement, set[0]);
			var raised = Assert.Single(events);
			Assert.Equal(NotifyCollectionChangedAction.Replace, raised.Action);
			Assert.Equal("a", set.GetByName("a")!.Name);
		}

		[Fact]
		public void Upsert_SameInstance_IsSilent()
		{
			var addon = new Addon("a");
			var set = Set(addon);
			var events = Record(set);

			set.Upsert(addon);

			Assert.Empty(events);
		}

		[Fact]
		public void Upsert_ChangedOrder_MovesTheItem()
		{
			var set = Set(new Addon("a", 1), new Addon("b", 2));

			set.Upsert(new Addon("a", 3));

			Assert.Equal(new[] { "b", "a" }, set.Addons.Select(addon => addon.Name));
		}

		[Fact]
		public void Remove_ForgetsTheItemAndItsLookup()
		{
			var set = Set(new Addon("a"));

			set.Remove("a");

			Assert.Empty(set);
			Assert.Null(set.GetByName("a"));
			Assert.Null(set.GetByKey("a"));
		}

		[Fact]
		public void Clear_EmptiesTheSetAndLookup()
		{
			var set = Set(new Addon("a"));

			set.Clear();

			Assert.Empty(set);
			Assert.Null(set.GetByName("a"));
		}

		[Fact]
		public void Reset_SortsAndRaisesOneChange()
		{
			var set = new AddonSet<Addon>();
			var events = Record(set);

			set.Reset([new Addon("c", 3), new Addon("a", 1), new Addon("b", 2)]);

			Assert.Equal(new[] { "a", "b", "c" }, set.Addons.Select(addon => addon.Name));
			var raised = Assert.Single(events);
			Assert.Equal(NotifyCollectionChangedAction.Add, raised.Action);
		}

		[Fact]
		public void GetByName_IsCaseInsensitive()
		{
			var addon = new Addon("Alpha");
			var set = Set(addon);

			Assert.Same(addon, set.GetByName("alpha"));
			Assert.Same(addon, set.GetByName("ALPHA"));
		}

		[Fact]
		public void GetByName_PrefersTheNameOverAnAlias()
		{
			var named = new Addon("x");
			var aliased = new Addon("y") { Aliases = ["x"] };
			var set = Set(aliased, named);

			Assert.Same(named, set.GetByName("x"));
		}

		[Fact]
		public void GetByName_FallsBackToAnAlias()
		{
			var aliased = new Addon("y") { Aliases = ["x"] };

			Assert.Same(aliased, Set(aliased).GetByName("x"));
		}

		[Fact]
		public void GetByName_AliasTiesAreBrokenByOverrideOrder()
		{
			var low = new Addon("low") { Aliases = ["x"], OverrideOrder = 0 };
			var high = new Addon("high") { Aliases = ["x"], OverrideOrder = 1 };
			var set = Set(low, high);

			Assert.Same(high, set.GetByName("x"));
		}

		[Fact]
		public void GetByKey_IsOrdinal()
		{
			var addon = new Addon("a") { KeyOverride = "A" };
			var set = Set(addon);

			Assert.Same(addon, set.GetByKey("A"));
			Assert.Null(set.GetByKey("a"));
		}

		[Fact]
		public void RemovingTheNameOwner_RevealsTheShadowedAlias()
		{
			var named = new Addon("x");
			var aliased = new Addon("y") { Aliases = ["x"] };
			var set = Set(named, aliased);

			set.Remove("x");

			Assert.Same(aliased, set.GetByName("x"));
		}

		[Fact]
		public void RemovingAnAliasOwner_ForgetsTheAlias()
		{
			var aliased = new Addon("y") { Aliases = ["x"] };
			var set = Set(aliased);

			set.Remove("y");

			Assert.Null(set.GetByName("x"));
		}
	}
}
