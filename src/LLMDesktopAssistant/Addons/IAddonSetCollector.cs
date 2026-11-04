using LLMDesktopAssistant.Agents;

namespace LLMDesktopAssistant.Addons
{
	/// <summary>
	/// Represents a collector for a set of addons. There are 3 types of sets: <br/>
	/// - available (one entry per <see cref="AddonBase{Self}.Key"/>, no configuration applied) <br/>
	/// - for chat (chat availability filtering and, where the kind has one, the chat-level set configuration) <br/>
	/// - for agent (both chat and agent availability filtering and the effective agent-level set configuration) <br/>
	/// Methods in this interface ALWAYS return the SAME instances for the SAME inputs.
	/// </summary>
	/// <remarks>
	/// "The same instance" is the <see cref="AddonSet{TAddon}"/> object, not the addons inside it: a set is re-resolved
	/// in place, so a caller that subscribed to it keeps seeing the changes. The addons of the chat and agent sets are
	/// frozen clones, so their identity intentionally differs from the available set, and mutating them throws.
	/// Enablement (<see cref="AddonChangedBase{Self,TChange}.Enabled"/>,
	/// <see cref="AddonChangedBase{Self,TChange}.Hidden"/>,
	/// <see cref="AddonChangedBase{Self,TChange}.IsFixed"/>) is a concern of addons derived from
	/// <see cref="AddonChangedBase{Self,TChange}"/> only; a plain <see cref="AddonBase{Self}"/> addon is always part of
	/// a set that its availability predicates allow.
	/// </remarks>
	/// <typeparam name="T">The type of addon.</typeparam>
	public interface IAddonSetCollector<T>
		where T : AddonBase<T>
	{
		/// <summary>
		/// Gets the available addons as a reactive <see cref="AddonSet{TAddon}"/>.
		/// The set holds one addon per <see cref="AddonBase{Self}.Key"/>: when several sources provide the same key, the
		/// winner is chosen by <see cref="AddonBase{Self}.OverrideOrder"/>, then <see cref="AddonBase{Self}.Order"/>, and
		/// the shadowed ones are attached to it in <see cref="AddonChangedBase{Self,TChange}.Overrides"/>.
		/// No set configuration is applied and no addon is dropped here, so invalid addons stay in the set.
		/// This method will ALWAYS return the same object, so this object can be used to subscribe to changes in the
		/// available addons set.
		/// </summary>
		/// <returns>The available addons as <see cref="AddonSet{TAddon}"/>.</returns>
		AddonSet<T> GetAvailableAddons();

		/// <summary>
		/// Gets the chat-level addons as a reactive <see cref="AddonSet{TAddon}"/>: the available addons that do not carry a
		/// fatal diagnostic, pass the chat availability predicate and are enabled by the chat-level set configuration.
		/// A kind whose configuration lives on the agent only (for example skills, sub-agents, tools or prompt contexts)
		/// has no chat-level configuration, and its chat set is the available set without the invalid addons.
		/// This method will ALWAYS return the same object, so this object can be used to subscribe to changes in the
		/// chat-level addons set.
		/// </summary>
		/// <remarks>
		/// This method compares the current effective configuration of the chat with the one the stored
		/// <see cref="AddonSet{TAddon}"/> was resolved for, and if it is a different object (for example the inheritance
		/// level has moved the configuration to another settings level), the set will be re-resolved in place and
		/// returned as the SAME instance. A configuration instance that is changed in place is followed through its
		/// change notifications instead.
		/// </remarks>
		/// <returns>The available chat-level addons as <see cref="AddonSet{TAddon}"/>.</returns>
		AddonSet<T> GetAddonsForChat();

		/// <summary>
		/// Gets the addons addressed to the specified agent as a reactive <see cref="AddonSet{TAddon}"/>: the available
		/// addons that do not carry a fatal diagnostic, pass both the chat and the agent availability predicates and are enabled
		/// by the effective agent-level set configuration. The chat level of the configuration is already resolved
		/// inside that effective configuration through the settings inheritance, so it is not applied a second time.
		/// This method will ALWAYS return the same object for the same agent, so this object can be used to subscribe to
		/// changes in the addons set of that agent.
		/// </summary>
		/// <remarks>
		/// "The same agent" means the same <see cref="ChatAgentDescriptor"/> instance: the sets are keyed by the
		/// descriptor reference and held weakly, so a new descriptor of the same agent starts a new set, and the set of a
		/// descriptor that is no longer referenced is dropped. As in <see cref="GetAddonsForChat"/>, a changed effective
		/// configuration re-resolves the set in place and the returned instance stays the same.
		/// </remarks>
		/// <param name="agent">The agent the set is requested for.</param>
		/// <returns>The available agent-level addons as <see cref="AddonSet{TAddon}"/>.</returns>
		/// <exception cref="ArgumentNullException"><paramref name="agent"/> is <see langword="null"/>.</exception>
		/// <exception cref="NotSupportedException">
		/// The collector declares neither an agent-addressed set nor a chat-level configuration to fall back to.
		/// </exception>
		AddonSet<T> GetAddonsForAgent(ChatAgentDescriptor agent);
	}
}
