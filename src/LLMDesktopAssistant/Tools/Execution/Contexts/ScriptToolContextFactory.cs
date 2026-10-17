using LLMDesktopAssistant.LLM.Domain;
using LLMDesktopAssistant.Tools.Execution.Domain;

namespace LLMDesktopAssistant.Tools.Execution.Contexts
{
	/// <summary>
	/// Creates contexts for script invocations: the chat scope without a host message and without consent
	/// memorization.
	/// </summary>
	/// <remarks>
	/// The chat entry point stamps <see cref="ChatToolContextFactory"/> on chat tools, and that factory
	/// requires a message. A nested <c>dass.tool.call</c> has no message, so the script entry constructs
	/// this factory and passes it in the request; the dispatcher never decides which world a call belongs
	/// to.
	/// </remarks>
	public sealed class ScriptToolContextFactory(Chat chat) : IToolExecutionContextFactory
	{
		/// <inheritdoc/>
		public IToolExecutionContext CreateContext(in ToolContextCreationParams parameters)
			=> new ChatScriptToolExecutionContext(chat, parameters);
	}
}
