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
		public void Validate(IToolExecutionContext ctx)
		{
			// The tolerant parse is materialized here; the schema check lands with the real validator.
			_ = ctx.Arguments.Effective;
		}
	}
}
