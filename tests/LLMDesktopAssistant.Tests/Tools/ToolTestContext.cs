using System.Text.Json.Nodes;
using LLMDesktopAssistant.LLM.Domain;
using LLMDesktopAssistant.LLM.Services;
using LLMDesktopAssistant.Tools;
using LLMDesktopAssistant.Tools.Execution.Contexts;
using LLMDesktopAssistant.Tools.Execution.Domain;
using Microsoft.Extensions.DependencyInjection;
using RCLargeLanguageModels.Tasks;
using RCLargeLanguageModels.Tools;
using DomainToolCall = LLMDesktopAssistant.LLM.Domain.ToolCall;

namespace LLMDesktopAssistant.Tests.Tools
{
	/// <summary>
	/// Builds a real chat execution context for a tool, so tests exercise the same context the pipeline
	/// uses instead of a hand-made dummy.
	/// </summary>
	internal static class ToolTestContext
	{
		/// <summary>
		/// One prepared invocation: the call and its context.
		/// </summary>
		internal sealed class Session
		{
			public required DomainToolCall Call { get; init; }

			public required IToolExecutionContext Context { get; init; }
		}

		/// <summary>
		/// Creates a minimal tool descriptor over an empty executor, with optional metadata.
		/// </summary>
		/// <param name="name">The tool name.</param>
		/// <param name="configure">Mutates the initialization info before the descriptor is frozen.</param>
		/// <param name="streamingAnalyzer">The streaming analyzer delegate, or <see langword="null"/>.</param>
		/// <returns>The frozen descriptor.</returns>
		public static ToolInfo CreateTool(string name = "test-tool",
			Action<ToolInitializationInfo>? configure = null,
			Delegate? streamingAnalyzer = null)
		{
			var info = new ToolInitializationInfo
			{
				Name = name,
				Description = "A test tool.",
				Executor = (Action)(() => { }),
				StreamingAnalyzer = streamingAnalyzer
			};

			configure?.Invoke(info);
			return ToolInfo.Create(info);
		}

		/// <summary>
		/// Creates a real chat context with a chat, an assistant message and the call.
		/// </summary>
		/// <param name="tool">The descriptor the context is created for.</param>
		/// <param name="args">The model's arguments, or <see langword="null"/> for an empty set.</param>
		/// <param name="autoApprove">The invocation's auto-approve behaviours.</param>
		/// <param name="disallowed">The invocation's disallowed behaviours.</param>
		/// <param name="confirmation">The confirmation surface, or <see langword="null"/> for a denying one.</param>
		/// <param name="argumentStream">The model's argument stream, or <see langword="null"/>.</param>
		/// <param name="configureServices">Registers additional services into the chat scope.</param>
		/// <returns>The call and the context.</returns>
		public static Session Create(ToolInfo tool, JsonNode? args = null,
			ToolBehaviour autoApprove = ToolBehaviour.None,
			ToolBehaviour disallowed = ToolBehaviour.None,
			IConfirmationSink? confirmation = null,
			IToolArgumentStream? argumentStream = null,
			Action<IServiceCollection>? configureServices = null)
		{
			var services = new ServiceCollection();
			services.AddSingleton<IChatSettingsService, ChatSettingsService>();
			configureServices?.Invoke(services);
			var chat = new Chat(services.BuildServiceProvider());

			var message = new AssistantMessage
			{
				AgentStageId = Guid.Empty,
				SenderAgentId = Guid.Empty,
				Status = AssistantMessageStatus.Success
			};
			chat.Messages.Add(message);

			var call = new DomainToolCall
			{
				ToolCallId = ToolCallId.Generate(),
				ToolName = tool.Name,
				Arguments = args?.ToJsonString() ?? "{}"
			};
			message.ToolCalls.Add(call);

			var parameters = new ToolContextCreationParams
			{
				Call = call,
				Descriptor = tool,
				Source = ToolInvocationSource.ChatRun,
				Host = message,
				RunningInUi = false,
				Confirmation = confirmation ?? new DeniedConfirmationSink(),
				EffectiveBehaviours = new ToolPolicyMask
				{
					AutoApproveBehaviours = autoApprove,
					DisallowedBehaviours = disallowed
				},
				ArgumentStream = argumentStream
			};

			return new Session
			{
				Call = call,
				Context = new ChatToolExecutionContext(chat, message, parameters, null)
			};
		}

		/// <summary>
		/// Runs the tool's main executor and returns the resulting call.
		/// </summary>
		/// <param name="tool">The descriptor to execute.</param>
		/// <param name="args">The model's arguments, or <see langword="null"/>.</param>
		/// <returns>The call the tool wrote into.</returns>
		public static async Task<DomainToolCall> ExecuteAsync(ToolInfo tool, JsonNode? args = null)
		{
			var session = Create(tool, args);
			await tool.Main.ExecuteAsync(args, session.Context, CancellationToken.None);
			return session.Call;
		}

		private sealed class DeniedConfirmationSink : IConfirmationSink
		{
			public Task<ToolApprovalResult> RequestAsync(IToolExecutionContext ctx, CancellationToken ct) =>
				Task.FromResult(ToolApprovalResult.DeniedOnce("Tests do not confirm tool executions."));
		}
	}
}
