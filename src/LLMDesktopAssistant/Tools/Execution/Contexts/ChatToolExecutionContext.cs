using System.Text.Json.Nodes;
using LLMDesktopAssistant.LLM.Domain;
using LLMDesktopAssistant.Tools.Consents;
using LLMDesktopAssistant.Tools.Execution.Domain;
using LLMDesktopAssistant.Tools.Execution.Support;

namespace LLMDesktopAssistant.Tools.Execution.Contexts
{
	/// <summary>
	/// The chat world's execution context. It adds the chat message (the owner of the call and the
	/// source of the message id) to the neutral contract.
	/// </summary>
	/// <remarks>
	/// A chat tool that needs the message or its id casts the context to this concrete type; nothing
	/// chat-specific enters <see cref="IToolExecutionContext"/>.
	/// </remarks>
	public sealed class ChatToolExecutionContext : IToolExecutionContext
	{
		private readonly Chat _chat;
		private readonly ChatMessage _message;

		/// <summary>
		/// Creates the chat context.
		/// </summary>
		/// <param name="chat">The chat the invocation belongs to.</param>
		/// <param name="message">The message that owns the call.</param>
		/// <param name="parameters">The source-supplied invocation inputs.</param>
		/// <param name="consent">The consent memorization context, or <see langword="null"/>.</param>
		public ChatToolExecutionContext(Chat chat, ChatMessage message, ToolContextCreationParams parameters,
			ToolConsentMemorizationContext? consent)
		{
			_chat = chat;
			_message = message;

			Call = parameters.Call;
			Descriptor = parameters.Descriptor;
			Host = parameters.Host;
			Services = chat.Services;
			Consent = consent;
			Source = parameters.Source;
			RunningInUi = parameters.RunningInUi;
			EffectiveBehaviours = parameters.EffectiveBehaviours;
			ArgumentStream = parameters.ArgumentStream;
			Arguments = new ToolArgumentsAccessor(Call, () => ArgumentsOverride);
		}

		/// <summary>
		/// The message that owns the call.
		/// </summary>
		public ChatMessage Message => _message;

		/// <summary>
		/// The chat the call executes in.
		/// </summary>
		public Chat Chat => _chat;

		Chat? IToolExecutionContext.Chat => _chat;

		/// <summary>
		/// The call being processed.
		/// </summary>
		public IToolCall Call { get; }

		/// <summary>
		/// The tool being executed.
		/// </summary>
		public IToolDescriptor Descriptor { get; }

		/// <summary>
		/// The message that owns the sibling calls.
		/// </summary>
		public IToolCallHost? Host { get; }

		/// <summary>
		/// The tool's own channel between its stages.
		/// </summary>
		public object? SharedContext { get; set; }

		/// <summary>
		/// The chat-scoped service provider. <c>[Inject]</c> parameters are resolved from here.
		/// </summary>
		public IServiceProvider Services { get; }

		/// <summary>
		/// Lets the tool persist its consent decisions, or <see langword="null"/>.
		/// </summary>
		public ToolConsentMemorizationContext? Consent { get; }

		/// <summary>
		/// The source that launched the invocation.
		/// </summary>
		public ToolInvocationSource Source { get; }

		/// <summary>
		/// Whether a user interface is available.
		/// </summary>
		public bool RunningInUi { get; }

		/// <summary>
		/// The pipeline's decision.
		/// </summary>
		public ToolPolicyDecision PolicyDecision { get; set; } = ToolPolicyDecision.None;

		/// <summary>
		/// The decision's source and explanation.
		/// </summary>
		public ToolPolicyReason? PolicyReason { get; set; }

		/// <summary>
		/// The effective behaviours of the invocation.
		/// </summary>
		public ToolPolicyMask EffectiveBehaviours { get; }

		/// <summary>
		/// The model's argument stream, when the invocation has one.
		/// </summary>
		public IToolArgumentStream? ArgumentStream { get; }

		/// <summary>
		/// An ephemeral argument substitution — the hook channel.
		/// </summary>
		public JsonNode? ArgumentsOverride { get; set; }

		/// <summary>
		/// The effective-arguments accessor shared by every stage.
		/// </summary>
		public IToolArgumentsAccessor Arguments { get; }

		/// <summary>
		/// Finds the message id of the message that owns the call.
		/// </summary>
		/// <returns>The message id, or 0 when the message is not in the chat any more.</returns>
		public int FindMessageId() => _chat.Messages.FirstOrDefault(m => m.Message == _message)?.MessageId ?? 0;
	}
}
