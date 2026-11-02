using LLMDesktopAssistant.LLM.Services;
using LLMDesktopAssistant.Localization;

namespace LLMDesktopAssistant.Startup.Stages
{
	/// <summary>
	/// The stage that initializes the chat-side container. Its body lives in the static constructor
	/// of <see cref="ChatServices"/>; touching the type from here is what keeps that cost inside a
	/// named stage instead of a surprise inside the first view model.
	/// </summary>
	public sealed class ChatServicesStage : IStartupStage
	{
		/// <inheritdoc />
		public LocaleKeyBase Title => Locale.GetKey("splash.stage.chats");

		/// <inheritdoc />
		public Task RunAsync(CancellationToken cancellationToken = default)
		{
			cancellationToken.ThrowIfCancellationRequested();

			_ = ChatServices.ManagementService;

			return Task.CompletedTask;
		}
	}
}
