using LLMDesktopAssistant.Agents;
using LLMDesktopAssistant.LLM.Services.Prompting;

namespace LLMDesktopAssistant.Prompting.Context
{
	/// <summary>
	/// Base class for prompt sections: delegates all work to the four typed services:
	/// state provider, state renderer, delta provider and delta renderer.
	/// </summary>
	public abstract class PromptAnchoredSectionBase<TState, TDelta> : IPromptAnchoredSectionProvider
		where TState : PromptSectionStateBase
		where TDelta : PromptSectionDeltaBase
	{
		private readonly IPromptSectionStateProvider<TState> _stateProvider;
		private readonly IPromptSectionStateRenderer<TState> _stateRenderer;
		private readonly IPromptSectionDeltaProvider<TState, TDelta> _deltaProvider;
		private readonly IPromptSectionDeltaRenderer<TDelta> _deltaRenderer;

		protected PromptAnchoredSectionBase(IServiceProvider services)
		{
			_stateProvider = services.GetRequiredService<IPromptSectionStateProvider<TState>>();
			_stateRenderer = services.GetRequiredService<IPromptSectionStateRenderer<TState>>();
			_deltaProvider = services.GetRequiredService<IPromptSectionDeltaProvider<TState, TDelta>>();
			_deltaRenderer = services.GetRequiredService<IPromptSectionDeltaRenderer<TDelta>>();
		}

		public abstract string Discriminator { get; }

		/// <inheritdoc/>
		public async Task<PromptSectionStateBase?> CaptureStateAsync(ChatAgentDescriptor agent,
			CancellationToken cancellationToken = default)
			=> await _stateProvider.CaptureStateAsync(agent, cancellationToken);

		/// <inheritdoc/>
		public SystemPromptSnapshot RenderState(PromptSectionStateBase state) => _stateRenderer.Render((TState)state);

		/// <inheritdoc/>
		public string RenderDelta(PromptSectionDeltaBase delta) => _deltaRenderer.Render((TDelta)delta);

		/// <inheritdoc/>
		public async Task<PromptSectionDeltaBase?> CalculateDeltaAsync(PromptSectionStateBase? anchorState,
			IEnumerable<PromptSectionDeltaBase> existingDeltas, EffectiveChatContext context,
			CancellationToken cancellationToken = default)
			=> await _deltaProvider.CalculateDeltaAsync((TState?)anchorState, existingDeltas.Cast<TDelta>(), context,
				cancellationToken);
	}
}
