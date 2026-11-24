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
		public AdditionalChatData? TryResolve(IChatAttachment attachment)
		{
			if (attachment is not LuaUiControl uiControl)
				return null;

			var additional = new DynamicAxamlControlAdditionalData(uiControl.Axaml, uiControl.ViewModel);

			// Follow the handle: ui:set_axaml(axaml) re-renders every control appended from it.
			void OnAxamlChanged(object? _, EventArgs __) => additional.Axaml = uiControl.Axaml;
			uiControl.AxamlChanged += OnAxamlChanged;
			additional.OnDispose += (_, _) => uiControl.AxamlChanged -= OnAxamlChanged;

			return additional;
		}
	}
}
