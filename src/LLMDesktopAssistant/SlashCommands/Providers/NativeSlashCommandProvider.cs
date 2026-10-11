namespace LLMDesktopAssistant.SlashCommands.Providers
{
	/// <summary>
	/// The base of every native (built-in) slash command provider. A concrete provider builds its commands in its
	/// constructor through <see cref="AddCommand"/>, which stamps the native tier and freezes each command.
	/// </summary>
	/// <remarks>
	/// Native commands are static: they do not come from a pack and are built once. Unlike
	/// <see cref="DerivedSlashCommandProvider{TSource}"/>, nothing is re-derived, so the command list is populated in
	/// the constructor and returned verbatim. Any per-chat override still applies through the command set collector,
	/// which clones and re-freezes the command.
	/// </remarks>
	public abstract class NativeSlashCommandProvider : ISlashCommandProvider
	{
		private readonly List<SlashCommandInfo> _commands = [];

		/// <summary>
		/// Registers a native command: stamps <see cref="SlashCommandSource.Native"/> and the native priority tier,
		/// freezes it (the last mutation a command ever takes) and appends it to this provider's command list.
		/// </summary>
		/// <param name="command">The fully-built command (name, executor, arguments, aliases, keys).</param>
		protected void AddCommand(SlashCommandInfo command)
		{
			command.OverrideOrder = SlashCommandOrderTiers.Native;
			command.SourceKind = SlashCommandSource.Native;

			command.Freeze();
			_commands.Add(command);
		}

		/// <inheritdoc/>
		public IEnumerable<SlashCommandInfo> GetCommands() => _commands;
	}
}
