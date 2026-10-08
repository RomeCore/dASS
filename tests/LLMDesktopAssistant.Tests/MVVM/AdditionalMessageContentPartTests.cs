using LLMDesktopAssistant.Data;
using LLMDesktopAssistant.Data.ChatModels;
using LLMDesktopAssistant.LLM.MVVM.Additional;
using LLMDesktopAssistant.LLM.Services.Storage;
using LLMDesktopAssistant.Localization;

namespace LLMDesktopAssistant.Tests.MVVM
{
	/// <summary>
	/// The command-injected content part: its text and its (now localized) chip title survive the persistence
	/// round-trip.
	/// </summary>
	public class AdditionalMessageContentPartTests
	{
		[Fact]
		public void ContentPart_RoundTripsThroughTheAdditionalDataSynchronizer()
		{
			using var database = new ChatDatabase(null);
			var part = new AdditionalMessageContentPart
			{
				Content = "the skill body",
				ChipTitle = Locale.GetConstKey("Used skill 'grilling'"),
				IsRestorable = false
			};

			using var synchronizer = AdditionalChatDataSynchronizer.FromTarget(
				database, part, ChatDataParentKind.Message, 1);

			var stored = Assert.IsType<AdditionalMessageContentPart>(
				database.AdditionalChatData.FindAll().Single().Data);

			Assert.Equal("the skill body", stored.Content);
			Assert.NotNull(stored.ChipTitle);
			Assert.Equal("Used skill 'grilling'", stored.ChipTitle!.Value);
			Assert.False(stored.IsRestorable);
		}
	}
}
