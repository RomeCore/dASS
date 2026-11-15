using AsyncLua;
using AsyncLua.Values;
using LLMDesktopAssistant.Hooks;
using LLMDesktopAssistant.StructuredValues;
using LLMDesktopAssistant.StructuredValues.Converters;
using Serilog;

namespace LLMDesktopAssistant.Scripting.Lua.Addons
{
	/// <summary>
	/// Runs a Lua function registered at run time with <c>dass.hook.register</c>.
	/// </summary>
	/// <remarks>
	/// The function receives the vocabulary a file hook reads from its globals, positionally:
	/// <c>(payload, previous, hook_type, hook_target)</c>. It answers with <c>value, blocking_message</c>, the same
	/// pair every engine reports through <see cref="HookExecutionResult"/>.
	/// </remarks>
	public class LuaFunctionHookExecutor(LuaFunction function, LuaCallingContext context) : IHookExecutor
	{
		/// <inheritdoc/>
		public async Task<HookExecutionResult> ExecuteAsync(IServiceProvider services, INodeValue? payload,
			INodeValue? previous, string hookType, string? hookTarget, INodeValue? parameters = null,
			CancellationToken cancellationToken = default)
		{
			try
			{
				// The registering call is long over: its own token would abort the hook on the first instruction.
				var result = await function.InvokeAsync(context.WithCancellation(cancellationToken),
				[
					payload.ToLuaValue(),
					previous.ToLuaValue(),
					new LuaString(hookType),
					string.IsNullOrEmpty(hookTarget) ? LuaNil.Instance : new LuaString(hookTarget)
				]);

				return result.ToHookResult(hookType, hookTarget);
			}
			catch (OperationCanceledException)
			{
				throw;
			}
			catch (Exception ex)
			{
				Log.Warning(ex, "Error executing a runtime Lua hook of type {HookType} on target {HookTarget}: {Error}",
					hookType, hookTarget, ex.Message);
				return new HookExecutionResult(null, null);
			}
		}
	}
}
