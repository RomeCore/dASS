using System.ComponentModel;
using System.Timers;
using LLMDesktopAssistant.LLM.Domain;
using LLMDesktopAssistant.LLM.Services;
using Material.Icons;
using Timer = System.Timers.Timer;

namespace LLMDesktopAssistant.LLM.MVVM;

[ViewModelFor(typeof(ChatStatusView))]
public class ChatStatusViewModel : ViewModelBase
{
	private readonly IChatStatusService _statusService;
	private readonly Timer _dotTimer;
	private int _dotCount;

	/// <summary>
	/// Whether the status is currently visible.
	/// </summary>
	public bool IsActive => _statusService.Text != null;

	/// <summary>
	/// The icon to display based on the current chat status.
	/// </summary>
	public VisualIconKind IconKind => _statusService.Icon;

	/// <summary>
	/// The current status text directly from the chat status service.
	/// </summary>
	public string? StatusText => _statusService.Text;

	private string _animatedDots = "";
	/// <summary>
	/// Animated dots that blink to show activity.
	/// </summary>
	public string AnimatedDots
	{
		get => _animatedDots;
		private set => SetProperty(ref _animatedDots, value);
	}

	public ChatStatusViewModel(Chat chat)
	{
		_statusService = chat.Services.GetRequiredService<IChatStatusService>();

		_statusService.PropertyChanged += OnStatusServicePropertyChanged;

		_dotTimer = new Timer(400);
		_dotTimer.Elapsed += OnDotTimerElapsed;
		_dotTimer.AutoReset = true;

		if (IsActive)
			_dotTimer.Start();
	}

	private void OnStatusServicePropertyChanged(object? sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName is nameof(IChatStatusService.Text) or nameof(IChatStatusService.Icon))
		{
			InvokeUI(() =>
			{
				RaisePropertyChanged(nameof(IsActive));
				RaisePropertyChanged(nameof(IconKind));
				RaisePropertyChanged(nameof(StatusText));

				if (IsActive && !_dotTimer.Enabled)
					_dotTimer.Start();
				else if (!IsActive && _dotTimer.Enabled)
					_dotTimer.Stop();
			});
		}
	}

	private void OnDotTimerElapsed(object? sender, ElapsedEventArgs e)
	{
		_dotCount = (_dotCount + 1) % 4;
		var dots = _dotCount switch
		{
			0 => "",
			1 => ".",
			2 => "..",
			3 => "...",
			_ => ""
		};

		InvokeUI(() =>
		{
			AnimatedDots = dots;
		});
	}

	protected override void Dispose(bool disposing)
	{
		base.Dispose(disposing);

		if (disposing)
		{
			_statusService.PropertyChanged -= OnStatusServicePropertyChanged;
			_dotTimer.Stop();
			_dotTimer.Dispose();
		}
	}
}
