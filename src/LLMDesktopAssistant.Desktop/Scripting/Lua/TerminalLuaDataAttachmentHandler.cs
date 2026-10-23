using LLMDesktopAssistant.Desktop.Execution;
using LLMDesktopAssistant.Desktop.ToolModules.Terminal;
using LLMDesktopAssistant.Scripting.Lua;
using LLMDesktopAssistant.Services;
using LLMDesktopAssistant.Utils.Attachments;
using Serilog;

namespace LLMDesktopAssistant.Desktop.Scripting.Lua
{
	/// <summary>
	/// Attaches a live <c>terminal</c> handle (see <see cref="LuaTerminal"/>) as a terminal view model.
	/// </summary>
	/// <remarks>
	/// This is the desktop half of the Lua attachment pipeline: the script attaches the handle, the host
	/// receives a <see cref="TerminalAdditionalViewModel"/>, and the view turns it into an interactive PTY.
	/// </remarks>
	[Service(typeof(ILuaDataAttachmentHandler))]
	public class TerminalLuaDataAttachmentHandler : ILuaDataAttachmentHandler
	{
		/// <inheritdoc/>
		public int Order => 0;

		/// <inheritdoc/>
		public bool TryAttach(IChatAttachmentHost host, IChatAttachment attachment)
		{
			if (attachment is not LuaTerminal terminal)
				return false;

			var descriptor = terminal.Descriptor;
			var viewModel = new TerminalAdditionalViewModel
			{
				Descriptor = descriptor,
				// A Lua script's terminal is not a pending tool call: there is nothing to leave, and a
				// process that already exited shows its result instead of a live session.
				State = descriptor.IsRunning ? TerminalState.Running : TerminalState.Completed
			};
			viewModel.SetCancellationTokenSource(descriptor.CancellationTokenSource);

			host.AddAttachment(viewModel);

			_ = CompleteWhenExitedAsync(descriptor, viewModel);
			return true;
		}

		private static async Task CompleteWhenExitedAsync(ProcessDescriptor descriptor,
			TerminalAdditionalViewModel viewModel)
		{
			try
			{
				viewModel.Complete(await descriptor.ExitCodeTask.ConfigureAwait(false));
			}
			catch (OperationCanceledException)
			{
				viewModel.MarkKilled();
			}
			catch (Exception ex)
			{
				Log.Error(ex, "Error waiting for terminal exit code: {Error}", ex.Message);
				viewModel.Fail(ex.Message);
			}
		}
	}
}
