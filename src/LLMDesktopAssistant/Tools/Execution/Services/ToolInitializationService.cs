using LLMDesktopAssistant.LLM.Domain;
using LLMDesktopAssistant.Services;
using LLMDesktopAssistant.Tools.Execution.Domain;

namespace LLMDesktopAssistant.Tools.Execution.Services
{
	[Service(typeof(IToolInitializationService))]
	public class ToolInitializationService : IToolInitializationService
	{
		/// <summary>
		/// Initializes the tool execution context.
		/// </summary>
		/// <param name="ctx">The invocation context.</param>
		public void Initialize(IToolExecutionContext ctx, ref CancellationToken ct)
		{
			var call = ctx.Call;

			call.ExpectedBehaviour = null;
			call.ResultContent = null;
			call.Status = ToolStatus.None;
			call.StatusIcon = null;
			call.StatusTitle = null;
			call.StructuredResult = null;
			call.UseMarkdown = false;
			call.ClearAttachments();

			call.CancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(ct);
			ct = call.CancellationTokenSource.Token;
		}
	}
}
