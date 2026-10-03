using System.Collections.Specialized;
using LLMDesktopAssistant.Utils;

namespace LLMDesktopAssistant.Tests.Utils
{
	public class FilteredObservableCollectionTests
	{
		private static FilteredObservableCollection<int> Even(RangeObservableCollection<int> source)
			=> new(source, x => x % 2 == 0);

		[Fact]
		public void InitialState_IsFiltered()
		{
			var source = new RangeObservableCollection<int> { 1, 2, 3, 4 };

			var view = Even(source);

			Assert.Equal(new[] { 2, 4 }, view.ToArray());
			Assert.Equal(2, view.Count);
			Assert.True(view.Any);
			Assert.True(view.IsReadOnly);
		}

		[Fact]
		public void AddMatching_RaisesGranularAdd()
		{
			var source = new RangeObservableCollection<int>();
			var view = Even(source);
			var events = new List<NotifyCollectionChangedEventArgs>();
			view.CollectionChanged += (s, e) => events.Add(e);

			source.Add(2);

			Assert.Equal(new[] { 2 }, view.ToArray());
			var e = Assert.Single(events);
			Assert.Equal(NotifyCollectionChangedAction.Add, e.Action);
			Assert.Equal(2, e.NewItems![0]);
			Assert.Equal(0, e.NewStartingIndex);
		}

		[Fact]
		public void AddNonMatching_RaisesNothing()
		{
			var source = new RangeObservableCollection<int>();
			var view = Even(source);
			int count = 0;
			view.CollectionChanged += (s, e) => count++;

			source.Add(1);

			Assert.Empty(view);
			Assert.Equal(0, count);
		}

		[Fact]
		public void RemoveMatching_RaisesRemoveWithCorrectIndex()
		{
			var source = new RangeObservableCollection<int> { 2, 4, 6 };
			var view = Even(source);
			var events = new List<NotifyCollectionChangedEventArgs>();
			view.CollectionChanged += (s, e) => events.Add(e);

			source.RemoveAt(1); // removes 4

			Assert.Equal(new[] { 2, 6 }, view.ToArray());
			var e = Assert.Single(events);
			Assert.Equal(NotifyCollectionChangedAction.Remove, e.Action);
			Assert.Equal(4, e.OldItems![0]);
			Assert.Equal(1, e.OldStartingIndex);
		}

		[Fact]
		public void SourceReset_ProducesTargetedDeltas_NeverReset()
		{
			var source = new RangeObservableCollection<int> { 2, 4 };
			var view = Even(source);
			var actions = new List<NotifyCollectionChangedAction>();
			view.CollectionChanged += (s, e) => actions.Add(e.Action);

			source.PreferResetForRangeOperations = true;
			source.Reset(new[] { 2, 4, 6, 8 }); // raises Reset on the source

			Assert.Equal(new[] { 2, 4, 6, 8 }, view.ToArray());
			Assert.DoesNotContain(NotifyCollectionChangedAction.Reset, actions);
			Assert.All(actions, a => Assert.Equal(NotifyCollectionChangedAction.Add, a));
		}

		[Fact]
		public void SourceClear_RemovesEverything()
		{
			var source = new RangeObservableCollection<int> { 1, 2, 3, 4, 5, 6 };
			var view = Even(source);
			var actions = new List<NotifyCollectionChangedAction>();
			view.CollectionChanged += (s, e) => actions.Add(e.Action);

			source.Clear();

			Assert.Empty(view);
			Assert.Equal(3, actions.Count);
			Assert.All(actions, a => Assert.Equal(NotifyCollectionChangedAction.Remove, a));
		}

		[Fact]
		public void Refresh_ReevaluatesMembership_NoAutoSubscription()
		{
			var box = new Box { Value = 2 };
			var source = new RangeObservableCollection<Box> { box };
			var view = new FilteredObservableCollection<Box>(source, x => x.Value % 2 == 0);
			Assert.Single(view);

			box.Value = 3; // no collection change, no auto-reaction

			Assert.Single(view);

			view.Refresh();

			Assert.Empty(view);
		}

		[Fact]
		public void Refresh_WithoutChanges_RaisesNothing()
		{
			var source = new RangeObservableCollection<int> { 2, 4 };
			var view = Even(source);
			int count = 0;
			view.CollectionChanged += (s, e) => count++;

			view.Refresh();

			Assert.Equal(0, count);
		}

		[Fact]
		public void Chain_FiltersCompose()
		{
			var source = new RangeObservableCollection<int>();

			var evens = source.Filter(x => x % 2 == 0);
			var bigEvens = evens.Filter(x => x > 4);

			source.AddRange(new[] { 2, 4, 6, 8 });
			Assert.Equal(new[] { 6, 8 }, bigEvens.ToArray());

			source.Add(10);
			Assert.Equal(new[] { 6, 8, 10 }, bigEvens.ToArray());

			source.Add(3);
			Assert.Equal(new[] { 6, 8, 10 }, bigEvens.ToArray());
		}

		[Fact]
		public void Dispose_StopsUpdates_AndFreezes()
		{
			var source = new RangeObservableCollection<int>();
			var view = Even(source);
			int count = 0;
			view.CollectionChanged += (s, e) => count++;

			view.Dispose();
			source.Add(2);

			Assert.Equal(0, count);
			Assert.Empty(view); // frozen at the last state before dispose
		}

		[Fact]
		public void Mutators_Throw()
		{
			var source = new RangeObservableCollection<int> { 2 };
			var view = Even(source);

			Assert.Throws<InvalidOperationException>(() => ((IList<int>)view).Add(4));
			Assert.Throws<InvalidOperationException>(() => ((IList<int>)view).Insert(0, 4));
			Assert.Throws<InvalidOperationException>(() => ((IList<int>)view).Remove(2));
			Assert.Throws<InvalidOperationException>(() => ((IList<int>)view).RemoveAt(0));
			Assert.Throws<InvalidOperationException>(() => ((IList<int>)view).Clear());
			Assert.Throws<InvalidOperationException>(() => view[0] = 4);
		}

		[Fact]
		public void Constructor_Throws_WhenSourceIsNotObservable()
		{
			Assert.Throws<ArgumentException>(() => new FilteredObservableCollection<int>(new List<int>(), x => true));
		}

		[Fact]
		public void Constructor_Throws_WhenPredicateIsNull()
		{
			var source = new RangeObservableCollection<int>();
			Assert.Throws<ArgumentNullException>(() => new FilteredObservableCollection<int>(source, null!));
		}

		private class Box
		{
			public int Value { get; set; }
		}
	}
}
