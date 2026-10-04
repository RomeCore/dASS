using AsyncLua.Values;
using LLMDesktopAssistant.MVVM.Dynamic;

namespace LLMDesktopAssistant.Scripting.Lua
{
	/// <summary>
	/// Represents a UI control descriptor exposed to Lua as a <c>uicontrol</c> UserData.
	/// It is a pure input holder: the AXAML source and the dynamic view model to render it against.
	/// The actual Avalonia control is produced later by <c>DynamicAxamlControl</c> inside the
	/// <c>DynamicAxamlControlAdditionalData</c> view, so a single handle can be appended many times.
	/// </summary>
	/// <remarks>
	/// The AXAML source is mutable: <see cref="SetXaml"/> (exposed to Lua as <c>ui:set_xaml(xaml)</c>,
	/// or via a plain assignment <c>ui.xaml = ...</c>) replaces the markup and re-renders every control
	/// that was appended from this handle. The dynamic view model, by contrast, is fixed at creation.
	/// </remarks>
	public sealed class LuaUiControl
	{
		private string _xaml;

		/// <summary>
		/// Initializes a new instance of the <see cref="LuaUiControl"/> class.
		/// </summary>
		/// <param name="xaml">The AXAML markup to render.</param>
		/// <param name="viewModel">The dynamic view model used as the data context of the rendered control.</param>
		public LuaUiControl(string xaml, DynamicViewModel viewModel)
		{
			_xaml = xaml ?? throw new ArgumentNullException(nameof(xaml));
			ViewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
		}

		/// <summary>
		/// Gets or sets the AXAML markup to render.
		/// Setting it re-renders every control appended from this handle (the whole visual tree is rebuilt).
		/// </summary>
		public string Xaml
		{
			get => _xaml;
			set
			{
				if (value is null)
					throw new ArgumentNullException(nameof(value));
				if (_xaml == value)
					return;

				_xaml = value;
				XamlChanged?.Invoke(this, EventArgs.Empty);
			}
		}

		/// <summary>
		/// Gets the dynamic view model used as the data context of the rendered control.
		/// </summary>
		[LuaHidden]
		public DynamicViewModel ViewModel { get; }

		/// <summary>
		/// Replaces the AXAML markup of this handle, re-rendering every control appended from it.
		/// Exposed to Lua as <c>ui:set_xaml(xaml)</c>.
		/// </summary>
		/// <param name="xaml">The new AXAML markup to render.</param>
		public void SetXaml(string xaml) => Xaml = xaml;

		/// <summary>
		/// Raised when <see cref="Xaml"/> changes, so appended controls can re-render.
		/// </summary>
		[LuaHidden]
		public event EventHandler? XamlChanged;
	}
}
