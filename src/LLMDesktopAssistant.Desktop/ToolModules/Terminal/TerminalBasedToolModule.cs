using System.Collections.Immutable;
using System.Runtime.InteropServices;
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
		protected TerminalBasedToolModule(IProcessLauncher processLauncher, IAddonManagerInvalidator addonInvalidator)
		{
			_processLauncher = processLauncher;
			_addonInvalidator = addonInvalidator;
		}

		/// <summary>
		/// Runs a process with terminal output displayed in the chat message.
		/// Creates a <see cref="TerminalAdditionalViewModel"/>, attaches it to the tool call,
		/// and waits for the process to complete.
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
			call.SetStatus(parameters.StatusIcon, parameters.StatusTitle);

			ProcessDescriptor descriptor;
			try
			{
				descriptor = _processLauncher.Launch(parameters.ProcessParameters, cancellationToken);
				// External process can affect addons potentially
				_addonInvalidator.Invalidate(AddonKind.All);
			}
			catch (Exception ex)
			{
				call.SetResult($"Failed to launch process: {ex.Message}");
				return ToolExecutionResult.Failed;
			}

			TerminalAdditionalViewModel? viewModel = null;
			if (parameters.ProcessParameters.RunInTerminal)
			{
				viewModel = new TerminalAdditionalViewModel
				{
					Descriptor = descriptor,
					IsRunning = true
				};
				viewModel.SetCancellationTokenSource(descriptor.CancellationTokenSource);
				call.AddAttachment(viewModel);
			}

			if (!parameters.Wait)
			{
				async void FireAndForgetTask()
				{
					try
					{
						viewModel?.Complete(await descriptor.ExitCodeTask);
					}
					catch (Exception ex)
					{
						Log.Error(ex, "Error waiting for process exit code: {Error}", ex.Message);
					}
				}
				FireAndForgetTask();
				return ToolExecutionResult.Succeeded;
			}

			int exitCode;
			try
			{
				exitCode = await descriptor.ExitCodeTask.WaitAsync(cancellationToken);
			}
			catch (OperationCanceledException)
			{
				viewModel?.Cancel();
				call.SetResult(descriptor.Output);
				return ToolExecutionResult.Failed;
			}

			viewModel?.Complete(exitCode);
			call.SetResult(descriptor.Output);

			if (exitCode == 0)
				return ToolExecutionResult.Succeeded;

			call.ResultContent += $"\nProcess exited with code {exitCode}. Check terminal output above for details.";
			return ToolExecutionResult.Failed;
		}
	}
}
