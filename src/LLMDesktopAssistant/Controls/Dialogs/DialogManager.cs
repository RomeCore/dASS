using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Media.Transformation;
using Avalonia.VisualTree;
using DialogHostAvalonia;

namespace LLMDesktopAssistant.Controls.Dialogs
{
	public static class DialogManager
	{
		private static readonly TimeSpan closeAnimationDuration = TimeSpan.FromMilliseconds(160);
		private static readonly HashSet<DialogSession> _animatingClosures = [];
		private static readonly Stack<DialogViewModel> _dialogStack = [];

		/// <summary>
		/// Gets whether any dialogs are currently open.
		/// </summary>
		public static bool IsDialogOpen => _dialogStack.Count > 0;

		/// <summary>
		/// Displays a dialog asynchronously.
		/// </summary>
		/// <param name="viewModel">The view model to display in the dialog.</param>
		/// <returns></returns>
		public static async Task<object?> ShowDialogAsync(object? viewModel)
		{
			var dvm = new DialogViewModel
			{
				Content = viewModel
			};
			_dialogStack.Push(dvm);

			try
			{
				var dv = new DialogView { DataContext = dvm };
				return await DialogHost.Show(
					dv,
					dialogIdentifier: null,
					openedEventHandler: null,
					closingEventHandler: OnDialogClosing);
			}
			finally
			{
				_dialogStack.Pop();
			}
		}

		/// <summary>
		/// Closes the currently open dialog.
		/// </summary>
		/// <param name="result">The result to pass back to the dialog's view model.</param>
		public static void CloseDialog(object? result = null)
		{
			DialogHost.Close(dialogIdentifier: null, parameter: result);
		}

		/// <summary>
		/// Plays the closing animation before letting the host go.
		/// </summary>
		/// <remarks>
		/// The host clears its content before its own exit animation runs, so anything the library
		/// animates on close is an empty frame. Cancelling the close keeps the session - and the content -
		/// alive for the duration, and the backdrop stays full size, so a dialog underneath it stays
		/// shielded while this one fades out.
		/// </remarks>
		private static async void OnDialogClosing(object? sender, DialogClosingEventArgs eventArgs)
		{
			var session = eventArgs.Session;
			var parameter = eventArgs.Parameter;

			if (!eventArgs.CanBeCancelled
				|| _animatingClosures.Contains(session)
				|| session.Host.DisableOpeningAnimation)
				return;

			eventArgs.Cancel();
			_animatingClosures.Add(session);

			try
			{
				await AnimateCloseAsync(session.Host);
			}
			finally
			{
				try
				{
					// Still marked as animating, so the close re-entering this handler falls straight through.
					session.Close(parameter);
				}
				finally
				{
					_animatingClosures.Remove(session);
				}
			}
		}

		private static async Task AnimateCloseAsync(DialogOverlayPopupHost host)
		{
			var descendants = host.GetVisualDescendants().ToArray();

			var frame = descendants
				.OfType<Border>()
				.FirstOrDefault(border => border.Name == "PART_Dialog_ContentBackground");

			if (frame is not null)
			{
				frame.Transitions = new Transitions
				{
					new DoubleTransition
					{
						Property = Visual.OpacityProperty,
						Duration = closeAnimationDuration,
						Easing = new CubicEaseOut()
					},
					new TransformOperationsTransition
					{
						Property = Visual.RenderTransformProperty,
						Duration = closeAnimationDuration,
						Easing = new CubicEaseOut()
					}
				};

				frame.RenderTransformOrigin = RelativePoint.Center;
				frame.RenderTransform = TransformOperations.Parse("scale(0.96)");
				frame.Opacity = 0;
			}

			// The backdrop is part of the host template, not of the dialog: the library clears the content
			// before its own exit animation, so the scrim is faded here instead of trusting that timing.
			var scrim = descendants
				.OfType<Rectangle>()
				.FirstOrDefault(rectangle => rectangle.Name == "PART_Dialog_Scrim");

			if (scrim is not null)
			{
				scrim.Transitions = new Transitions
				{
					new DoubleTransition
					{
						Property = Visual.OpacityProperty,
						Duration = closeAnimationDuration,
						Easing = new CubicEaseOut()
					}
				};

				scrim.Opacity = 0;
			}

			await Task.Delay(closeAnimationDuration);
		}
	}
}
