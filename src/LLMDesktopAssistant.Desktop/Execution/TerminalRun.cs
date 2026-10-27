using System.Text;
using LLMDesktopAssistant.Desktop.ToolModules.Terminal;
using LLMDesktopAssistant.Utils.Attachments;
using Serilog;

namespace LLMDesktopAssistant.Desktop.Execution
{
	/// <summary>
	/// The launch-independent half of a terminal run: attach the terminal to its host, wait for the exit or for the
	/// user to detach, kill on cancellation and report the late exit code to the card.
	/// </summary>
	/// <remarks>
	/// Shared by every caller that shows a process in an embedded terminal - a native tool module and an
	/// out-of-process script alike - so the three traps of the lifetime (no timeout, no token linkage, an optional
	/// wait) are solved in one place.
	/// </remarks>
	public static class TerminalRun
	{
		/// <summary>
		/// Attaches the terminal and drives it to its end.
		/// </summary>
		/// <param name="descriptor">The launched process.</param>
		/// <param name="host">The tool call or chat message the terminal is attached to.</param>
		/// <param name="attachTerminal">Whether to create and attach a terminal view model.</param>
		/// <param name="wait">Whether to await the exit. <see langword="false"/> returns immediately.</param>
		/// <param name="leaveAllowed">Whether the user may detach the process from the call.</param>
		/// <param name="cancellationToken">The caller's token; cancelling it kills the process.</param>
		public static async Task<TerminalRunResult> RunAsync(ProcessDescriptor descriptor, IChatAttachmentHost host,
			bool attachTerminal, bool wait, bool leaveAllowed, CancellationToken cancellationToken)
		{
			TerminalAdditionalViewModel? viewModel = null;
			if (attachTerminal)
			{
				viewModel = new TerminalAdditionalViewModel
				{
					Descriptor = descriptor,
					// A call that returned already has nothing left to detach.
					IsLeaveAllowed = leaveAllowed
				};
				viewModel.SetCancellationTokenSource(descriptor.CancellationTokenSource);
				viewModel.SetKillHandler(RegisterKill(descriptor, viewModel, cancellationToken));
				host.AddAttachment(viewModel);
			}

			if (!wait)
			{
				CompleteInBackground(descriptor, viewModel);
				return new TerminalRunResult(null, false, false);
			}

			bool left;
			int exitCode;
			try
			{
				if (viewModel is null)
				{
					exitCode = await descriptor.ExitCodeTask.WaitAsync(cancellationToken);
					left = false;
				}
				else
				{
					// Whichever comes first ends the wait. On Leave the process keeps running and its
					// background continuation reports the exit code to the card when it really happens.
					var winner = await Task
						.WhenAny(descriptor.ExitCodeTask, viewModel.LeftTask)
						.WaitAsync(cancellationToken);

					left = winner == viewModel.LeftTask;
					exitCode = left ? 0 : await descriptor.ExitCodeTask;
				}
			}
			catch (OperationCanceledException)
			{
				viewModel?.Kill();

				// A killed process completes its exit task as cancelled, never as "exited with code N",
				// so this is the only place that can tell the card the process is gone.
				viewModel?.MarkKilled();

				return new TerminalRunResult(null, false, true);
			}

			if (left)
			{
				CompleteInBackground(descriptor, viewModel);
				return new TerminalRunResult(null, true, false);
			}

			viewModel?.Complete(exitCode);
			return new TerminalRunResult(exitCode, false, false);
		}

		/// <summary>
		/// Builds the model-facing result of a call the user detached: what the terminal showed at that moment, plus
		/// the fact that later output will never reach the model.
		/// </summary>
		public static string BuildLeftRunningResult(ProcessDescriptor descriptor)
		{
			var builder = new StringBuilder();
			builder.AppendLine($"Process left running (pid {descriptor.ProcessId}): the user detached it from this call.");
			builder.AppendLine("It keeps running outside this tool call, so anything it prints from now on is not visible to you.");
			builder.AppendLine("The output below is what the terminal showed at the moment of detaching.");

			var output = descriptor.Output;
			if (!string.IsNullOrEmpty(output))
			{
				builder.AppendLine();
				builder.Append(output);
			}

			return builder.ToString();
		}

		/// <summary>
		/// Registers the kill of the running process against the call's cancellation. The card owns the
		/// registration, so <see cref="TerminalAdditionalViewModel.Leave"/> can release it and let the
		/// process outlive the call.
		/// </summary>
		private static CancellationTokenRegistration RegisterKill(ProcessDescriptor descriptor,
			TerminalAdditionalViewModel viewModel, CancellationToken cancellationToken)
		{
			try
			{
				return cancellationToken.Register(() =>
				{
					// A left process is none of the call's business any more.
					if (viewModel.IsLeft)
						return;

					try
					{
						descriptor.CancellationTokenSource.Cancel();
					}
					catch (Exception ex)
					{
						Log.Error(ex, "Failed to kill terminal process: {Error}", ex.Message);
					}
				});
			}
			catch (ObjectDisposedException)
			{
				// The call is already over; nothing can cancel it any more.
				return default;
			}
		}

		/// <summary>
		/// Keeps watching a process whose call has already returned, so the card can still report the
		/// exit code once it really exits.
		/// </summary>
		private static void CompleteInBackground(ProcessDescriptor descriptor, TerminalAdditionalViewModel? viewModel)
		{
			if (viewModel is null)
				return;

			async void WaitForExit()
			{
				try
				{
					viewModel.Complete(await descriptor.ExitCodeTask);
				}
				catch (OperationCanceledException)
				{
					// Killed on purpose: the process never reports an exit code, only the cancellation.
					viewModel.MarkKilled();
				}
				catch (Exception ex)
				{
					Log.Error(ex, "Error waiting for process exit code: {Error}", ex.Message);
				}
			}

			WaitForExit();
		}
	}
}
