using Material.Icons;

namespace LLMDesktopAssistant.LLM.Services
{
	/// <summary>
	/// The default implementation of <see cref="IChatStatusService"/>.
	/// </summary>
	[ChatService(typeof(IChatStatusService))]
	public class ChatStatusService : NotifyPropertyChanged, IChatStatusService
	{
		private VisualIconKind _icon;
		/// <inheritdoc/>
		public VisualIconKind Icon
		{
			get => _icon;
			set => SetProperty(ref _icon, value);
		}

		private string? _text;
		/// <inheritdoc/>
		public string? Text
		{
			get => _text;
			set => SetProperty(ref _text, value);
		}
	}
}
