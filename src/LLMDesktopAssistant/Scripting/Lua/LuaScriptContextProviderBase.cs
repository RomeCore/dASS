using System.Text;
using AsyncLua.Values;
using LLMDesktopAssistant.Prompting.Context;
using LLMDesktopAssistant.Services;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace LLMDesktopAssistant.Scripting.Lua
{
	/// <summary>
	/// The shared runner of a scriptable prompt context written in Lua: the body executes in the chat's Lua state,
	/// <c>print(...)</c> output is the context text, and the previous rendered text is exposed as the
	/// <c>previous</c> global.
	/// </summary>
	/// <remarks>
	/// The provider <em>is</em> the run: the base class holds the timeout and the failure isolation (a script must
	/// never break the prompt build), and the concrete provider decides what the text means — a current-request
	/// live tail or a persisted supersede stamp.
	/// </remarks>
	public abstract class LuaScriptContextProviderBase
	{
		/// <summary>Initializes a new instance of the <see cref="LuaScriptContextProviderBase"/> class.</summary>
		protected LuaScriptContextProviderBase(PromptContextInfo addon) => Addon = addon;

		/// <summary>The context addon this provider runs.</summary>
		protected PromptContextInfo Addon { get; }

		/// <summary>
		/// Runs the body under the addon's timeout and turns any failure into "no context".
		/// </summary>
		protected async Task<string?> RunAsync(PromptContextExecutionContext context, string? previous,
			CancellationToken cancellationToken)
		{
			using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
			timeout.CancelAfter(Addon.TimeoutMs);

			try
			{
				var luaService = context.Services.GetRequiredService<LuaService>();
				var output = new StringBuilder();

				await luaService.ExecuteAsync(Addon.Body, text => output.AppendLine(text),
					globals => globals["previous"] = previous is null
						? LuaNil.Instance
						: LuaValueConverter.ToLuaValue(previous),
					timeout.Token);

				return output.ToString();
			}
			catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
			{
				Log.Warning("Prompt context script timed out after {Timeout} ms (context '{Context}').",
					Addon.TimeoutMs, Addon.Name);
				return null;
			}
			catch (Exception ex)
			{
				Log.Warning(ex, "Prompt context script failed (context '{Context}'): {Message}",
					Addon.Name, ex.Message);
				return null;
			}
		}
	}
}
