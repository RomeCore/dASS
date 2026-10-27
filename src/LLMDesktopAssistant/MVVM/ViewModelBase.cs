using Avalonia.Threading;
using LLMDesktopAssistant.Utils;

namespace LLMDesktopAssistant.MVVM
{
	/// <summary>
	/// Base class for view models. Implements INotifyPropertyChanged to facilitate data binding.
	/// </summary>
	public class ViewModelBase : NotifyPropertyChanged
	{
		protected static void InvokeUI(Action action, CancellationToken cancellationToken = default)
		{
			UIDispatcher.Invoke(action, DispatcherPriority.Default, cancellationToken);
		}

		protected static UIDispatcherOperation InvokeUIAsync(Action action, CancellationToken cancellationToken = default)
		{
			return UIDispatcher.InvokeAsync(action, DispatcherPriority.Default, cancellationToken);
		}
	}
}
