using System.Text.Json.Nodes;
using LLMDesktopAssistant.Hooks;
using LLMDesktopAssistant.LLM.Domain;
using LLMDesktopAssistant.Services;
using LLMDesktopAssistant.Tools;
using LLMDesktopAssistant.Tools.Execution.Domain;
using LLMDesktopAssistant.Tools.Execution.Hooks;
using LLMDesktopAssistant.Utils;
using Serilog;

namespace LLMDesktopAssistant.Tools.Execution.Services
{
	/// <summary>
	/// The pre-use hook point: fires <c>tool.pre-use</c> before the argument set is validated and before
	/// the synchronization keys are derived from it, and either vetoes the invocation or replaces the
	/// argument set the whole rest of the pipeline sees.
	/// </summary>
	/// <remarks>
	/// <para>
	/// This is the only point at which a hook may rewrite arguments. The synchronization keys, the
	/// specifier verdict and the executor input are all derived from the effective set, so a rewrite after
	/// this stage would make those findings describe a call that never runs.
	/// </para>
	/// <para>
	/// A veto is an ordinary failed call, not an exception: the status and the model-facing text are
	/// written here, and settlement leaves them alone because the result is already non-empty.
	/// </para>
	/// <para>
	/// Invocations launched by a script are not hooked. A script is already outside policy and
	/// confirmation, and firing the hooks here would run the same hook a second time from inside its own
	/// tool call - a hook written in Lua that inspects a file would recurse until it timed out.
	/// </para>
	/// </remarks>
	[Service(typeof(IToolPreHookExecutionService))]
	public class ToolPreHookExecutionService : IToolPreHookExecutionService
	{
		/// <summary>
		/// The event fired before a tool is used.
		/// </summary>
		private const string hookType = "tool.pre-use";

		/// <summary>
		/// The text the model reads when a hook vetoes a call without explaining itself.
		/// </summary>
		private const string defaultDenial = "The tool execution was blocked by a hook.";

		/// <inheritdoc/>
		public async Task<bool> RunAsync(IToolExecutionContext ctx, CancellationToken ct)
		{
			// A script's own tool calls must not re-enter the hooks of the invocation that is running it.
			if (ctx.Source is ToolInvocationSource.Script)
				return true;

			if (ctx.Services.GetService<IHookDispatcher>() is not { } hookDispatcher)
				return true;

			var result = await hookDispatcher.ExecuteHooksAsync<ToolPreUseHookVerdict>(
				CreatePayload(ctx), hookType, ctx.Call.ToolName, HookExecutionFlow.Sequential,
				cancellationToken: ct);

			// A hook that stopped the chain itself - a blocking exit code or a crash of an out-of-process
			// hook - wins over any verdict, because the chain never produced one.
			if (!string.IsNullOrEmpty(result.BlockingMessage))
			{
				ctx.Call.Status = ToolStatus.Error;
				ctx.Call.ResultContent = result.BlockingMessage;
				return false;
			}

			var verdict = result.Value;
			if (verdict is null)
				return true;

			// Approve and ask mean the same thing here: the policy stage has not decided yet, so this point only
			// has the veto left to say. A combination of flags is never a decision and is reported, not obeyed.
			switch (verdict.Decision)
			{
				case null or ToolPolicyDecision.None or ToolPolicyDecision.Approve or ToolPolicyDecision.Ask:
					break;

				case ToolPolicyDecision.Disallow:
					ctx.Call.Status = ToolStatus.Error;
					ctx.Call.ResultContent = string.IsNullOrWhiteSpace(verdict.Reason) ? defaultDenial : verdict.Reason;
					return false;

				default:
					Log.Warning("Hook {HookType} on tool {ToolName} returned {Decision}, which is never a single decision; " +
						"ignored.", hookType, ctx.Call.ToolName, verdict.Decision);
					break;
			}

			if (verdict.Arguments is JsonObject arguments)
			{
				if (verdict.PersistsArguments)
				{
					ctx.ArgumentsOverride = null;
					ctx.Call.Arguments = arguments.ToJsonString(JsonSerializationOpts.UnsafeEscaping);
				}
				else
					ctx.ArgumentsOverride = arguments;
			}
			else if (verdict.Arguments is not null)
				Log.Warning("Hook {HookType} on tool {ToolName} returned an argument set that is not an object " +
					"and cannot replace the model's arguments.", hookType, ctx.Call.ToolName);

			return true;
		}

		/// <summary>
		/// Builds the document the hooks receive.
		/// </summary>
		/// <remarks>
		/// The argument set is the effective one on purpose: a hook validates what will run, not what the
		/// model typed, and an override an earlier hook installed is already part of it.
		/// </remarks>
		/// <param name="ctx">The invocation context.</param>
		/// <returns>The payload of one <c>tool.pre-use</c> event.</returns>
		private static ToolPreUseHookPayload CreatePayload(IToolExecutionContext ctx)
		{
			var call = ctx.Call;
			var canonicalName = ctx.Descriptor.Name;

			return new ToolPreUseHookPayload
			{
				Tool = string.IsNullOrEmpty(canonicalName) ? call.ToolName : canonicalName,
				Behaviour = DescribeBehaviour(call.ExpectedBehaviour ?? ctx.Descriptor.DefaultExpectedBehaviour),
				Source = KebabEnumNames<ToolInvocationSource>.Serialize(ctx.Source) ?? ctx.Source.ToString(),
				Arguments = ctx.Arguments.Effective,
				ChatId = ctx.Chat?.Id,
				AgentId = (ctx.Host as AssistantMessage)?.SenderAgentId
			};
		}

		/// <summary>
		/// Renders behaviour flags as kebab-case names, dropping the two members that are not flags:
		/// <see cref="ToolBehaviour.None"/> is set on every value and <see cref="ToolBehaviour.All"/> is set
		/// only when every flag is.
		/// </summary>
		/// <param name="behaviour">The behaviour flags of the call.</param>
		/// <returns>The names, or <see langword="null"/> when nothing is declared.</returns>
		private static string[]? DescribeBehaviour(ToolBehaviour behaviour)
		{
			var names = Enum.GetValues<ToolBehaviour>()
				.Where(flag => flag is not ToolBehaviour.None and not ToolBehaviour.All)
				.Where(flag => behaviour.HasFlag(flag))
				.Select(flag => KebabEnumNames<ToolBehaviour>.Names[flag])
				.ToArray();

			return names.Length == 0 ? null : names;
		}
	}
}
