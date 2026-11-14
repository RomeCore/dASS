using System.ComponentModel;
using LLMDesktopAssistant.LLM.MVVM.Additional;

namespace LLMDesktopAssistant.LLM.MVVM.Additional.Chat
{
	/// <summary>
	/// The base for additional chat data that brings its own presentation surface.
	/// </summary>
	/// <remarks>
	/// The item is the envelope: it holds the payload and the surface-specific placement, so the side panel
	/// keeps skipping every item by type and a new surface never has to edit it. Changes of the payload that
	/// decide the envelope's own <see cref="Order"/> and <see cref="IsEffectivelyVisible"/> are re-raised as the
	/// envelope's, so a surface subscribes to the envelope and never to the payload.
	/// </remarks>
	public class AdditionalChatDataItemBase : AdditionalChatData, IChatServicesAttachable
	{
		/// <summary>
		/// Gets or sets the payload this envelope presents.
		/// </summary>
		public required AdditionalChatData Content
		{
			get;
			init
			{
				ArgumentNullException.ThrowIfNull(value);
				field = value;
				ReplacementKey = value.ReplacementKey;
				value.PropertyChanged += OnContentPropertyChanged;
			}
		}

		/// <summary>
		/// Gets the order of the payload: the envelope has no order of its own.
		/// </summary>
		public override int Order => Content.Order;

		/// <summary>
		/// Gets a value indicating whether the envelope and its payload are both visible.
		/// </summary>
		public bool IsEffectivelyVisible => IsVisible && Content.IsVisible;

		/// <summary>
		/// Takes over what a script's update must not reset when the envelope is replaced under one
		/// <see cref="AdditionalChatData.ReplacementKey"/>.
		/// </summary>
		/// <remarks>
		/// Nothing is carried over by default: what the payload shows is the script's to declare on every append.
		/// A surface that owns presentation state of its own - the place the user dragged a window to, the state
		/// they put it in - overrides this, so the collection can stay indifferent to which surfaces exist.
		/// </remarks>
		/// <param name="previous">The envelope this one replaces.</param>
		public virtual void InheritFrom(AdditionalChatDataItemBase previous)
		{
		}

		/// <summary>
		/// The payload decides the order and the visibility, so its changes are re-raised as the envelope's own.
		/// </summary>
		/// <remarks>
		/// A surface that derives further computed members from <see cref="IsEffectivelyVisible"/> overrides this
		/// and calls the base first.
		/// </remarks>
		protected virtual void OnContentPropertyChanged(object? sender, PropertyChangedEventArgs e)
		{
			if (e.PropertyName is null or nameof(IsVisible))
				RaisePropertyChanged(nameof(IsEffectivelyVisible));

			if (e.PropertyName is null or nameof(Order))
				RaisePropertyChanged(nameof(Order));
		}

		protected override void Dispose(bool disposing)
		{
			base.Dispose(disposing);

			if (disposing && !Content.ManualDispose)
			{
				Content.PropertyChanged -= OnContentPropertyChanged;
				Content.Dispose();
			}
		}

		/// <summary>
		/// Forwards the chat's service scope to the payload.
		/// </summary>
		/// <param name="services">The chat's service scope.</param>
		public void AttachChatServices(IServiceProvider services)
		{
			if (Content is IChatServicesAttachable attachable)
				attachable.AttachChatServices(services);
		}
	}
}
