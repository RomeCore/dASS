using LLMDesktopAssistant.Addons;
using LLMDesktopAssistant.Addons.Management;
using LLMDesktopAssistant.Desktop.Execution;
using LLMDesktopAssistant.Tools;
using LLMDesktopAssistant.Tools.Execution.Domain;
using LLMDesktopAssistant.Tools.Execution.Support;

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
		/// and waits for the process to complete - unless the user leaves the process running.
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

			var outcome = await TerminalRun.RunAsync(descriptor, call, runInTerminal, parameters.Wait,
				leaveAllowed: parameters.Wait, cancellationToken);

			if (outcome.Cancelled)
			{
				call.SetResult(descriptor.Output);
				return ToolExecutionResult.Failed;
			}

			if (outcome.Left)
			{
				call.SetResult(TerminalRun.BuildLeftRunningResult(descriptor));
				return ToolExecutionResult.Succeeded;
			}

			call.SetResult(descriptor.Output);

			if (outcome.ExitCode is null or 0)
				return ToolExecutionResult.Succeeded;

			call.ResultContent += $"\nProcess exited with code {outcome.ExitCode}. Check terminal output above for details.";
			return ToolExecutionResult.Failed;
		}
	}
}
