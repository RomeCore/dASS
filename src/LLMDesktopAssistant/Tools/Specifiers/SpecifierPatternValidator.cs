using System.Collections.Frozen;
using LLMDesktopAssistant.Tools.Execution.Domain;
using LLMDesktopAssistant.Utils.Patterns;
using RCParsing;

namespace LLMDesktopAssistant.Tools.Specifiers
{
	/// <summary>
	/// Checks a specifier rule before it is saved: the expression grammar first, then every literal against
	/// the dialect of the argument it targets. This is the UI's half of the safety net - at runtime a broken
	/// rule is inert and logged, which is exactly why the error has to be visible while it is being written.
	/// </summary>
	public static class SpecifierPatternValidator
	{
		/// <summary>
		/// Validates a rule against the arguments of one tool.
		/// </summary>
		/// <param name="pattern">The rule text as written by the user.</param>
		/// <param name="arguments">The arguments of the tool the rule belongs to.</param>
		/// <returns>The problems found, in the order they appear in the text; empty when the rule is fine.</returns>
		public static IReadOnlyList<SpecifierDiagnostic> Validate(string pattern, ToolArgumentsDescriptor arguments)
		{
			ArgumentNullException.ThrowIfNull(arguments);

			if (string.IsNullOrWhiteSpace(pattern))
				return [new SpecifierDiagnostic(SpecifierDiagnosticKind.Empty, 0, 0, Detail: "The rule is empty.")];

			if (!TryParse(pattern, arguments, out var specifier, out var syntaxError))
				return [syntaxError];

			var diagnostics = new List<SpecifierDiagnostic>();

			foreach (var literal in Literals(specifier))
			{
				var kind = TargetKind(literal, arguments);
				if (kind is null)
				{
					diagnostics.Add(new SpecifierDiagnostic(SpecifierDiagnosticKind.NoTarget,
						literal.Start, literal.Value.Length,
						Detail: "The tool has no main argument, so an unnamed literal can never match."));

					continue;
				}

				try
				{
					Compile(literal, kind.Value);
				}
				catch (GlobException exception)
				{
					diagnostics.Add(new SpecifierDiagnostic(SpecifierDiagnosticKind.Pattern,
						literal.Start, literal.Value.Length, exception.Error, exception.Message));
				}
			}

			return diagnostics;
		}

		/// <summary>
		/// Whether the rule has no problems.
		/// </summary>
		/// <param name="pattern">The rule text as written by the user.</param>
		/// <param name="arguments">The arguments of the tool the rule belongs to.</param>
		public static bool IsValid(string pattern, ToolArgumentsDescriptor arguments) =>
			Validate(pattern, arguments).Count == 0;

		/// <summary>
		/// Parses the rule with the tool's specifier names, turning a grammar failure into a diagnostic.
		/// </summary>
		private static bool TryParse(string pattern, ToolArgumentsDescriptor arguments,
			out Specifier specifier, out SpecifierDiagnostic syntaxError)
		{
			var names = arguments.SpecifierParameters
				.Select(argument => argument.Name)
				.ToFrozenSet(StringComparer.Ordinal);

			try
			{
				specifier = SpecifierParser.Parse(pattern, names);
				syntaxError = null!;
				return true;
			}
			catch (ParsingException exception)
			{
				specifier = null!;
				syntaxError = new SpecifierDiagnostic(SpecifierDiagnosticKind.Syntax,
					Math.Max(exception.LastPosition, 0), 0, Detail: exception.LastErrorMessage);
				return false;
			}
		}

		/// <summary>
		/// The dialect the literal is compiled in, or <see langword="null"/> when it targets the main
		/// argument of a tool that has no targetable one.
		/// </summary>
		private static SpecifierTargetKind? TargetKind(SpecifierLiteralPart literal,
			ToolArgumentsDescriptor arguments)
		{
			if (literal is SpecifierParameterPart parameter)
			{
				return arguments.TryGet(parameter.Name, out var target)
					? target.SpecifierKind
					: SpecifierTargetKind.Text;
			}

			return arguments.Main?.SpecifierKind;
		}

		/// <summary>
		/// Compiles the literal exactly as the matcher will, so the validator cannot drift from it.
		/// </summary>
		private static void Compile(SpecifierLiteralPart literal, SpecifierTargetKind kind)
		{
			if (literal.Value.Length == 0)
				return;

			if (kind is SpecifierTargetKind.Path)
				_ = literal.PathGlob;
			else
				_ = literal.Glob;
		}

		/// <summary>
		/// Every literal of the rule, in source order.
		/// </summary>
		private static IEnumerable<SpecifierLiteralPart> Literals(Specifier specifier)
		{
			foreach (var part in specifier.Parts)
			{
				switch (part)
				{
					case SpecifierAndPart and:
						foreach (var literal in and.Parts)
							yield return literal;
						break;

					case SpecifierLiteralPart literal:
						yield return literal;
						break;
				}
			}
		}
	}
}
