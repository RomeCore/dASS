using LLMDesktopAssistant.Tools.Execution.Domain;

namespace LLMDesktopAssistant.Tools.Execution.Services
{
	/// <summary>
	/// The validation stage: materializes the effective arguments and decides whether the invocation may
	/// run.
	/// </summary>
	public interface IToolValidationService
	{
		/// <summary>
		/// Validates the invocation's effective arguments.
		/// </summary>
		/// <param name="ctx">The invocation.</param>
		/// <returns><see langword="true"/> when the invocation may proceed.</returns>
		bool Validate(IToolExecutionContext ctx);
	}
}
