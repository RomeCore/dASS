using AsyncLua;
using LLMDesktopAssistant.LLM.MVVM.Additional;
using LLMDesktopAssistant.Utils.Attachments;

namespace LLMDesktopAssistant.Scripting.Lua
{
	/// <summary>
	/// Appends a <see cref="LuaAttachmentPartOptions"/> to a host on behalf of the three Lua surfaces - the chat, a
	/// message handle and the tool result - so they cannot drift apart in how they build or place a part.
	/// </summary>
	internal static class LuaAttachmentPartAppender
	{
		/// <summary>
		/// Parses the spec, then either updates the part already carrying its <c>replacement_key</c> or appends a
		/// new one.
		/// </summary>
		/// <remarks>
		/// A matching key whose part is of another type is left to the collection, which replaces it and disposes
		/// it - the part types are not mutable into each other.
		/// </remarks>
		/// <param name="host">The chat, message or tool call the part belongs to.</param>
		/// <param name="spec">The argument the script passed, expected to be a spec table.</param>
		/// <param name="call">The Lua call being executed, for the error message.</param>
		/// <returns>The part that is now attached to the host.</returns>
		/// <exception cref="LuaRuntimeException">The spec is not a table or the table is malformed.</exception>
		public static AdditionalChatData Append(IChatAttachmentHost host, object? spec, string call)
		{
			var options = LuaAttachmentPartOptions.Parse(spec, call);

			if (options.ReplacementKey is { } key && FindInPlace(host, key, options) is { } existing)
			{
				// Updating one part beats replacing it: a progress chip redrawn every second would otherwise churn
				// the collection, the wrap panel and a dispose per update.
				options.ApplyTo(existing);
				return existing;
			}

			var part = options.Create();
			host.AddAttachment(part);
			return part;
		}

		/// <summary>
		/// Finds the part this spec should update instead of replacing, if there is one.
		/// </summary>
		private static AdditionalMessagePart? FindInPlace(IChatAttachmentHost host, string key,
			LuaAttachmentPartOptions options)
		{
			// Materialised: the caller mutates the collection this walks right after the lookup.
			foreach (var attachment in host.GetAttachments().ToList())
			{
				if (attachment is not AdditionalMessagePart part)
					continue;

				if (part.ReplacementKey == key && part.GetType() == options.PartType)
					return part;
			}

			return null;
		}
	}
}
