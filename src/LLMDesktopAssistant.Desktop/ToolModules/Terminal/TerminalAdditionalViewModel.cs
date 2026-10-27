using System.IO;
using CommunityToolkit.Mvvm.Input;
using LLMDesktopAssistant.Desktop.Execution;
using LLMDesktopAssistant.LLM.MVVM.Additional;
using LLMDesktopAssistant.Localization;
using LLMDesktopAssistant.Utils;

namespace LLMDesktopAssistant.Desktop.ToolModules.Terminal
{
	/// <summary>
	/// ViewModel for displaying a terminal emulator inside a chat message.
	/// Uses Iciclecreek.Avalonia.Terminal to show live process output.
	/// </summary>
	/// <remarks>
	/// The card owns four states (<see cref="TerminalState"/>) and two ways out of the running one:
	/// <see cref="KillCommand"/> ends the process, <see cref="LeaveCommand"/> ends only the tool call and
	/// lets the process keep running (see <see cref="TerminalState.Left"/>).
	/// </remarks>
	[ViewModelFor(typeof(TerminalAdditionalView))]
	public class TerminalAdditionalViewModel : AdditionalChatData
	{
		/// <summary>
		/// The height the terminal area starts with and returns to after a collapse, in pixels.
		/// </summary>
		public const double DefaultHeight = 500;

		/// <summary>
		/// The smallest height the user can drag the terminal area down to, in pixels.
		/// </summary>
		public const double MinHeight = 100;

		/// <summary>
		/// The largest height the user can drag the terminal area up to, in pixels.
		/// </summary>
		public const double MaxHeight = 900;

		private readonly TaskCompletionSource _leftTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
		private CancellationTokenRegistration _killRegistration;
		private CancellationTokenSource? _cts;
		private double _expandedHeight = DefaultHeight;

		// The localized status lines are created once per state: a LocaleFormattedKey subscribes to a
		// static language-changed event, so recreating them on every property read would pile up handlers.
		private LocaleKeyBase? _runningStatusKey;
		private LocaleKeyBase? _leftStatusKey;
		private LocaleKeyBase? _completedStatusKey;
		private LocaleKeyBase? _killedStatusKey;

		/// <summary>
		/// The process descriptor created by the <see cref="IProcessLauncher"/> for this session.
		/// Its <see cref="ProcessTerminalSession"/> is rendered by the view.
		/// </summary>
		public ProcessDescriptor? Descriptor
		{
			get => _descriptor;
			set
			{
				if (!SetProperty(ref _descriptor, value))
					return;

				RebuildProcessStatusKeys();
				RaisePropertyChanged(nameof(ProcessId));
				RaisePropertyChanged(nameof(ProcessName));
				RaisePropertyChanged(nameof(StatusKey));
			}
		}
		private ProcessDescriptor? _descriptor;

		/// <summary>
		/// The current lifecycle state of the process. Every other state flag is derived from it.
		/// </summary>
		public TerminalState State
		{
			get => _state;
			set
			{
				if (!SetProperty(ref _state, value))
					return;

				RaisePropertyChanged(nameof(IsRunning));
				RaisePropertyChanged(nameof(IsAwaiting));
				RaisePropertyChanged(nameof(IsLeft));
				RaisePropertyChanged(nameof(IsFinished));
				RaisePropertyChanged(nameof(IsFailed));
				RaisePropertyChanged(nameof(CanKill));
				RaisePropertyChanged(nameof(CanLeave));
				RaisePropertyChanged(nameof(CanCollapse));
				RaisePropertyChanged(nameof(StatusKey));
			}
		}
		private TerminalState _state = TerminalState.Running;

		/// <summary>
		/// The exit code of the process, once it is known.
		/// </summary>
		public int ExitCode
		{
			get => _exitCode;
			private set
			{
				if (SetProperty(ref _exitCode, value))
					RaisePropertyChanged(nameof(StatusKey));
			}
		}
		private int _exitCode;

		/// <summary>
		/// The error that prevented the session from running. Shown as the status tooltip.
		/// </summary>
		public string? ErrorMessage
		{
			get => _errorMessage;
			private set => SetProperty(ref _errorMessage, value);
		}
		private string? _errorMessage;

