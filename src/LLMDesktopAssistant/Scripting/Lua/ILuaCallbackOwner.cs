namespace LLMDesktopAssistant.Scripting.Lua
{
	public interface ILuaCallbackOwner
	{
		bool Contains(LuaCallbackRegistration registration);
		bool Unregister(LuaCallbackRegistration registration);
	}
}
