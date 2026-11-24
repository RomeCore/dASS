using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.VisualTree;
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

		public static object CompileAxaml(string axaml)
		{
			var document = new RuntimeXamlLoaderDocument(
				new Uri("avares://LLMDesktopAssistant/Controls/DynamicAxamlControl.axaml"),
				axaml);
			var configuration = new RuntimeXamlLoaderConfiguration
			{
				LocalAssembly = typeof(DynamicAxamlControl).Assembly,
				UseCompiledBindingsByDefault = false,
			};
			return AvaloniaRuntimeXamlLoader.Load(document, configuration);
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
				var control = CompileAxaml(xaml);
				if (control is Visual visual && visual.IsAttachedToVisualTree())
					throw new InvalidOperationException("Cannot render a control that is already attached to the visual tree " +
						"(Window, or any other control that creates TopLevel).");
				Content = control;
			}
			catch (Exception ex)
			{
				Log.Error(ex, "Failed to render dynamic AXAML: {Error}", ex.Message);
				Content = new TextBlock
				{
					Text = "Failed to render dynamic AXAML: " + ex.Message,
					TextWrapping = Avalonia.Media.TextWrapping.Wrap,
				};
			}
		}
	}
}
