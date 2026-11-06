using AsyncLua.Values;
using LLMDesktopAssistant.Hooks;
using LLMDesktopAssistant.StructuredValues;
using LLMDesktopAssistant.StructuredValues.Converters;
using Serilog;

namespace LLMDesktopAssistant.Scripting.Lua.Addons
{
	public class LuaScriptHookExecutor(HookInfo hook) : IHookExecutor
	{
		/// <inheritdoc/>
		public async Task<INodeValue?> ExecuteAsync(IServiceProvider services, INodeValue? payload,
			INodeValue? previous, string hookType, string? hookName, CancellationToken cancellationToken = default)
		{
			var luaService = services.GetRequiredService<LuaService>();

			var luaPayload = hook.InputMode is ScriptInputMode.Json
				? new LuaString(payload.ToJsonNode()?.ToJsonString() ?? "null")
				: payload.ToLuaValue();
			var luaPrevious = hook.InputMode is ScriptInputMode.Json
				? new LuaString(previous.ToJsonNode()?.ToJsonString() ?? "null")
				: previous.ToLuaValue();

			LuaTuple scriptResult;
			try
			{
				scriptResult = await luaService.ExecuteAsync(hook.Body, _ => { }, globals =>
				{
					globals["payload"] = luaPayload;
					globals["previous"] = luaPrevious;
					globals["hook_type"] = new LuaString(hookType);
					globals["hook_name"] = string.IsNullOrEmpty(hookName) ? LuaNil.Instance : new LuaString(hookName);
				}, cancellationToken);
			}
			catch (OperationCanceledException)
			{
				throw;
			}
			catch (Exception ex)
			{
				Log.Debug(ex, "Error executing Lua script hook {HookName} of type {HookType} on key {HookKey}: {Error}",
					hook.Name, hookType, hookName, ex.Message);
				return null;
			}

			return scriptResult.First.ToConstNodeValue(tolerant: true);
		}
	}
}
