using System.Text;
using AsyncLua.Values;
using LLMDesktopAssistant.Prompting.Context;
using LLMDesktopAssistant.Services;
using Microsoft.Extensions.DependencyInjection;

namespace LLMDesktopAssistant.Scripting.Lua
{
	/// <summary>
	/// The shared runner of a scriptable prompt context written in Lua: the body executes in the chat's Lua state,
	/// <c>print(...)</c> output is the context text, and the previous rendered text is exposed as the
	/// <c>previous</c> global.
	/// </summary>
	/// <remarks>
	/// The provider <em>is</em> the run: the concrete provider decides what the text means - a current-request
	/// live tail or a persisted supersede stamp. This base only knows how to execute a Lua body; nothing here arms
	/// a timeout or catches anything - the budget and the failure isolation belong to the caller
	/// (<c>PromptContextBudget</c>), so every language is treated identically.
	/// </remarks>
	public abstract class LuaScriptContextProviderBase
	{
		/// <summary>Initializes a new instance of the <see cref="LuaScriptContextProviderBase"/> class.</summary>
		protected LuaScriptContextProviderBase(PromptContextInfo addon) => Addon = addon;

		/// <summary>The context addon this provider runs.</summary>
		protected PromptContextInfo Addon { get; }

		/// <summary>Runs the body in the chat's Lua state and returns what it printed.</summary>
		protected async Task<string?> RunAsync(PromptContextExecutionContext context, string? previous,
			CancellationToken cancellationToken)
		{
			var luaService = context.Services.GetRequiredService<LuaService>();
			var output = new StringBuilder();

			await luaService.ExecuteAsync(Addon.Body, text => output.AppendLine(text),
				globals => globals["previous"] = previous is null
					? LuaNil.Instance
					: LuaValueConverter.ToLuaValue(previous),
				cancellationToken);

			return output.ToString();
		}
	}
}
