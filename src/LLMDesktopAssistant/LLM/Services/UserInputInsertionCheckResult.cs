using LLMDesktopAssistant.Localization;

namespace LLMDesktopAssistant.LLM.Services
{
	/// <summary>
	/// The verdict of <see cref="IChatMessageInsertionService.CanInsertUserInput"/>: whether the input can be sent, and,
	/// when it cannot, the reason and the position of a syntax problem in the argument text.
	/// </summary>
	/// <param name="Success">Whether the input can be inserted.</param>
	/// <param name="Error">The blocking error, or <see langword="null"/> on success.</param>
	/// <param name="ErrorPosition">
	/// The offset into the argument text where a syntax error sits, or <c>-1</c> when the failure has no position (an
	/// unknown command, a missing or invalid argument).
	/// </param>
	public readonly record struct UserInputInsertionCheckResult(bool Success, LocaleKeyBase? Error, int ErrorPosition)
	{
		/// <summary>An accepting verdict.</summary>
		public static UserInputInsertionCheckResult Ok => new(true, null, -1);

		/// <summary>A refusing verdict.</summary>
		public static UserInputInsertionCheckResult Blocked(LocaleKeyBase error, int errorPosition = -1)
			=> new(false, error, errorPosition);
	}
}
