using LLMDesktopAssistant.Addons.Sources;
using LLMDesktopAssistant.LLM.Services;
using LLMDesktopAssistant.Scripting.Lua.API;

namespace LLMDesktopAssistant.Scripting.Lua
{
	/// <summary>
	/// Contributes one native <see cref="LuaScriptInfo"/> per registered <see cref="LuaApiBase"/>.
	/// </summary>
	[ChatService(typeof(IAdditionalAddonSource<LuaScriptInfo>))]
	public class NativeLuaApiAddonSource(IEnumerable<LuaApiBase> nativeApis)
		: StaticAddonSource<LuaScriptInfo>(nativeApis.Select(api => new LuaScriptInfo
		{
			Enabled = true,
			OverrideOrder = 1,
			IsNative = true,
			Name = "%native+" + api.GetType().Name,
			Namespace = api.Namespace ?? string.Empty,
			Manuals = api.Manuals,
			Loader = (globals, ns, lua) => api.Populate(globals, ns!, lua)
		}));
}
