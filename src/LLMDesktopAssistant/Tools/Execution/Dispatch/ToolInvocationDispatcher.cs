using LLMDesktopAssistant.Services;
using LLMDesktopAssistant.Tools.Execution.Contexts;
using LLMDesktopAssistant.Tools.Execution.Domain;
using LLMDesktopAssistant.Tools.Execution.Orchestrators;

namespace LLMDesktopAssistant.Tools.Execution.Dispatch
{
	/// <summary>
	/// The single tool invocation entry point: substitutes the missing-tool descriptor, builds the context
	/// through the request's factory and routes the invocation to its orchestrator.
	/// </summary>
	/// <remarks>
	/// Application-scoped and stateless on purpose. The chat-scoped pieces a chat invocation needs (the
	/// descriptor's context factory, the confirmation sink, the agent policy) travel in the request, so
	/// the task world and the script world reach the same dispatcher without a fake chat.
	/// </remarks>
	[Service(typeof(IToolInvocationDispatcher))]
	public sealed class ToolInvocationDispatcher(
		ChatToolOrchestrator chatRun,
		ChatRestartOrchestrator chatRestart,
		ToolCommandOrchestrator command,
		AgentTaskToolOrchestrator agentTask,
		ScriptToolOrchestrator script) : IToolInvocationDispatcher
	{
		/// <inheritdoc/>
		public Task RunAsync(ToolInvocationRequest request, CancellationToken ct)
		{
			var descriptor = request.Descriptor ?? MissingToolDescriptor.Instance;

			var ctx = request.ContextFactory.CreateContext(new ToolContextCreationParams
			{
				Call = request.Call,
				Descriptor = descriptor,
				Source = request.Source,
				Host = request.Host,
				RunningInUi = request.RunningInUi,
				Confirmation = request.Confirmation,
				EffectiveBehaviours = request.EffectiveBehaviours,
				ArgumentStream = request.ArgumentStream
			});

			return request.Source switch
			{
				ToolInvocationSource.ChatRun => chatRun.RunAsync(ctx, ct),
				ToolInvocationSource.ChatRestart => chatRestart.RunAsync(ctx, ct),
				ToolInvocationSource.Command => command.RunAsync(ctx, ct),
				ToolInvocationSource.AgentTask => agentTask.RunAsync(ctx, ct),
				ToolInvocationSource.Script => script.RunAsync(ctx, ct),
				_ => throw new ArgumentOutOfRangeException(nameof(request), request.Source,
					"Unknown tool invocation source.")
			};
		}
	}
}
