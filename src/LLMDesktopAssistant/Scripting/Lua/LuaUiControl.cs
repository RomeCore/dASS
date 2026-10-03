using AsyncLua;
using LLMDesktopAssistant.MVVM.Dynamic;

namespace LLMDesktopAssistant.Scripting.Lua
{
	/// <summary>
	/// Represents a UI control descriptor exposed to Lua as a <c>uicontrol</c> UserData.
	/// It is a pure input holder: the AXAML source and the dynamic view model to render it against.
	/// The actual Avalonia control is produced later by <c>DynamicAxamlControl</c> inside the
	/// <c>DynamicAxamlControlAdditionalData</c> view, so a single handle can be appended many times.
	/// </summary>
	public sealed class LuaUiControl
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="LuaUiControl"/> class.
		/// </summary>
		/// <param name="xaml">The AXAML markup to render.</param>
		/// <param name="viewModel">The dynamic view model used as the data context of the rendered control.</param>
		public LuaUiControl(string xaml, DynamicViewModel viewModel)
		{
			Xaml = xaml ?? throw new ArgumentNullException(nameof(xaml));
			ViewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
		}

		/// <summary>
		/// Gets the AXAML markup to render.
		/// </summary>
		public string Xaml { get; }

		/// <summary>
		/// Gets the dynamic view model used as the data context of the rendered control.
		/// </summary>
		public DynamicViewModel ViewModel { get; }
	}
}
