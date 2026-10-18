using System.Text.Json.Nodes;
using LLMDesktopAssistant.LLM.Domain;
using LLMDesktopAssistant.Tools.Consents;
using LLMDesktopAssistant.Tools.Execution.Domain;
using LLMDesktopAssistant.Tools.Execution.Support;

namespace LLMDesktopAssistant.Tools.Execution.Contexts
{
	/// <summary>
	/// The chat world's context for a script invocation (<c>dass.tool.call</c>). It carries the chat scope
	/// but has no owning message and no consent memorization.
	/// </summary>
	/// <remarks>
	/// A script may run as a slash command, where there is no message at all, so a message-carrying
	/// context cannot be built. Policy and confirmation are skipped for the script source, which is why
	/// <see cref="Consent"/> is <see langword="null"/>: there is nothing to memorize and no sender agent
	/// to memorize it for.
	/// </remarks>
	public sealed class ChatScriptToolExecutionContext : IToolExecutionContext
	{
		private readonly Chat _chat;

		/// <summary>
		/// Creates the script context.
		/// </summary>
		/// <param name="chat">The chat the script belongs to.</param>
		/// <param name="parameters">The source-supplied invocation inputs.</param>
		public ChatScriptToolExecutionContext(Chat chat, ToolContextCreationParams parameters)
		{
			_chat = chat;

			Call = parameters.Call;
			Descriptor = parameters.Descriptor;
			HandledDecisions = parameters.Descriptor.HandledDecisions;
			Host = parameters.Host;
			Confirmation = parameters.Confirmation;
			Services = chat.Services;
			Source = parameters.Source;
			RunningInUi = parameters.RunningInUi;
			EffectiveBehaviours = parameters.EffectiveBehaviours;
			ArgumentStream = parameters.ArgumentStream;
			Arguments = new ToolArgumentsAccessor(Call, () => ArgumentsOverride);
		}

		/// <summary>
		/// The chat the call executes in.
		/// </summary>
		public Chat Chat => _chat;

		Chat? IToolExecutionContext.Chat => _chat;

		/// <inheritdoc/>
		public IToolCall Call { get; }

		/// <inheritdoc/>
		public IToolDescriptor Descriptor { get; }

		/// <inheritdoc/>
		public IToolCallHost? Host { get; }

		/// <inheritdoc/>
		public IConfirmationSink Confirmation { get; }

		/// <inheritdoc/>
		public object? SharedContext { get; set; }

		/// <summary>
		/// The chat-scoped service provider.
		/// </summary>
		public IServiceProvider Services { get; }

		/// <summary>
		/// Always <see langword="null"/>: a script invocation has no consent memorization.
		/// </summary>
		public ToolConsentMemorizationContext? Consent => null;

		/// <inheritdoc/>
		public ToolInvocationSource Source { get; }

		/// <inheritdoc/>
		public bool RunningInUi { get; }

		/// <inheritdoc/>
		public ToolPolicyDecision PolicyDecision { get; set; } = ToolPolicyDecision.None;

		/// <inheritdoc/>
		public ToolPolicyDecision HandledDecisions { get; set; }

		/// <inheritdoc/>
		public ToolPolicyReason? PolicyReason { get; set; }

		/// <inheritdoc/>
		public JsonNode? ApprovalData { get; set; }

		/// <inheritdoc/>
		public ToolPolicyMask EffectiveBehaviours { get; }

		/// <inheritdoc/>
		public IToolArgumentStream? ArgumentStream { get; }

		/// <inheritdoc/>
		public JsonNode? ArgumentsOverride { get; set; }

		/// <inheritdoc/>
		public IToolArgumentsAccessor Arguments { get; }
	}
}
