using LLMDesktopAssistant.Agents;
using LLMDesktopAssistant.LLM.Domain;
using RCLargeLanguageModels.Messages;

namespace LLMDesktopAssistant.Prompting.Hooks;

// Я министр документации

/// <summary>
/// Defines a mechanism for modifying individual messages or the final message list
/// during the prompt building phase in <see cref="LLM.Services.Prompting.ChatPromptBuilder"/>.
/// </summary>
/// <remarks>
/// <para>
/// Unlike <see cref="Injectors.IPromptInjector"/> (which inserts entire
/// <see cref="RawUserMessage"/> instances), <see cref="IPromptBuildingHook"/>
/// operates on <b>existing</b> messages - it can replace, remove, or annotate
/// individual <see cref="BranchedMessage"/> entries, or transform the final
/// list of LLM-native messages after conversion.
/// </para>
/// <para>
/// Hooks are executed <b>after</b> all injectors have run, during the conversion
/// phase. They are ordered via <see cref="Order"/> (lower values execute first).
/// All hooks should be registered in the DI container as
/// <c>IEnumerable&lt;IPromptBuildingHook&gt;</c>.
/// </para>
/// <para>
/// Typical use cases include:
/// <list type="bullet">
///   <item>Replacing a message with a <see cref="RawUserMessage"/> at the same position.</item>
///   <item>Removing a message from the context entirely (return <c>null</c>).</item>
///   <item>Adjusting branching metadata (e.g., switching the active branch).</item>
///   <item>Injecting agent-specific annotations just before the first user message.</item>
///   <item>Reordering or filtering the final LLM context.</item>
/// </list>
/// </para>
/// </remarks>
public interface IPromptBuildingHook
{
	/// <summary>
	/// Gets the execution order. Lower values are executed first.
	/// Default is <c>0</c>.
	/// </summary>
	int Order => 0;

	/// <summary>
	/// Allows final modifications to the complete list of LLM-native messages
	/// before they are returned to the caller.
	/// </summary>
	/// <param name="messages">
	/// The complete list of converted <see cref="IMessage"/> instances,
	/// including the system prompt message at index 0.
	/// </param>
	/// <param name="message">
	/// The <see cref="BranchedMessage"/> currently being processed,
	/// which contains the original chat message and its branching metadata.
	/// </param>
	/// <param name="agent">
	/// The target agent.
	/// </param>
	/// <returns>
	/// A modified enumerable of messages, or <c>null</c> to keep the list unchanged.
	/// </returns>
	IEnumerable<IMessage>? ModifyFinalContext(IEnumerable<IMessage> messages, BranchedMessage message, ChatAgentDescriptor agent);
}

