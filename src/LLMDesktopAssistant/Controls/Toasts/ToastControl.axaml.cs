using System.Collections.ObjectModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Rendering.Composition;
using Avalonia.Threading;
using Avalonia.VisualTree;
using CommunityToolkit.Mvvm.Input;
using Material.Icons;

namespace LLMDesktopAssistant.Controls.Toasts;

/// <summary>
/// A container control that displays toast notifications with composition animations.
/// Toasts slide in from the edge and can auto-dismiss after a configurable duration.
/// </summary>
public partial class ToastControl : UserControl
{
	private const double SlideInDistance = 80.0;
	private static readonly TimeSpan SlideInDuration = TimeSpan.FromMilliseconds(350);
	private static readonly TimeSpan FadeInDuration = TimeSpan.FromMilliseconds(250);
	private static readonly TimeSpan SlideOutDuration = TimeSpan.FromMilliseconds(300);

	public static readonly StyledProperty<ToastsPlacement> PlacementProperty =
		AvaloniaProperty.Register<ToastControl, ToastsPlacement>(
			nameof(Placement), ToastsPlacement.TopRight);

	public static readonly StyledProperty<ToastsDirection> DirectionProperty =
		AvaloniaProperty.Register<ToastControl, ToastsDirection>(
			nameof(Direction), ToastsDirection.FromTopToBottom);

	public static readonly StyledProperty<int> MaxVisibleToastsProperty =
		AvaloniaProperty.Register<ToastControl, int>(
			nameof(MaxVisibleToasts), 5);

	public ToastsPlacement Placement
	{
		get => GetValue(PlacementProperty);
		set => SetValue(PlacementProperty, value);
	}

	public ToastsDirection Direction
	{
		get => GetValue(DirectionProperty);
		set => SetValue(DirectionProperty, value);
	}

	public int MaxVisibleToasts
	{
		get => GetValue(MaxVisibleToastsProperty);
		set => SetValue(MaxVisibleToastsProperty, value);
	}

	/// <summary>
	/// The collection of currently displayed toast ViewModels.
	/// </summary>
	public ObservableCollection<ToastItemViewModel> Toasts { get; } = [];

	private readonly Dictionary<long, CompositionVisual?> _toastVisuals = [];
	private readonly Dictionary<long, CancellationTokenSource> _toastTimers = [];

	static ToastControl()
	{
		PlacementProperty.Changed.AddClassHandler<ToastControl>((ctrl, _) => ctrl.UpdatePlacement());
	}

	public ToastControl()
	{
		InitializeComponent();

		PART_ToastList.ItemsSource = Toasts;
		PART_ToastList.ContainerPrepared += OnItemContainerPrepared;

		UpdatePlacement();
	}

	public void ShowInfo(string title, string? description = null, double durationSeconds = 5.0)
	{
		Show(new ToastItemViewModel
		{
			Type = ToastType.Info,
			Title = title,
			Description = description,
			DurationSeconds = durationSeconds,
			DismissCommand = CreateDismissCommand(),
		});
	}

	public void ShowWarning(string title, string? description = null, double durationSeconds = 6.0)
	{
		Show(new ToastItemViewModel
		{
			Type = ToastType.Warning,
			Title = title,
			Description = description,
			DurationSeconds = durationSeconds,
			DismissCommand = CreateDismissCommand(),
		});
	}

	public void ShowError(string title, string? description = null, double durationSeconds = 8.0)
	{
		Show(new ToastItemViewModel
		{
			Type = ToastType.Error,
			Title = title,
			Description = description,
			DurationSeconds = durationSeconds,
			DismissCommand = CreateDismissCommand(),
		});
	}

	public void ShowSuccess(string title, string? description = null, double durationSeconds = 5.0)
	{
		Show(new ToastItemViewModel
		{
			Type = ToastType.Success,
			Title = title,
			Description = description,
			DurationSeconds = durationSeconds,
			DismissCommand = CreateDismissCommand(),
		});
	}

	public void Show(ToastItemViewModel toast)
	{
		ArgumentNullException.ThrowIfNull(toast);

		if (Direction != ToastsDirection.FromTopToBottom)
			Toasts.Insert(0, toast);
		else
			Toasts.Add(toast);

		EnforceMaxVisible();

		if (toast.DurationSeconds > 0)
		{
			ScheduleDismiss(toast.Id, TimeSpan.FromSeconds(toast.DurationSeconds));
		}
	}

