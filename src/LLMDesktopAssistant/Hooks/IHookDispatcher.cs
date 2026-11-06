using LLMDesktopAssistant.StructuredValues;

namespace LLMDesktopAssistant.Hooks
{
	/// <summary>
	/// Dispatches a hook event to the hooks of the chat, honouring their flow, order and timeouts, and
	/// returns the value of the last sequential hook.
	/// </summary>
	/// <remarks>
	/// Every hook the caller waits on is bounded: by its own <see cref="HookInfo.TimeoutMs"/>, capped at the
	/// dispatcher ceiling, or by the dispatcher default when the hook declares none; a hook that exceeds the
	/// bound is cancelled and treated as having no opinion. A fire-and-forget hook is bounded only by the
	/// timeout it declares itself, with no default and no ceiling, because nobody waits for it.
	/// </remarks>
	public interface IHookDispatcher
	{
		/// <summary>
		/// Runs every hook that matches the event and the key, and returns the value of the last sequential
		/// hook, or <see langword="null"/> when no sequential hook produced one.
		/// </summary>
		/// <param name="payload">The neutral document the hooks receive as their input.</param>
		/// <param name="hookType">
		/// The event being fired; a hook matches it with its <see cref="HookInfo.HookType"/> glob.
		/// </param>
		/// <param name="hookName">
		/// The identity inside the event - a tool name, a command name - or <see langword="null"/> when the
		/// event has none. A hook matches it with its <see cref="HookInfo.HookName"/> glob.
		/// </param>
		/// <param name="defaultFlow">The flow applied to a hook that declares none.</param>
		/// <param name="forcedFlow">
		/// A flow that overrides both the hook's declaration and <paramref name="defaultFlow"/>, or
		/// <see langword="null"/> to honour them.
		/// </param>
		/// <param name="cancellationToken">
		/// Cancellation of the awaited hooks. Fire-and-forget hooks ignore it: nobody waits for them.
		/// </param>
		/// <returns>The value of the last sequential hook, or <see langword="null"/>.</returns>
		Task<INodeValue?> ExecuteHooksAsync(INodeValue? payload,
			string hookType, string? hookName, HookExecutionFlow defaultFlow,
			HookExecutionFlow? forcedFlow = null, CancellationToken cancellationToken = default);
	}
}
