using System.Text;
using LLMDesktopAssistant.Addons;
using LLMDesktopAssistant.Addons.Management;
using LLMDesktopAssistant.Desktop.Execution;
using LLMDesktopAssistant.Tools;
using LLMDesktopAssistant.Tools.Execution.Domain;
using LLMDesktopAssistant.Tools.Execution.Support;
using Serilog;

namespace LLMDesktopAssistant.Desktop.ToolModules.Terminal
{
	/// <summary>
	/// Base class for tool modules that run processes and display their output
	/// in a terminal emulator embedded in the chat message.
	/// </summary>
	public abstract class TerminalBasedToolModule : ToolModule
	{
		private readonly IProcessLauncher _processLauncher;
		private readonly IAddonManagerInvalidator _addonInvalidator;

		/// <summary>
		/// Initializes a new instance of the <see cref="TerminalBasedToolModule"/> class.
		/// </summary>
		/// <param name="processLauncher">The process launcher used to start child processes.</param>
		/// <param name="addonInvalidator">The invalidator called after an external process may have touched addons.</param>
		protected TerminalBasedToolModule(IProcessLauncher processLauncher, IAddonManagerInvalidator addonInvalidator)
		{
			_processLauncher = processLauncher;
			_addonInvalidator = addonInvalidator;
		}

		/// <summary>
		/// Runs a process with terminal output displayed in the chat message.
		/// Creates a <see cref="TerminalAdditionalViewModel"/>, attaches it to the tool call,
		/// and waits for the process to complete — unless the user leaves the process running.
		/// </summary>
		/// <param name="parameters">Parameters describing what to run.</param>
		/// <param name="context">The tool execution context (provides access to the tool call).</param>
		/// <param name="cancellationToken">Cancellation token.</param>
		/// <returns>A <see cref="ToolExecutionResult"/> with the process exit code.</returns>
		protected async Task<ToolExecutionResult> Run(
			TerminalToolRunParameters parameters,
			IToolExecutionContext context,
			CancellationToken cancellationToken = default)
		{
			ArgumentNullException.ThrowIfNull(parameters);
			ArgumentNullException.ThrowIfNull(context);

			var call = context.Call;

			// An interactive terminal lives until its process exits or the user kills it,
			// never until a timeout: the output is not a result with a deadline.
			var launchParameters = parameters.ProcessParameters;
			if (launchParameters.RunInTerminal)
				launchParameters = launchParameters.WithTimeout(null);

			var runInTerminal = launchParameters.RunInTerminal;

			ProcessDescriptor descriptor;
			try
			{
				// A terminal process is deliberately not linked to the call's token: Leave has to detach
				// it, and a linked token cannot be unlinked. The kill is a manual registration instead,
				// which the card can release.
				descriptor = _processLauncher.Launch(
					launchParameters,
					runInTerminal ? CancellationToken.None : cancellationToken);

				// External process can affect addons potentially
				_addonInvalidator.Invalidate(AddonKind.All);
			}
			catch (Exception ex)
			{
				call.SetResult($"Failed to launch process: {ex.Message}");
				return ToolExecutionResult.Failed;
			}

			TerminalAdditionalViewModel? viewModel = null;
			if (runInTerminal)
			{
				viewModel = new TerminalAdditionalViewModel
				{
					Descriptor = descriptor,
					// A call the model ran fire-and-forget has nothing left to detach: it returned already.
					IsLeaveAllowed = parameters.Wait
				};
				viewModel.SetCancellationTokenSource(descriptor.CancellationTokenSource);
				viewModel.SetKillHandler(RegisterKill(descriptor, viewModel, cancellationToken));
				call.AddAttachment(viewModel);
			}

			if (!parameters.Wait)
			{
				CompleteInBackground(descriptor, viewModel);
				return ToolExecutionResult.Succeeded;
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

				call.SetResult(descriptor.Output);
				return ToolExecutionResult.Failed;
			}

			if (left)
			{
				call.SetResult(BuildLeftRunningResult(descriptor));
				CompleteInBackground(descriptor, viewModel);
				return ToolExecutionResult.Succeeded;
			}

			viewModel?.Complete(exitCode);
			call.SetResult(descriptor.Output);

			if (exitCode == 0)
				return ToolExecutionResult.Succeeded;

			call.ResultContent += $"\nProcess exited with code {exitCode}. Check terminal output above for details.";
			return ToolExecutionResult.Failed;
		}

		/// <summary>
		/// Registers the kill of the running process against the call's cancellation. The card owns the
		/// registration, so <see cref="TerminalAdditionalViewModel.Leave"/> can release it and let the
		/// process outlive the call.
		/// </summary>
		private static CancellationTokenRegistration RegisterKill(
			ProcessDescriptor descriptor,
			TerminalAdditionalViewModel viewModel,
			CancellationToken cancellationToken)
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

		/// <summary>
		/// Builds the model-facing result of a call the user detached: what the terminal showed at that
		/// moment, plus the fact that later output will never reach the model.
		/// </summary>
		private static string BuildLeftRunningResult(ProcessDescriptor descriptor)
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
	}
}
