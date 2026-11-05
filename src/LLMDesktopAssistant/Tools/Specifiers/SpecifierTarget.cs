namespace LLMDesktopAssistant.Tools.Specifiers
{
	/// <summary>
	/// One value a specifier literal is matched against: a part of the main argument, or the value of
	/// a named parameter. The kind decides the dialect the matching literal is compiled in, and a path
	/// target also carries the directory its relative pattern and value are resolved against.
	/// </summary>
	/// <param name="Value">The value the tool received.</param>
	/// <param name="Kind">The dialect the matching literal is compiled in.</param>
	/// <param name="Name">The parameter name, or <see langword="null"/> for a part of the main argument.</param>
	/// <param name="BaseDirectory">
	/// The absolute directory a path value and a relative pattern are resolved against. Required for
	/// <see cref="SpecifierTargetKind.Path"/>, because the process directory is never what the tool means.
	/// </param>
	public readonly record struct SpecifierTarget(string Value,
		SpecifierTargetKind Kind = SpecifierTargetKind.Text, string? Name = null, string? BaseDirectory = null)
	{
		/// <summary>
		/// A part of the main argument, matched as flat text.
		/// </summary>
		/// <param name="value">The part of the main argument.</param>
		public static SpecifierTarget Text(string value)
		{
			return new SpecifierTarget(value);
		}

		/// <summary>
		/// A named argument, matched as flat text.
		/// </summary>
		/// <param name="name">The parameter name.</param>
		/// <param name="value">The parameter value.</param>
		public static SpecifierTarget Parameter(string name, string value)
		{
			return new SpecifierTarget(value, SpecifierTargetKind.Text, name);
		}

		/// <summary>
		/// A path - the main argument or a named one - matched as a filesystem pattern.
		/// </summary>
		/// <param name="value">The path the tool received: absolute, or relative to <paramref name="baseDirectory"/>.</param>
		/// <param name="baseDirectory">The absolute directory the path and a relative pattern are resolved against.</param>
		/// <param name="name">The parameter name, or <see langword="null"/> for the main argument.</param>
		public static SpecifierTarget Path(string value, string baseDirectory, string? name = null)
		{
			return new SpecifierTarget(value, SpecifierTargetKind.Path, name, baseDirectory);
		}
	}
}
