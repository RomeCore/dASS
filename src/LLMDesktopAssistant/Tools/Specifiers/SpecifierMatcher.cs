using LLMDesktopAssistant.Utils.Files;

namespace LLMDesktopAssistant.Tools.Specifiers
{
	/// <summary>
	/// Matches <see cref="Specifier"/> instances against the targets of a tool call: the parts of the
	/// main argument and the values of the named parameters.
	/// </summary>
	public static class SpecifierMatcher
	{
		/// <summary>
		/// Matches the specifier against the provided targets. Each literal part is compiled in the
		/// dialect of the target it covers - flat text for a command line, a filesystem pattern for a
		/// path - on its first use.
		/// </summary>
		/// <param name="specifier">The specifier to match. Cannot be <see langword="null"/>.</param>
		/// <param name="targets">The values the literals are matched against. Cannot be <see langword="null"/>.</param>
		/// <returns>
		/// <see cref="SpecifierMatchResult.FullMatch"/> when every target of the matched groups
		/// (the main argument parts and the values of the referenced parameters) is covered,
		/// <see cref="SpecifierMatchResult.PartialMatch"/> when only some targets are covered,
		/// and <see cref="SpecifierMatchResult.NoMatch"/> when no group of the specifier matches anything.
		/// </returns>
		public static SpecifierMatchResult Match(Specifier specifier, IEnumerable<SpecifierTarget> targets)
		{
			ArgumentNullException.ThrowIfNull(specifier);
			ArgumentNullException.ThrowIfNull(targets);

			var parts = new Dictionary<string, SpecifierTarget>(StringComparer.Ordinal);
			var parameters = new Dictionary<string, SpecifierTarget>(StringComparer.Ordinal);

			foreach (var target in targets)
			{
				if (target.Name is { } name)
				{
					// The first value of a duplicated parameter is the one the tool used.
					parameters.TryAdd(name, target);
				}
				else
				{
					parts.TryAdd(target.Value, target);
				}
			}

			HashSet<string>? matchedTargets = null;
			HashSet<string>? covered = null;

			foreach (var group in specifier.Parts)
			{
				var (matched, groupTargets, groupCovered) = MatchGroup(group, parts, parameters);
				if (!matched)
					continue;

				matchedTargets ??= new HashSet<string>(StringComparer.Ordinal);
				covered ??= new HashSet<string>(StringComparer.Ordinal);
				matchedTargets.UnionWith(groupTargets);
				covered.UnionWith(groupCovered);
			}

			if (matchedTargets == null)
				return SpecifierMatchResult.NoMatch;

			return covered!.IsSupersetOf(matchedTargets)
				? SpecifierMatchResult.FullMatch
				: SpecifierMatchResult.PartialMatch;
		}

		/// <summary>
		/// Matches the specifier against flat-text targets: the parts of the main argument and the
		/// named parameters, all compiled as text patterns.
		/// </summary>
		/// <param name="specifier">The specifier to match. Cannot be <see langword="null"/>.</param>
		/// <param name="mainArgumentParts">The parts of the main argument (for example, individual commands of a compound shell command).</param>
		/// <param name="parameters">The named tool parameters (parameter name → string value).</param>
		/// <returns>The match of the specifier against the targets.</returns>
		public static SpecifierMatchResult Match(Specifier specifier,
			IEnumerable<string> mainArgumentParts, IEnumerable<KeyValuePair<string, string>> parameters)
		{
			ArgumentNullException.ThrowIfNull(specifier);
			ArgumentNullException.ThrowIfNull(mainArgumentParts);
			ArgumentNullException.ThrowIfNull(parameters);

			return Match(specifier, mainArgumentParts.Select(SpecifierTarget.Text)
				.Concat(parameters.Select(p => SpecifierTarget.Parameter(p.Key, p.Value))));
		}

		/// <summary>
		/// Matches a single group (an AND-group or a standalone literal) and returns whether it matched,
		/// its targets and its covered targets.
		/// </summary>
		private static (bool Matched, IEnumerable<string> Targets, IEnumerable<string> Covered) MatchGroup(
			SpecifierPart group, Dictionary<string, SpecifierTarget> parts,
			Dictionary<string, SpecifierTarget> parameters)
		{
			if (group is SpecifierAndPart andPart)
			{
				var targets = new HashSet<string>(StringComparer.Ordinal);
				var covered = new HashSet<string>(StringComparer.Ordinal);

				foreach (var literal1 in andPart.Parts)
				{
					var (literalTargets, literalCovered) = MatchLiteral(literal1, parts, parameters);

					// AND semantics: every literal must cover ALL of its targets, because
					// all literals describe the same object (x == 1 && x == 2 is impossible).
					if (literalTargets.Count == 0 || !literalCovered.IsSupersetOf(literalTargets))
						return (false, targets, covered);

					targets.UnionWith(literalTargets);
					covered.UnionWith(literalCovered);
				}

				return (true, targets, covered);
			}

			if (group is SpecifierLiteralPart literal2)
			{
				var (targets, covered) = MatchLiteral(literal2, parts, parameters);
				return (covered.Count > 0, targets, covered);
			}

			return (false, [], []);
		}

		/// <summary>
		/// Matches a single literal against its targets: the value of the referenced parameter
		/// for <see cref="SpecifierParameterPart"/>, or all main argument parts otherwise.
		/// </summary>
		private static (HashSet<string> Targets, HashSet<string> Covered) MatchLiteral(
			SpecifierLiteralPart literal, Dictionary<string, SpecifierTarget> parts,
			Dictionary<string, SpecifierTarget> parameters)
		{
			if (literal is SpecifierParameterPart parameterPart)
			{
				var targets = new HashSet<string>(StringComparer.Ordinal);
				var covered = new HashSet<string>(StringComparer.Ordinal);

				if (parameters.TryGetValue(parameterPart.Name, out var target))
				{
					targets.Add(target.Value);
					if (Matches(parameterPart, target))
						covered.Add(target.Value);
				}

				return (targets, covered);
			}

			var coveredParts = new HashSet<string>(StringComparer.Ordinal);
			foreach (var (value, target) in parts)
			{
				if (Matches(literal, target))
					coveredParts.Add(value);
			}

			return (new HashSet<string>(parts.Keys, StringComparer.Ordinal), coveredParts);
		}

		/// <summary>
		/// Matches one literal against one target. An empty pattern matches only an empty text:
		/// the glob compiler refuses an empty pattern, and a literal that parsed to nothing
		/// must stay inert instead of matching everything.
		/// </summary>
		private static bool Matches(SpecifierLiteralPart literal, SpecifierTarget target)
		{
			if (literal.Value.Length == 0)
				return target.Value.Length == 0;

			if (target.Kind == SpecifierTargetKind.Text)
				return literal.Glob.Matches(target.Value);

			if (target.BaseDirectory is not { } baseDirectory)
				throw new ArgumentException(
					$"A path target requires a base directory ('{target.Name ?? "main argument"}').",
					nameof(target));

			return GlobPathMatcher.Matches(literal.PathGlob, target.Value, baseDirectory);
		}
	}
}
