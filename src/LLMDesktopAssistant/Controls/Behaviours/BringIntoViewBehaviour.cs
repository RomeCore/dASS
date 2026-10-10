using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;

namespace LLMDesktopAssistant.Controls.Behaviours
{
	public static class BringIntoViewBehaviour
	{
		public static readonly AttachedProperty<bool> WhenProperty =
			AvaloniaProperty.RegisterAttached<Control, bool>(
				"When",
				typeof(BringIntoViewBehaviour));

		public static void SetWhen(Control element, bool value) => element.SetValue(WhenProperty, value);

		public static bool GetWhen(Control element) => element.GetValue(WhenProperty);

		static BringIntoViewBehaviour()
		{
			WhenProperty.Changed.AddClassHandler<Control, bool>(
				(o, e) =>
				{
					if (e.GetNewValue<bool>())
						Reveal(o);
				});
		}

		private static void Reveal(Control element)
		{
			if (element.IsLoaded)
			{
				element.BringIntoView();
				return;
			}

			void OnLoaded(object? sender, RoutedEventArgs e)
			{
				element.Loaded -= OnLoaded;
				element.BringIntoView();
			}

			element.Loaded += OnLoaded;
		}
	}
}
