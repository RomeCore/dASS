using System.Text.Json;
using System.Text.Json.Nodes;
using LLMDesktopAssistant.LLM.Domain;
using LLMDesktopAssistant.LLM.Services;
using LLMDesktopAssistant.LLM.Services.Agents;
using LLMDesktopAssistant.SlashCommands.Arguments;
using LLMDesktopAssistant.SlashCommands.Execution;
using LLMDesktopAssistant.Tools;
using LLMDesktopAssistant.Tools.Consents;
using LLMDesktopAssistant.Tools.Execution.Contexts;
using LLMDesktopAssistant.Tools.Execution.Dispatch;
using LLMDesktopAssistant.Tools.Execution.Domain;
using LLMDesktopAssistant.Tools.Execution.Sinks;
using Microsoft.Extensions.DependencyInjection;

namespace LLMDesktopAssistant.SlashCommands.Providers
{
	/// <summary>
	/// Runs <c>/tool:&lt;name&gt; key=value …</c>: assembles the arguments into the tool call the model would have
	/// emitted, attaches that call to the command's own message and runs it through the <c>Command</c> orchestrator.
	/// </summary>
	/// <remarks>
	/// The call is a <em>user</em> call: it lives on the user message, it is a card in the chat with its status,
	/// attachments and result, and it never pretends to be the generating agent's intent. The orchestration skips the
	/// policy and the confirmation - the user asked for this tool by name - and the descriptor travels as the plain
	/// available addon, because the per-agent overrides are a property of an agent decision. The command asks for no
	/// generation: a chain of <c>/tool:</c> calls is the user building a turn by hand, and the next message they write
	/// is what starts it.
	/// </remarks>
	public sealed class ToolCommandExecutor(ToolInfo tool) : ISlashCommandExecutor
	{
		/// <inheritdoc/>
		public SlashCommandArgumentSchema? ArgumentSchema { get; } = ToolArgumentSchemaMapper.Create(tool.ArgumentSchema);

		/// <inheritdoc/>
		public async Task<SlashCommandExecutionResult> ExecuteAsync(SlashCommandExecutionContext ctx, CancellationToken ct)
		{
			var call = new ToolCall
			{
				ToolCallId = $"dass-{Guid.NewGuid():N}",
				ToolName = tool.Name,
				Title = tool.NameKey,
				Arguments = BuildArguments(ctx.Arguments).ToJsonString(),
				CanBeCompacted = tool.CanBeCompacted
			};

			ctx.Message.ToolCalls.Add(call);

			var services = ctx.Services;

			await services.GetRequiredService<IToolInvocationDispatcher>().RunAsync(new ToolInvocationRequest
			{
				Source = ToolInvocationSource.Command,
				Call = call,
				Descriptor = tool,
				Confirmation = ConstantConfirmationSink.Approve,
				ContextFactory = new ChatToolContextFactory(
					ctx.Chat,
					services.GetRequiredService<IToolMemorizationService>(),
					services.GetRequiredService<IAgentManagementService>(),
					services.GetRequiredService<IChatSettingsService>()),
				Host = ctx.Message,
				RunningInUi = true
			}, ct);

			return SlashCommandExecutionResult.Ok(generate: false, modelFacingMode: ModelFacingMode.Raw);
		}

		/// <summary>
		/// Builds the tool call's argument object from the bound keyed arguments: the converted value when it already
		/// is a JSON node (an array or an object literal) and its JSON form otherwise. Unsupplied optional arguments
		/// contribute no key at all, so the tool sees exactly what the user wrote plus the schema defaults.
		/// </summary>
		private static JsonObject BuildArguments(SlashCommandBoundArguments arguments)
		{
			var result = new JsonObject();

			foreach (var (key, argument) in arguments.Keyed)
			{
				if (ToJsonNode(argument.Value) is { } value)
					result[key] = value;
			}

			return result;
		}

		private static JsonNode? ToJsonNode(object? value) => value switch
		{
			null => null,
			JsonNode node => node,
			string text => JsonValue.Create(text),
			bool flag => JsonValue.Create(flag),
			double number => JsonValue.Create(number),
			int number => JsonValue.Create(number),
			_ => JsonSerializer.SerializeToNode(value)
		};
	}
}