	public void Dismiss(long toastId)
	{
		if (!Dispatcher.UIThread.CheckAccess())
		{
			Dispatcher.UIThread.Post(() => Dismiss(toastId));
			return;
		}

		var toast = Toasts.FirstOrDefault(t => t.Id == toastId);
		if (toast == null) return;

		CancelTimer(toastId);
		toast.IsVisible = false;

		AnimateOut(toastId, () =>
		{
			Toasts.Remove(toast);
			_toastVisuals.Remove(toastId);
		});
	}

	public void DismissAll()
	{
		var ids = Toasts.Select(t => t.Id).ToList();
		foreach (var id in ids)
		{
			Dismiss(id);
		}
	}

	private void OnItemContainerPrepared(object? sender, ContainerPreparedEventArgs e)
	{
		if (e.Container.DataContext is not ToastItemViewModel toast)
			return;

		e.Container.Loaded += (_, _) =>
		{
			ConfigureToastVisuals(e.Container, toast);
			PlaySlideIn(toast.Id, e.Container);
		};
	}

	private void ConfigureToastVisuals(Control container, ToastItemViewModel toast)
	{
		// Авалония говно
		// Почему NameScope не работают и мне приходится вручную искать элементы? А? Блять!
		var cc = container.FindDescendantOfType<Border>()!;
		var iconBorder = cc.FindDescendantOfType<Border>(false, d => d.Name == "PART_IconBorder");
		var icon = cc.FindDescendantOfType<VisualIcon>(false, d => d.Name == "PART_ToastIcon");

		if (icon == null) return;

		switch (toast.Type)
		{
			case ToastType.Info:
				icon.Kind = MaterialIconKind.InformationOutline;
				if (iconBorder != null)
					iconBorder.Background = this.TryFindResource("PrimaryAccentBrush", out var accentBrush)
						? accentBrush as IBrush
						: new SolidColorBrush(Color.Parse("#6C5CE7"));
				break;

			case ToastType.Warning:
				icon.Kind = MaterialIconKind.AlertOutline;
				if (iconBorder != null)
					iconBorder.Background = this.TryFindResource("WarningBrush", out var warningBrush)
						? warningBrush as IBrush
						: new SolidColorBrush(Color.Parse("#D4A017"));
				break;

			case ToastType.Error:
				icon.Kind = MaterialIconKind.AlertCircleOutline;
				if (iconBorder != null)
					iconBorder.Background = this.TryFindResource("ErrorBrush", out var errorBrush)
						? errorBrush as IBrush
						: new SolidColorBrush(Color.Parse("#FF4444"));
				break;

			case ToastType.Success:
				icon.Kind = MaterialIconKind.CheckCircleOutline;
				if (iconBorder != null)
					iconBorder.Background = this.TryFindResource("SuccessBrush", out var successBrush)
						? successBrush as IBrush
						: new SolidColorBrush(Color.Parse("#2ECC71"));
				break;
		}
	}

	private ICommand CreateDismissCommand()
	{
		return new RelayCommand<long>(Dismiss);
	}

	private void ScheduleDismiss(long toastId, TimeSpan delay)
	{
		var cts = new CancellationTokenSource();
		_toastTimers[toastId] = cts;

		_ = Task.Run(async () =>
		{
			try
			{
				await Task.Delay(delay, cts.Token);
				if (!cts.Token.IsCancellationRequested)
				{
					Dispatcher.UIThread.Post(() => Dismiss(toastId));
				}
			}
			catch (OperationCanceledException) { }
		}, cts.Token);
	}

	private void CancelTimer(long toastId)
	{
		if (_toastTimers.TryGetValue(toastId, out var cts))
		{
			cts.Cancel();
			cts.Dispose();
			_toastTimers.Remove(toastId);
		}
	}

	private void EnforceMaxVisible()
	{
		while (Toasts.Count > MaxVisibleToasts)
		{
			var oldest = Direction == ToastsDirection.FromTopToBottom ? Toasts.LastOrDefault() : Toasts.FirstOrDefault();
			if (oldest != null)
			{
				Dismiss(oldest.Id);
			}
		}
	}

