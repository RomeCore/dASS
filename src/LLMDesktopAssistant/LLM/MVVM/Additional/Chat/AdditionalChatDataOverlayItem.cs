using System.ComponentModel;
using LiteDB;
using LLMDesktopAssistant.Controls.Icons;

namespace LLMDesktopAssistant.LLM.MVVM.Additional.Chat
{
	/// <summary>
	/// An additional chat data item that asks for an overlay window: a framed widget floating over the message
	/// sequence.
	/// </summary>
	/// <remarks>
	/// The window belongs to the host: the envelope owns the title, the chrome, the state and the geometry, while
	/// the payload draws only the body. Geometry is stored as fractions of the messages area, so a window keeps its
	/// place when the application window or the side panel changes the area's size. The window can be collapsed in
	/// place (header only) or hidden into the chat's tray; closing destroys it, which is why a payload has to allow
	/// it through <see cref="CanClose"/>.
	/// </remarks>
	public class AdditionalChatDataOverlayItem : AdditionalChatDataItemBase
	{
		/// <summary>
		/// Gets or sets the title the window header, the collapsed header and the tray row show.
		/// </summary>
		public string? Title
		{
			get;
			set
			{
				if (SetProperty(ref field, value))
					RaisePropertyChanged(nameof(HasTitle));
			}
		}

		/// <summary>
		/// Gets a value indicating whether the item has a title worth drawing.
		/// </summary>
		[BsonIgnore]
		public bool HasTitle => !string.IsNullOrWhiteSpace(Title);

		/// <summary>
		/// Gets or sets the icon drawn before the title in the window header and in the tray row.
		/// </summary>
		public VisualIconKind Icon
		{
			get;
			set
			{
				if (SetProperty(ref field, value))
					RaisePropertyChanged(nameof(HasIcon));
			}
		} = VisualIconKind.None;

		/// <summary>
		/// Gets a value indicating whether the window has an icon of its own.
		/// </summary>
		[BsonIgnore]
		public bool HasIcon => !Icon.IsNone;

		/// <summary>
		/// Gets or sets a value indicating whether the user may destroy the window, together with its payload.
		/// </summary>
		public bool CanClose
		{
			get;
			set => SetProperty(ref field, value);
		}

		/// <summary>
		/// Gets or sets the presentation state of the window.
		/// </summary>
		public AdditionalChatDataOverlayState State
		{
			get;
			set
			{
				if (SetProperty(ref field, value))
					RaiseShownChanged();
			}
		}

		/// <summary>
		/// Gets or sets the offset of the window from the left edge of the messages area, as a fraction of its width.
		/// </summary>
		public double? Left
		{
			get;
			set => SetProperty(ref field, value);
		}

		/// <summary>
		/// Gets or sets the offset of the window from the top edge of the messages area, as a fraction of its height.
		/// </summary>
		public double? Top
		{
			get;
			set => SetProperty(ref field, value);
		}

		/// <summary>
		/// Gets or sets the width of the window, as a fraction of the messages area; null lets the content decide.
		/// </summary>
		/// <remarks>
		/// One field for two owners: the script seeds it, and the end of a resize seals the size the user dragged as
		/// explicit. The "fit the content" button clears it back to null, which is why null cannot mean "not decided
		/// yet" - a window that was never sized simply hugs its content.
		/// </remarks>
		public double? Width
		{
			get;
			set
			{
				if (SetProperty(ref field, value))
					RaiseSizeChanged();
			}
		}

		/// <summary>
		/// Gets or sets the height of the window, as a fraction of the messages area; null lets the content decide.
		/// </summary>
		public double? Height
		{
			get;
			set
			{
				if (SetProperty(ref field, value))
					RaiseSizeChanged();
			}
		}

		/// <summary>
		/// Gets a value indicating whether the window has a size of its own instead of hugging its content.
		/// </summary>
		[BsonIgnore]
		public bool HasExplicitSize => Width is not null || Height is not null;

		/// <summary>
		/// Gets or sets the stacking order the user gave the window by using it; higher is closer to the front.
		/// </summary>
		/// <remarks>
		/// The initial order comes from the collection itself. Persisted because raising a window is a gesture like
		/// moving it, and a window that covered another one should keep doing so after a reload.
		/// </remarks>
		public int Z
		{
			get;
			set => SetProperty(ref field, value);
		}

		/// <summary>
		/// Gets a value indicating whether the window itself is on screen.
		/// </summary>
		[BsonIgnore]
		public bool IsWindowShown => IsEffectivelyVisible && State != AdditionalChatDataOverlayState.Hidden;

		/// <summary>
		/// Gets a value indicating whether the item is listed in the chat's tray.
		/// </summary>
		[BsonIgnore]
		public bool IsInTray => IsEffectivelyVisible && State == AdditionalChatDataOverlayState.Hidden;

		/// <summary>
		/// Gets a value indicating whether the body is drawn.
		/// </summary>
		/// <remarks>
		/// Only the state is consulted: the body is a child of the window, so the payload's own visibility has
		/// already removed the whole thing from the tree.
		/// </remarks>
		[BsonIgnore]
		public bool IsContentShown => State == AdditionalChatDataOverlayState.Normal;

		/// <summary>
		/// Gets the width fraction the window is laid out with. A collapsed window is as wide as its header.
		/// </summary>
		[BsonIgnore]
		public double? EffectiveWidth => IsContentShown ? Width : null;

		/// <summary>
		/// Gets the height fraction the window is laid out with. A collapsed window is as tall as its header.
		/// </summary>
		[BsonIgnore]
		public double? EffectiveHeight => IsContentShown ? Height : null;

		/// <summary>
		/// Takes over what a script's update must not reset: the state the user chose, the place and the size the
		/// user gave the window, the stacking order and the title, when the update did not bring one.
		/// </summary>
		/// <param name="previous">The envelope this item replaces under one replacement key.</param>
		public override void InheritFrom(AdditionalChatDataItemBase previous)
		{
			base.InheritFrom(previous);

			if (previous is not AdditionalChatDataOverlayItem overlay)
				return;

			if (string.IsNullOrWhiteSpace(Title))
				Title = overlay.Title;

			if (Icon.IsNone)
				Icon = overlay.Icon;

			State = overlay.State;
			Left = overlay.Left;
			Top = overlay.Top;
			Width = overlay.Width;
			Height = overlay.Height;
			Z = Math.Max(Z, overlay.Z);
		}

		/// <summary>
		/// The payload's visibility decides whether the window and the tray row are shown at all.
		/// </summary>
		protected override void OnContentPropertyChanged(object? sender, PropertyChangedEventArgs e)
		{
			base.OnContentPropertyChanged(sender, e);

			if (e.PropertyName is null or nameof(IsVisible))
				RaiseShownChanged();
		}

		private void RaiseShownChanged()
		{
			RaisePropertyChanged(nameof(IsWindowShown));
			RaisePropertyChanged(nameof(IsInTray));
			RaisePropertyChanged(nameof(IsContentShown));
			RaisePropertyChanged(nameof(EffectiveWidth));
			RaisePropertyChanged(nameof(EffectiveHeight));
		}

		/// <summary>
		/// A size change has to reach the panel: the layout reads the effective fractions, not the stored ones.
		/// </summary>
		private void RaiseSizeChanged()
		{
			RaisePropertyChanged(nameof(HasExplicitSize));
			RaisePropertyChanged(nameof(EffectiveWidth));
			RaisePropertyChanged(nameof(EffectiveHeight));
		}
	}
}
