using LLMDesktopAssistant.LLM.MVVM.Additional;
using LLMDesktopAssistant.Utils;

namespace LLMDesktopAssistant.Scripting.Lua.Scheduling
{
	/// <summary>
	/// Holds the last firing time of every <c>catch_slot</c> of one chat.
	/// </summary>
	/// <remarks>
	/// Data-only and invisible: the row exists so that a cron job can tell how many occurrences passed
	/// while the chat was closed. LiteDB stores it as a plain document, so the row needs no polymorphism
	/// label.
	/// </remarks>
	public class ScheduleSlotChatData : AdditionalChatData
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="ScheduleSlotChatData"/> class.
		/// Used by the database loader.
		/// </summary>
		public ScheduleSlotChatData()
		{
			IsVisible = false;
		}

		/// <summary>
		/// Gets or sets the last firing time per slot key.
		/// </summary>
		public ObservableDictionary<string, DateTime> LastShots
		{
			get => field ??= [];
			set => (field ??= []).Reset(value);
		}
	}
}
