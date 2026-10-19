using LLMDesktopAssistant.Tools.Execution.Domain;

namespace LLMDesktopAssistant.Tools.Execution.Services
{
	public interface IToolInitializationService
	{
		void Initialize(IToolExecutionContext ctx, ref CancellationToken ct);
	}
}