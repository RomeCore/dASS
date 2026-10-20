using LLMDesktopAssistant.LLM.MVVM.Additional;
using LLMDesktopAssistant.Services;
using LLMDesktopAssistant.Utils.Attachments;

namespace LLMDesktopAssistant.Scripting.Lua
{
	/// <summary>
	/// Attaches a <c>uicontrol</c> handle (see <see cref="LuaUiControl"/>) as a dynamic AXAML part.
	/// </summary>
	[Service(typeof(ILuaDataAttachmentHandler))]
	public class LuaUiControlAttachmentHandler : ILuaDataAttachmentHandler
	{
		/// <inheritdoc/>
		public int Order => 0;

		/// <inheritdoc/>
		public bool TryAttach(IChatAttachmentHost host, IChatAttachment attachment)
		{
			if (attachment is not LuaUiControl uiControl)
				return false;

			var additional = new DynamicAxamlControlAdditionalData(uiControl.Xaml, uiControl.ViewModel);

			// Follow the handle: ui:set_xaml(xaml) re-renders every control appended from it.
			void OnXamlChanged(object? _, EventArgs __) => additional.Axaml = uiControl.Xaml;
			uiControl.XamlChanged += OnXamlChanged;
			additional.OnDispose += (_, _) => uiControl.XamlChanged -= OnXamlChanged;

			host.AddAttachment(additional);
			return true;
		}
	}
}
