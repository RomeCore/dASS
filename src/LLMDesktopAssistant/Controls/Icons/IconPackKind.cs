namespace LLMDesktopAssistant.Controls.Icons
{
	/// <summary>
	/// Identifies the icon pack a <see cref="VisualIconKind"/> belongs to.
	/// </summary>
	/// <remarks>
	/// The set of packs is intentionally closed. <see cref="None"/> is the zero value,
	/// so <c>default(VisualIconKind)</c> naturally means "no icon".
	/// <see cref="Unknown"/> is used by the parser to preserve a pack name it does not
	/// recognise (the raw text is kept in <see cref="VisualIconKind.Data"/>), so the
	/// renderer can report it and serialization stays lossless.
	/// </remarks>
	public enum IconPackKind
	{
		/// <summary>No icon.</summary>
		None = 0,

		/// <summary>Icons from <c>Material.Icons</c> (<c>material:&lt;kind&gt;</c>).</summary>
		Material,

		/// <summary>Raw SVG path data (<c>path:&lt;path&gt;</c>).</summary>
		Path,

		/// <summary>An unrecognised pack name. Never produced except by the parser.</summary>
		Unknown,
	}
}
