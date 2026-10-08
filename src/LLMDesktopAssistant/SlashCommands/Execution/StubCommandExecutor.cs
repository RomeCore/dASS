namespace LLMDesktopAssistant.SlashCommands.Execution
{
	/// <summary>
	/// A no-op executor used as the default on <see cref="SlashCommandInfo"/> until the real <c>/skill</c> and
	/// <c>/agent</c> executors land (Stage 3), when it is deleted.
	/// </summary>
	public sealed class StubCommandExecutor : ISlashCommandExecutor
	{
		/// <summary>
		/// The shared stateless instance.
		/// </summary>
		public static readonly StubCommandExecutor Instance = new();

		private StubCommandExecutor()
		{
		}

		/// <inheritdoc/>
		public Task<SlashCommandExecutionResult> ExecuteAsync(SlashCommandExecutionContext ctx, CancellationToken ct)
			=> Task.FromResult(SlashCommandExecutionResult.Ok());
	}
}
