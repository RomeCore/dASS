namespace LLMDesktopAssistant.Controls.Icons
{
	/// <summary>
	/// The result of resolving a <see cref="VisualIconKind"/>: a status and, when
	/// something has to be drawn, the SVG path data to draw.
	/// </summary>
	/// <param name="Status">The resolution verdict.</param>
	/// <param name="SvgPath">
	/// The SVG path data to render. <see langword="null"/> when <see cref="Status"/> is
	/// <see cref="VisualIconStatus.None"/>; for the error statuses it holds the fallback glyph.
	/// </param>
	public readonly record struct VisualIconData(VisualIconStatus Status, string? SvgPath)
	{
		/// <summary>Whether the icon resolved successfully.</summary>
		public bool IsOk => Status == VisualIconStatus.Ok;

		/// <summary>Whether the icon failed to resolve and should be rendered as an error.</summary>
		public bool IsError => Status is VisualIconStatus.InvalidPack or VisualIconStatus.InvalidData;
	}
}
