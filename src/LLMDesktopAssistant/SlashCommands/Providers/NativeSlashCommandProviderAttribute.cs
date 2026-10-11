namespace LLMDesktopAssistant.SlashCommands.Providers
{
	/// <summary>
	/// Marks a <see cref="NativeSlashCommandProvider"/> so that it is auto-registered as an
	/// <see cref="ISlashCommandProvider"/>, mirroring <c>ToolModuleAttribute</c>.
	/// </summary>
	/// <remarks>
	/// The registration itself is done by the native provider service configurators: a chat-scoped provider becomes a
	/// chat-scoped service, an app-scoped one a singleton.
	/// </remarks>
	[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
	public sealed class NativeSlashCommandProviderAttribute(bool chatScoped = true) : Attribute
	{
		/// <summary>
		/// Whether the provider is scoped to a chat (the default) or to the application.
		/// </summary>
		public bool ChatScoped { get; } = chatScoped;
	}
}
