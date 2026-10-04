using System.Collections.Immutable;
using System.Runtime.InteropServices;
using LLMDesktopAssistant.Addons;
using LLMDesktopAssistant.Addons.Management;
using LLMDesktopAssistant.Desktop.Execution;
using LLMDesktopAssistant.Tools;
using LLMDesktopAssistant.Utils;
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
		/// Creates a <see cref="TerminalAdditionalViewModel"/>, adds it to the message's
		/// AdditionalViewModels collection, and waits for the process to complete.
		/// </summary>
		/// <param name="parameters">Parameters describing what to run.</param>
		/// <param name="context">The tool execution context (provides access to the chat message).</param>
		/// <param name="cancellationToken">Cancellation token.</param>
		/// <returns>A ReactiveToolResult with the process exit code.</returns>
		protected ReactiveToolResult Run(
			TerminalToolRunParameters parameters,
			ToolExecutionContext context,
			CancellationToken cancellationToken = default)
		{
			ArgumentNullException.ThrowIfNull(parameters);
			ArgumentNullException.ThrowIfNull(context);

			var result = new ReactiveToolResult
			{
				StatusIcon = parameters.StatusIcon,
				StatusTitle = parameters.StatusTitle
			};

			ProcessDescriptor descriptor;
			try
			{
				descriptor = _processLauncher.Launch(parameters.ProcessParameters, cancellationToken);
				// External process can affect addons potentially
				_addonInvalidator.Invalidate(AddonKind.All);
			}
			catch (Exception ex)
			{
				result.ResultContent = $"Failed to launch process: {ex.Message}";
				result.CompleteWithError();
				return result;
			}

			Task.Run(async () =>
			{
				TerminalAdditionalViewModel? viewModel = null;
				if (parameters.ProcessParameters.RunInTerminal)
				{
					viewModel = new TerminalAdditionalViewModel
					{
						Descriptor = descriptor,
						IsRunning = true
					};
					viewModel.SetCancellationTokenSource(descriptor.CancellationTokenSource);
					result.AdditionalData.Add(viewModel);
				}

				if (parameters.Wait)
				{
					int exitCode;
					IDisposable? syncSub = null;
					try
					{
						// Stream only when we are not running in a terminal, so user can see the
						// streaming output progress in the tool call details.
						if (!parameters.ProcessParameters.RunInTerminal)
							syncSub = result.ResultContentLines.SyncFrom(descriptor.PlainOutput!.Output);
						exitCode = await descriptor.ExitCodeTask.WaitAsync(cancellationToken);
					}
					catch (OperationCanceledException)
					{
						viewModel?.Cancel();
						result.ResultContent = descriptor.Output;
						result.CompleteWithError();
						return;
					}
					finally
					{
						syncSub?.Dispose();
					}

					viewModel?.Complete(exitCode);
					result.ResultContent = descriptor.Output;

					if (exitCode == 0)
					{
						result.CompleteWithSuccess();
						return;
					}
					else
					{
						result.ResultContent += $"\nProcess exited with code {exitCode}. Check terminal output above for details.";
						result.CompleteWithError();
						return;
					}
				}
				else
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
					result.CompleteWithSuccess();
				}
			}, CancellationToken.None);

			return result;
		}
	}
}