		/// <summary>
		/// Whether the process itself is alive: still awaited or already left.
		/// </summary>
		public bool IsRunning => State is TerminalState.Running or TerminalState.Left;

		/// <summary>
		/// Whether the tool call still waits for the process.
		/// </summary>
		public bool IsAwaiting => State == TerminalState.Running;

		/// <summary>
		/// Whether the user left the process running: the tool call is over, the process is not.
		/// </summary>
		public bool IsLeft => State == TerminalState.Left;

		/// <summary>
		/// Whether the process is over for good: it exited, was killed or never started.
		/// </summary>
		public bool IsFinished => State is TerminalState.Completed or TerminalState.Killed or TerminalState.Failed;

		/// <summary>
		/// Whether the session could not be started at all.
		/// </summary>
		public bool IsFailed => State == TerminalState.Failed;

		/// <summary>
		/// Whether the Kill button is offered: any live process can be killed, left or not.
		/// </summary>
		public bool CanKill => IsRunning;

		/// <summary>
		/// Whether the Leave button is offered. A call that the model already ran fire-and-forget has
		/// nothing to leave - it returned long ago.
		/// </summary>
		public bool CanLeave => State == TerminalState.Running && IsLeaveAllowed;

		/// <summary>
		/// Whether the card can be folded to its bar. A live process is always expanded, since its
		/// output is the reason the card exists.
		/// </summary>
		public bool CanCollapse => IsFinished;

		/// <summary>
		/// Whether this call had the model waiting for it. Set by the tool module.
		/// </summary>
		public bool IsLeaveAllowed
		{
			get => _isLeaveAllowed;
			set
			{
				if (SetProperty(ref _isLeaveAllowed, value))
					RaisePropertyChanged(nameof(CanLeave));
			}
		}
		private bool _isLeaveAllowed;

		/// <summary>
		/// Whether the card is folded to its bottom bar. A finished card starts folded: a chat with a
		/// handful of terminals is a wall of output otherwise.
		/// </summary>
		public bool IsCollapsed
		{
			get => _isCollapsed;
			set
			{
				if (SetProperty(ref _isCollapsed, value))
					CardHeight = value ? double.NaN : _expandedHeight;
			}
		}
		private bool _isCollapsed;

		/// <summary>
		/// The height of the card in pixels, or <see cref="double.NaN"/> (auto) while collapsed.
		/// </summary>
		public double CardHeight
		{
			get => _cardHeight;
			set
			{
				var height = double.IsNaN(value) ? double.NaN : Math.Clamp(value, MinHeight, MaxHeight);
				if (!SetProperty(ref _cardHeight, height))
					return;

				if (!double.IsNaN(height) && !IsCollapsed)
					_expandedHeight = height;
			}
		}
		private double _cardHeight = DefaultHeight;

		/// <summary>
		/// The identifier of the process in the OS, or <see langword="null"/> before the launch.
		/// </summary>
		public int? ProcessId => Descriptor?.ProcessId;

		/// <summary>
		/// The human-readable name of the process, for the status line.
		/// </summary>
		public string ProcessName
		{
			get
			{
				var parameters = Descriptor?.LaunchParameters;
				if (!string.IsNullOrWhiteSpace(parameters?.ProcessName))
					return parameters!.ProcessName;
				if (!string.IsNullOrWhiteSpace(parameters?.FileName))
					return Path.GetFileNameWithoutExtension(parameters!.FileName);
				return "process";
			}
		}

		/// <summary>
		/// The localized status line shown on the right of the card bar.
		/// </summary>
		public LocaleKeyBase? StatusKey => State switch
		{
			TerminalState.Running => _runningStatusKey,
			TerminalState.Left => _leftStatusKey,
			TerminalState.Completed => _completedStatusKey,
			TerminalState.Killed => _killedStatusKey,
			TerminalState.Failed => Locale.GetKey("message.terminal.status.failed"),
			_ => null
		};

		/// <summary>
		/// Completes when the user leaves the process running.
		/// </summary>
		[ChangeTracker.Untracked]
		public Task LeftTask => _leftTcs.Task;

		/// <summary>
		/// Kills the running process.
		/// </summary>
		public IRelayCommand KillCommand { get; }

