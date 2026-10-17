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
		/// <exception cref="ToolArgumentsInvalidException">
		/// The effective arguments do not satisfy the tool's schema. The message is model-facing: the
		/// settlement stage writes it into the call verbatim.
		/// </exception>
		void Validate(IToolExecutionContext ctx);
	}
}
