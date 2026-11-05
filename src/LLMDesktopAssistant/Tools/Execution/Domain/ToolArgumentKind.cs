namespace LLMDesktopAssistant.Tools.Execution.Domain
{
	/// <summary>
	/// The shape of one tool argument, derived from its JSON Schema type and the dASS metadata attached
	/// to it. It drives the slash-command autocomplete format and, through
	/// <see cref="Specifiers.SpecifierTargetKind"/>, the dialect a specifier literal is compiled in.
	/// </summary>
	public enum ToolArgumentKind
	{
		/// <summary>
		/// The schema declares no usable type; the argument is still a value, but nothing is known about it.
		/// </summary>
		Unspecified,

		/// <summary>
		/// A JSON string that is not a known special shape.
		/// </summary>
		String,

		/// <summary>
		/// A JSON boolean.
		/// </summary>
		Bool,

		/// <summary>
		/// A JSON integer.
		/// </summary>
		Integer,

		/// <summary>
		/// A JSON number without an integer constraint.
		/// </summary>
		Number,

		/// <summary>
		/// A filesystem path to a file. A specifier literal that targets it is compiled as a path pattern.
		/// </summary>
		FilePath,

		/// <summary>
		/// A filesystem path to a directory. A specifier literal that targets it is compiled as a path pattern.
		/// </summary>
		DirectoryPath,

		/// <summary>
		/// A filesystem path that may point to a file or a directory - what a tool like 'fs-read_entry'
		/// takes. A specifier literal that targets it is compiled as a path pattern.
		/// </summary>
		FSPath,

		/// <summary>
		/// A JSON array.
		/// </summary>
		Array,

		/// <summary>
		/// A JSON object.
		/// </summary>
		Object,
	}
}
