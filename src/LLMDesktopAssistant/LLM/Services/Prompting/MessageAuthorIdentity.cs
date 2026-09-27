namespace LLMDesktopAssistant.LLM.Services.Prompting
{
	public enum MessageAuthorIdentity
	{
		/// <summary>
		/// Uses the default author identity. Calculates based on the agent's or user's configuration.
		/// </summary>
		Default,

		/// <summary>
		/// Uses an anonymous author identity. The author is not identified.
		/// </summary>
		Anon,

		/// <summary>
		/// Uses an unnamed user identity. Reader will know that the author is a user.
		/// </summary>
		UnnamedUser,

		/// <summary>
		/// Uses an unnamed agent identity. Reader will know that the author is an agent.
		/// </summary>
		UnnamedAgent,

		/// <summary>
		/// Uses a named user identity. Reader will know that the author is a user with a specific name.
		/// </summary>
		NamedUser,

		/// <summary>
		/// Uses a named agent identity. Reader will know that the author is an agent with a specific name.
		/// </summary>
		NamedAgent,
	}
}
