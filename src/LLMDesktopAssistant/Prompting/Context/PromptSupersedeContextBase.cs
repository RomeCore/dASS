using LLMDesktopAssistant.LLM.Services.Prompting;

namespace LLMDesktopAssistant.Prompting.Context
{
	/// <summary>
	/// Base class for supersede contexts: delegates all work to the two typed services:
	/// stamp provider and stamp renderer.
	/// </summary>
	/// <typeparam name="TStamp">The type of the section stamp.</typeparam>
	public abstract class PromptSupersedeContextBase<TStamp> : IPromptSupersedeContextProvider
		where TStamp : PromptSupersedeStampBase
	{
		private readonly IPromptSupersedeStampProvider<TStamp> _stampProvider;
		private readonly IPromptSupersedeStampRenderer<TStamp> _stampRenderer;

		protected PromptSupersedeContextBase(IServiceProvider services)
		{
			_stampProvider = services.GetRequiredService<IPromptSupersedeStampProvider<TStamp>>();
			_stampRenderer = services.GetRequiredService<IPromptSupersedeStampRenderer<TStamp>>();
		}

		/// <inheritdoc/>
		public abstract string Discriminator { get; }

		/// <inheritdoc/>
		public Task<PromptSupersedeStampBase?> GetNewStampAsync(PromptSupersedeStampBase? previousStamp,
			PromptContextExecutionContext context, CancellationToken cancellationToken = default)
			=> Task.FromResult<PromptSupersedeStampBase?>(
				_stampProvider.CaptureStamp((TStamp?)previousStamp, context));

		/// <inheritdoc/>
		public string Render(PromptSupersedeStampBase stamp) => _stampRenderer.Render((TStamp)stamp);
	}
}