		/// <summary>
		/// Ends the tool call and keeps the process running (see <see cref="Leave"/>).
		/// </summary>
		public IRelayCommand LeaveCommand { get; }

		/// <summary>
		/// Folds or unfolds a finished card.
		/// </summary>
		public IRelayCommand ToggleCollapsedCommand { get; }

		public TerminalAdditionalViewModel()
		{
			KillCommand = new RelayCommand(Kill);
			LeaveCommand = new RelayCommand(Leave);
			ToggleCollapsedCommand = new RelayCommand(ToggleCollapsed);
			IsTemporary = true; // Don't persist terminal sessions in DB
		}

		/// <summary>
		/// Sets the CancellationTokenSource used to kill the process.
		/// </summary>
		public void SetCancellationTokenSource(CancellationTokenSource cts)
		{
			_cts = cts;
		}

		/// <summary>
		/// Installs the handler that kills the process when the tool call is cancelled. The card owns it
		/// so that <see cref="Leave"/> can release it: a linked token cannot be unlinked.
		/// </summary>
		public void SetKillHandler(CancellationTokenRegistration registration)
		{
			_killRegistration = registration;
		}

		/// <summary>
		/// Kills the process via its CancellationTokenSource.
		/// </summary>
		public void Kill()
		{
			try
			{
				_cts?.Cancel();
			}
			catch (Exception)
			{
				// The source is already gone: nothing is left to kill.
			}
		}

		/// <summary>
		/// Detaches the process from the tool call: the call stops waiting and the process stops dying
		/// with it. The card still owns the process, so closing the chat still kills it.
		/// </summary>
		public void Leave()
		{
			if (!CanLeave)
				return;

			_killRegistration.Dispose();
			_killRegistration = default;

			State = TerminalState.Left;
			_leftTcs.TrySetResult();
		}

		/// <summary>
		/// Folds or unfolds the card. Only a finished card can be folded.
		/// </summary>
		public void ToggleCollapsed()
		{
			if (!CanCollapse)
				return;

			IsCollapsed = !IsCollapsed;
		}

		/// <summary>
		/// Marks the process as completed with the given exit code.
		/// </summary>
		public void Complete(int exitCode)
		{
			OnUiThread(() =>
			{
				ExitCode = exitCode;
				RebuildProcessStatusKeys();
				State = TerminalState.Completed;
				IsCollapsed = true;
			});
		}

		/// <summary>
		/// Marks the process as killed. The exit task of a killed process completes as cancelled rather
		/// than as "exited with code N", so the ordinary completion path never fires for it.
		/// </summary>
		public void MarkKilled()
		{
			OnUiThread(() =>
			{
				ExitCode = -1;
				State = TerminalState.Killed;
				IsCollapsed = true;
			});
		}

		/// <summary>
		/// Marks the process as failed with an error message.
		/// </summary>
		public void Fail(string error)
		{
			OnUiThread(() =>
			{
				ErrorMessage = error;
				State = TerminalState.Failed;
				IsCollapsed = true;
			});
		}

		/// <summary>
		/// Rebuilds the status lines that carry the process identity: the launch, the exit code and the
		/// translated templates all go into them.
		/// </summary>
		private void RebuildProcessStatusKeys()
		{
			var pid = ProcessId?.ToString() ?? "?";
			var name = ProcessName;
			_runningStatusKey = Locale.GetFormattedKey("message.terminal.status.running", pid, name);
			_leftStatusKey = Locale.GetFormattedKey("message.terminal.status.left", pid, name);
			_completedStatusKey = Locale.GetFormattedKey("message.terminal.status.exited", ExitCode.ToString(), name);
			_killedStatusKey = Locale.GetFormattedKey("message.terminal.status.killed", name);
		}

		private static void OnUiThread(Action action)
		{
			if (UIDispatcher.IsUIThread)
				action();
			else
				UIDispatcher.Post(action);
		}

		protected override void Dispose(bool disposing)
		{
			base.Dispose(disposing);

			if (!disposing)
				return;

			_killRegistration.Dispose();
			_killRegistration = default;

			try
			{
				// The card is the process's death signal: a left process dies with the chat it belongs to.
				_cts?.Cancel();
				_cts?.Dispose();
			}
			catch (Exception)
			{
				// Already disposed or cancelled - nothing to release.
			}
		}
	}
}