	/// <summary>
	/// Plays the entrance animation (slide + fade in) for a newly added toast.
	/// Only the new toast is animated — existing toasts are NOT touched.
	/// </summary>
	private void PlaySlideIn(long toastId, Control container)
	{
		var visual = ElementComposition.GetElementVisual(container);
		if (visual == null) return;

		_toastVisuals[toastId] = visual;
		var compositor = visual.Compositor;

		UpdateLayout();

		var opacityAnimation = compositor.CreateScalarKeyFrameAnimation();
		opacityAnimation.Duration = FadeInDuration;
		opacityAnimation.Target = "Opacity";
		opacityAnimation.InsertExpressionKeyFrame(1f, "this.FinalValue");

		var implicitAnimations = compositor.CreateImplicitAnimationCollection();
		implicitAnimations["Opacity"] = opacityAnimation;
		visual.ImplicitAnimations = implicitAnimations;

		var fadeIn = compositor.CreateScalarKeyFrameAnimation();
		fadeIn.Duration = FadeInDuration;
		fadeIn.InsertKeyFrame(0f, 0f);
		fadeIn.InsertKeyFrame(1f, 1f);
		visual.StartAnimation("Opacity", fadeIn);
	}

	private void AnimateOut(long toastId, Action onComplete)
	{
		if (!_toastVisuals.TryGetValue(toastId, out var visual) || visual == null)
		{
			onComplete();
			return;
		}

		var compositor = visual.Compositor;

		var (startOffset, _) = GetSlideOffsets();
		var exitOffset = new Vector3D(startOffset.X * 1.5f, startOffset.Y * 1.5f, 0);

		var slideOut = compositor.CreateVector3DKeyFrameAnimation();
		slideOut.Duration = SlideOutDuration;
		slideOut.InsertExpressionKeyFrame(0f, "this.FinalValue");
		slideOut.InsertKeyFrame(1f, exitOffset);
		visual.StartAnimation("Offset", slideOut);

		var fadeOut = compositor.CreateScalarKeyFrameAnimation();
		fadeOut.Duration = SlideOutDuration;
		fadeOut.InsertKeyFrame(1f, 0f);
		visual.StartAnimation("Opacity", fadeOut);

		_ = Task.Run(async () =>
		{
			await Task.Delay(SlideOutDuration + TimeSpan.FromMilliseconds(50));
			Dispatcher.UIThread.Post(onComplete);
		});
	}

	private (Vector3D Start, Vector3D End) GetSlideOffsets()
	{
		double startX = 0, startY = 0;

		switch (Placement)
		{
			case ToastsPlacement.TopLeft:
			case ToastsPlacement.CenterLeft:
			case ToastsPlacement.BottomLeft:
				startX = -SlideInDistance;
				break;
			case ToastsPlacement.TopRight:
			case ToastsPlacement.CenterRight:
			case ToastsPlacement.BottomRight:
				startX = SlideInDistance;
				break;
		}

		switch (Direction)
		{
			case ToastsDirection.FromTopToBottom:
				startY = SlideInDistance;
				break;
			case ToastsDirection.FromBottomToTop:
				startY = -SlideInDistance;
				break;
		}
		startY = 0;

		return (new Vector3D(startX, startY, 0), new Vector3D(0, 0, 0));
	}

	private void UpdatePlacement()
	{
		if (PART_ToastList == null) return;

		switch (Placement)
		{
			case ToastsPlacement.TopLeft:
			case ToastsPlacement.CenterLeft:
			case ToastsPlacement.BottomLeft:
				PART_ToastList.HorizontalAlignment = HorizontalAlignment.Left;
				break;
			case ToastsPlacement.Top:
			case ToastsPlacement.Bottom:
				PART_ToastList.HorizontalAlignment = HorizontalAlignment.Center;
				break;
			case ToastsPlacement.TopRight:
			case ToastsPlacement.CenterRight:
			case ToastsPlacement.BottomRight:
			default:
				PART_ToastList.HorizontalAlignment = HorizontalAlignment.Right;
				break;
		}

		switch (Placement)
		{
			case ToastsPlacement.TopLeft:
			case ToastsPlacement.Top:
			case ToastsPlacement.TopRight:
				PART_ToastList.VerticalAlignment = VerticalAlignment.Top;
				break;
			case ToastsPlacement.CenterLeft:
			case ToastsPlacement.CenterRight:
				PART_ToastList.VerticalAlignment = VerticalAlignment.Center;
				break;
			case ToastsPlacement.BottomLeft:
			case ToastsPlacement.Bottom:
			case ToastsPlacement.BottomRight:
				PART_ToastList.VerticalAlignment = VerticalAlignment.Bottom;
				break;
		}
	}
}
