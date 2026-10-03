using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Serilog;

namespace LLMDesktopAssistant.Controls
{
	/// <summary>
	/// A control that renders an AXAML fragment provided at runtime.
	/// The fragment is loaded with reflection bindings and becomes the <see cref="ContentControl.Content"/>
	/// of this control, so it inherits this control's <see cref="StyledElement.DataContext"/>.
	/// </summary>
	/// <remarks>
	/// Parsing failures are swallowed and replaced with a diagnostic placeholder so that a broken
	/// dynamic UI cannot bring down the whole chat view.
	/// </remarks>
	public class DynamicAxamlControl : ContentControl
	{
		/// <summary>
		/// Defines the <see cref="Axaml"/> property.
		/// </summary>
		public static readonly StyledProperty<string?> AxamlProperty =
			AvaloniaProperty.Register<DynamicAxamlControl, string?>(nameof(Axaml));

		/// <summary>
		/// Gets or sets the AXAML markup to render.
		/// </summary>
		public string? Axaml
		{
			get => GetValue(AxamlProperty);
			set => SetValue(AxamlProperty, value);
		}

		static DynamicAxamlControl()
		{
			AxamlProperty.Changed.AddClassHandler<DynamicAxamlControl>((control, args) =>
				control.RenderAxaml(args.GetNewValue<string?>()));
		}

		private void RenderAxaml(string? xaml)
		{
			if (string.IsNullOrWhiteSpace(xaml))
			{
				Content = null;
				return;
			}

			try
			{
				var document = new RuntimeXamlLoaderDocument(
					new Uri("avares://LLMDesktopAssistant/Controls/DynamicAxamlControl.axaml"),
					xaml);
				var configuration = new RuntimeXamlLoaderConfiguration
				{
					LocalAssembly = typeof(DynamicAxamlControl).Assembly,
					UseCompiledBindingsByDefault = false,
				};

				Content = AvaloniaRuntimeXamlLoader.Load(document, configuration);
			}
			catch (Exception ex)
			{
				Log.Error(ex, "Failed to render dynamic AXAML");
				Content = new TextBlock
				{
					Text = "Failed to render dynamic UI: " + ex.Message,
					TextWrapping = Avalonia.Media.TextWrapping.Wrap,
				};
			}
		}
	}
}
