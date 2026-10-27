using LLMDesktopAssistant.Controls.Toasts;
using LLMDesktopAssistant.Utils;

namespace LLMDesktopAssistant.Services.Instances;

/// <summary>
/// Global service for showing toast notifications from anywhere in the application.
/// Connects to the <see cref="ToastControl"/> instance hosted in <see cref="MVVM.MainView"/>.
/// </summary>
[Service(typeof(IToastService))]
public class ToastService : IToastService
{
	private ToastControl? _toastControl;

	/// <summary>
	/// Gets or sets the ToastControl instance to use for displaying toasts.
	/// This is automatically set by <see cref="MVVM.MainView"/> on load.
	/// </summary>
	public ToastControl? ToastControl
	{
		get => _toastControl;
		set
		{
			if (_toastControl != null && value != null && _toastControl != value)
			{
				// Transfer any queued toasts from the old control to the new one
				foreach (var toast in _toastControl.Toasts.ToList())
				{
					_toastControl.Toasts.Remove(toast);
					value.Toasts.Add(toast);
				}
			}

			_toastControl = value;
		}
	}

	public void ShowInfo(string title, string? description = null, double durationSeconds = 5.0)
	{
		ExecuteOnControl(tc => tc.ShowInfo(title, description, durationSeconds));
	}

	public void ShowWarning(string title, string? description = null, double durationSeconds = 6.0)
	{
		ExecuteOnControl(tc => tc.ShowWarning(title, description, durationSeconds));
	}

	public void ShowError(string title, string? description = null, double durationSeconds = 8.0)
	{
		ExecuteOnControl(tc => tc.ShowError(title, description, durationSeconds));
	}

	public void ShowSuccess(string title, string? description = null, double durationSeconds = 5.0)
	{
		ExecuteOnControl(tc => tc.ShowSuccess(title, description, durationSeconds));
	}

	public void Show(ToastItemViewModel toast)
	{
		ExecuteOnControl(tc => tc.Show(toast));
	}

	public void Dismiss(long toastId)
	{
		ExecuteOnControl(tc => tc.Dismiss(toastId));
	}

	public void DismissAll()
	{
		ExecuteOnControl(tc => tc.DismissAll());
	}

	private void ExecuteOnControl(Action<ToastControl> action)
	{
		if (_toastControl == null)
			return;

		if (UIDispatcher.IsUIThread)
		{
			action(_toastControl);
		}
		else
		{
			UIDispatcher.Post(() => action(_toastControl));
		}
	}
}
