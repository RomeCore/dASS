using LLMDesktopAssistant.SlashCommands.Arguments;
using Serilog;

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

		public SlashCommandArgumentSchema? ArgumentSchema => null;

		/// <inheritdoc/>
		public async Task<SlashCommandExecutionResult> ExecuteAsync(SlashCommandExecutionContext ctx, CancellationToken ct)
		{
			Log.Information("Executing stub command executor for {CommandName}", ctx.Token);
			await Task.Delay(2000);
			return SlashCommandExecutionResult.Ok();
		}
	}
}
