namespace LLMDesktopAssistant.LLM.MVVM.Messages
{
	/// <summary>
	/// The base class for the parts a message is composed of (reasoning, text, tool calls).
	/// Parts are created eagerly and manage their own visibility.
	/// </summary>
	public abstract class MessagePartViewModel : ViewModelBase
	{
		private bool _isVisible = true;
		/// <summary>
		/// Gets or sets a value indicating whether this part should be rendered.
		/// </summary>
		public bool IsVisible
		{
			get => _isVisible;
			set => SetProperty(ref _isVisible, value);
		}
	}
}
