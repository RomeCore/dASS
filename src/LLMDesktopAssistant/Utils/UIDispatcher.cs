using Avalonia;
using Avalonia.Threading;

namespace LLMDesktopAssistant.Utils
{
	/// <summary>
	/// The single seam for marshalling work onto the UI thread. When a live Avalonia application owns
	/// the process, every call is handed to <see cref="Dispatcher.UIThread"/> and keeps its real
	/// semantics. Without one - unit tests today, headless hosts later - the very same call runs
	/// inline on the calling thread, so nothing blocks on a dispatcher loop that never pumps.
	/// <para/>
	/// Inline mode also answers <see cref="IsUIThread"/> with <see langword="true"/>: callers use that
	/// property to choose between "touch the UI directly" and "queue it", and a false answer would make
	/// them queue forever or recurse.
	/// </summary>
	public static class UIDispatcher
	{
		private static bool? _delegating;

		/// <summary>
		/// Gets or sets a value indicating whether calls are marshalled to the Avalonia dispatcher.
		/// Left unset, a live <see cref="Application"/> decides; tests and dispatcher-less hosts set it
		/// explicitly. The value is deliberately not cached from the live application, because the first
		/// call may happen before the application exists and a cached <see langword="false"/> would stick.
		/// </summary>
		public static bool Delegating
		{
			get => _delegating ?? Application.Current is not null;
			set => _delegating = value;
		}

		/// <summary>
		/// Gets a value indicating whether the caller may touch UI state right now. Always
		/// <see langword="true"/> in inline mode, because nothing is ever deferred there.
		/// </summary>
		public static bool IsUIThread => !Delegating || Dispatcher.UIThread.CheckAccess();

		/// <summary>
		/// Runs the action on the UI thread and waits for it. On the UI thread (or inline) it runs
		/// immediately.
		/// </summary>
		/// <param name="action">The action to run.</param>
		/// <param name="priority">The dispatcher priority, ignored when the action runs inline.</param>
		/// <param name="cancellationToken">The token that cancels the queued operation.</param>
		public static void Invoke(Action action, DispatcherPriority? priority = null, CancellationToken cancellationToken = default)
		{
			if (IsUIThread)
			{
				action();
				return;
			}

			Dispatcher.UIThread.Invoke(action, priority ?? DispatcherPriority.Default, cancellationToken);
		}

		/// <summary>
		/// Queues the action for the UI thread without waiting for it. While delegating, the action is
		/// always deferred - even when the caller is already on the UI thread, which is the whole point
		/// of the "after the current event" pattern.
		/// </summary>
		/// <param name="action">The action to queue.</param>
		/// <param name="priority">The dispatcher priority, ignored when the action runs inline.</param>
		public static void Post(Action action, DispatcherPriority? priority = null)
		{
			if (!Delegating)
			{
				action();
				return;
			}

			Dispatcher.UIThread.Post(action, priority ?? DispatcherPriority.Default);
		}

		/// <summary>
		/// Queues an asynchronous action for the UI thread without waiting for it. Inline mode starts it
		/// right away and does not await it either.
		/// </summary>
		/// <param name="action">The asynchronous action to queue.</param>
		/// <param name="priority">The dispatcher priority, ignored when the action runs inline.</param>
		public static void Post(Func<Task> action, DispatcherPriority? priority = null)
		{
			if (!Delegating)
			{
				_ = action();
				return;
			}

			Dispatcher.UIThread.Post(() => _ = action(), priority ?? DispatcherPriority.Default);
		}

		/// <summary>
		/// Queues the action for the UI thread and returns a handle that can abort it. Unlike
		/// <see cref="Invoke"/>, a delegating call is queued even on the UI thread, so callers keep their
		/// coalescing behaviour. Inline mode runs it immediately and returns a completed handle.
		/// </summary>
		/// <param name="action">The action to queue.</param>
		/// <param name="priority">The dispatcher priority, ignored when the action runs inline.</param>
		/// <param name="cancellationToken">The token that cancels the queued operation.</param>
		/// <returns>The handle of the queued operation.</returns>
		public static UIDispatcherOperation InvokeAsync(Action action, DispatcherPriority? priority = null, CancellationToken cancellationToken = default)
		{
			if (!Delegating)
			{
				action();
				return UIDispatcherOperation.Completed;
			}

			return new UIDispatcherOperation(Dispatcher.UIThread.InvokeAsync(action, priority ?? DispatcherPriority.Default, cancellationToken));
		}
	}
}
