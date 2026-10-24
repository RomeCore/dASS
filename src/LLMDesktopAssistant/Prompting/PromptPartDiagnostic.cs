namespace LLMDesktopAssistant.Prompting
{
	public class PromptPartDiagnostic
	{
		public required bool IsFatal { get; init; }

		public required PromptPartDiagnosticCode Code { get; init; }

		public ImmutableList<string> Messages { get; init; } = [];

		public Exception? Exception { get; init; } = null;

		public static PromptPartDiagnostic? Combine(PromptPartDiagnostic? first, PromptPartDiagnostic? second)
		{
			if (first == null)
				return second;
			if (second == null)
				return first;

			return new PromptPartDiagnostic
			{
				IsFatal = first.IsFatal || second.IsFatal,
				Code = first.Code | second.Code,
				Messages = [.. first.Messages, .. second.Messages],
				Exception = second.Exception ?? first.Exception
			};
		}
	}
}