using LLMDesktopAssistant.LLM.MVVM.Additional;

namespace LLMDesktopAssistant.Tests.MVVM
{
	public class AdditionalChatDataCollectionRestoreTests
	{
		private sealed class PlainData : AdditionalChatData
		{
		}

		private sealed class RestorablePart : AdditionalMessagePart
		{
		}

		private sealed class NonRestorablePart : AdditionalMessagePart
		{
			public NonRestorablePart() => IsRestorable = false;
		}

		[Fact]
		public void IsRestorable_DefaultsToTrue()
		{
			Assert.True(new RestorablePart().IsRestorable);
		}

		[Fact]
		public void GetRestorableParts_ReturnsRestorableMessagePartsOnly()
		{
			// RaiseInUIThread is disabled so the test does not touch the Avalonia dispatcher.
			var collection = new AdditionalChatDataCollection { RaiseInUIThread = false };
			collection.Add(new PlainData());        // not a message part -> never restored
			collection.Add(new RestorablePart());   // restorable by default
			collection.Add(new NonRestorablePart()); // explicitly opted out

			var restorable = collection.GetRestorableParts().ToList();

			var part = Assert.Single(restorable);
			Assert.IsType<RestorablePart>(part);
		}
	}
}
