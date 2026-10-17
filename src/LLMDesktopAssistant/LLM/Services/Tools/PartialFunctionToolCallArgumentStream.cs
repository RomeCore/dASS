using LLMDesktopAssistant.Tools.Execution.Domain;
using RCLargeLanguageModels.Tools;

namespace LLMDesktopAssistant.LLM.Services.Tools
{
	/// <summary>
	/// Adapts the model's streaming tool call to the neutral <see cref="IToolArgumentStream"/>, so the
	/// execution core never sees a provider-specific type.
	/// </summary>
	/// <param name="call">The model's partial call.</param>
	internal sealed class PartialFunctionToolCallArgumentStream(PartialFunctionToolCall call) : IToolArgumentStream
	{
		private Task? _drained;

		/// <inheritdoc/>
		public string AccumulatedArgs => call.Args;

		/// <inheritdoc/>
		public Task Drained => _drained ??= AwaitDrainedAsync();

		/// <inheritdoc/>
		public IDisposable OnArgsUpdated(Action handler)
		{
			void OnArgsAdded(object? sender, string delta) => handler();

			call.ArgsPartAdded += OnArgsAdded;
			return new Disposable(() => call.ArgsPartAdded -= OnArgsAdded);
		}

		private async Task AwaitDrainedAsync() => await call;
	}
}
