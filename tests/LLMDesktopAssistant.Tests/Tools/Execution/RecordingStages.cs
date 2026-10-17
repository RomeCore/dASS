using LLMDesktopAssistant.Tools;
using LLMDesktopAssistant.Tools.Execution.Domain;
using LLMDesktopAssistant.Tools.Execution.Services;

namespace LLMDesktopAssistant.Tests.Tools.Execution
{
	/// <summary>
	/// A recording implementation of every stage service. It lets an orchestrator be exercised without a
	/// chat, a task or a real tool: the test asserts the sequence of stage calls and the exception the
	/// settlement stage received.
	/// </summary>
	internal sealed class RecordingStages : IToolStreamingService, IToolValidationService,
		IToolSynchronizationService, IToolPreviewService, IToolPolicyService, IToolConfirmationService,
		IToolExecutionService, IToolSettlementService
	{
		/// <summary>The stages that ran, in order.</summary>
		public List<string> Calls { get; } = [];

		/// <summary>The stage that throws <see cref="Failure"/>, or <see langword="null"/>.</summary>
		public string? FailureStage { get; init; }

		/// <summary>The exception the failing stage throws.</summary>
		public Exception? Failure { get; init; }

		/// <summary>Whether the preview stage lets the sequence continue.</summary>
		public bool PreviewContinues { get; init; } = true;

		/// <summary>Whether the confirmation stage approves the call.</summary>
		public bool Confirms { get; init; } = true;

		/// <summary>Whether the settlement stage ran.</summary>
		public bool Settled { get; private set; }

		/// <summary>The exception the settlement stage received, or <see langword="null"/>.</summary>
		public Exception? SettledException { get; private set; }

		Task IToolStreamingService.RunAsync(IToolExecutionContext ctx, StreamingMode mode, CancellationToken ct)
		{
			Record("streaming");
			return Task.CompletedTask;
		}

		void IToolValidationService.Validate(IToolExecutionContext ctx) => Record("validation");

		async Task IToolSynchronizationService.RunAsync(IToolExecutionContext ctx, Func<Task> inner,
			CancellationToken ct)
		{
			Record("synchronization");
			await inner();
		}

		Task<bool> IToolPreviewService.RunAsync(IToolExecutionContext ctx, CancellationToken ct)
		{
			Record("preview");
			return Task.FromResult(PreviewContinues);
		}

		Task<ToolPolicyDecision> IToolPolicyService.EvaluateAsync(IToolExecutionContext ctx, CancellationToken ct)
		{
			Record("policy");
			return Task.FromResult(ToolPolicyDecision.Approve);
		}

		Task<bool> IToolConfirmationService.ConfirmAsync(IToolExecutionContext ctx, CancellationToken ct)
		{
			Record("confirmation");
			return Task.FromResult(Confirms);
		}

		Task IToolExecutionService.RunAsync(IToolExecutionContext ctx, CancellationToken ct)
		{
			Record("execution");
			return Task.CompletedTask;
		}

		void IToolSettlementService.Finalize(IToolExecutionContext ctx, Exception? exception)
		{
			Settled = true;
			SettledException = exception;
			Calls.Add("settlement");
		}

		private void Record(string stage)
		{
			Calls.Add(stage);

			if (FailureStage == stage && Failure is not null)
				throw Failure;
		}
	}
}
