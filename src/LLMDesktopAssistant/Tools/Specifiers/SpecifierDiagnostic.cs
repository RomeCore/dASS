using LLMDesktopAssistant.Tools.Execution.Domain;
using LLMDesktopAssistant.Utils.Patterns;

namespace LLMDesktopAssistant.Tools.Specifiers
{
	/// <summary>
	/// What a specifier rule got wrong.
	/// </summary>
	public enum SpecifierDiagnosticKind
	{
		/// <summary>
		/// The rule is empty.
		/// </summary>
		Empty,

		/// <summary>
		/// The rule text cannot be parsed as a specifier expression.
		/// </summary>
		Syntax,

		/// <summary>
		/// A literal cannot be compiled in the dialect of the argument it targets.
		/// </summary>
		Pattern,

		/// <summary>
		/// A literal targets the main argument of a tool that has none, so it can never match.
		/// </summary>
		NoTarget,
	}

	/// <summary>
	/// One problem found in a specifier rule, with the place in the rule text to point at.
	/// </summary>
	/// <param name="Kind">The kind of problem.</param>
	/// <param name="Position">The zero-based index of the problem in the rule text.</param>
	/// <param name="Length">The length of the region to underline, or 0 when the problem is a point.</param>
	/// <param name="Error">The glob error code, for a <see cref="SpecifierDiagnosticKind.Pattern"/> diagnostic.</param>
	/// <param name="Detail">The technical message, for the log and for the generic fallback.</param>
	public sealed record SpecifierDiagnostic(SpecifierDiagnosticKind Kind, int Position, int Length,
		GlobError? Error = null, string? Detail = null);
}
