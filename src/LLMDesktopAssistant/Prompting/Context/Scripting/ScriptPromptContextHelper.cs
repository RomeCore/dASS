namespace LLMDesktopAssistant.Prompting.Context.Scripting
{
	/// <summary>
	/// Shared rules of the scriptable prompt contexts: how the text is wrapped, how a script can never shadow a
	/// native section, and the timeout budget a context declares.
	/// </summary>
	/// <remarks>
	/// There is no runner here: a provider <em>is</em> the run, so the timeout and the failure isolation live in
	/// the provider the engine creates, next to the language that executes the body.
	/// </remarks>
	public static class ScriptPromptContextHelper
	{
		/// <summary>The timeout applied when the addon does not declare one.</summary>
		public const int DefaultTimeoutMs = 2000;

		/// <summary>The hard ceiling of the per-script timeout.</summary>
		public const int MaxTimeoutMs = 10000;

		/// <summary>
		/// Gets the discriminator of a scriptable context: the <c>script:</c> namespace plus the addon name,
		/// so a script can never shadow a native section.
		/// </summary>
		public static string GetDiscriminator(string name) => $"script:{name}";

		/// <summary>
		/// Gets the XML element name used for the wrapper. An element name cannot start with a digit,
		/// so such addon names are prefixed with an underscore.
		/// </summary>
		public static string GetTagName(string name)
			=> name.Length > 0 && char.IsAsciiDigit(name[0]) ? $"_{name}" : name;

		/// <summary>
		/// Wraps the script output into the XML tag the model sees.
		/// </summary>
		public static string Wrap(string name, string content)
		{
			var tag = GetTagName(name);
			return $"<{tag}>\n{content}\n</{tag}>";
		}
	}
}
