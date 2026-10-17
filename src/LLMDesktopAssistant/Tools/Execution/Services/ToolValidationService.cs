using LLMDesktopAssistant.Services;
using LLMDesktopAssistant.Tools.Execution.Domain;

namespace LLMDesktopAssistant.Tools.Execution.Services
{
	/// <summary>
	/// The stub validation stage: it forces the tolerant parse of the effective arguments and reports the
	/// set as valid.
	/// </summary>
	[Service(typeof(IToolValidationService))]
	public sealed class ToolValidationService : IToolValidationService
	{
		/// <inheritdoc/>
		public bool Validate(IToolExecutionContext ctx)
		{
			_ = ctx.Arguments.Effective;
			return true;
		}
	}
}
