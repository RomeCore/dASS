using System.Text;

namespace LLMDesktopAssistant.Utils.Patterns
{
	/// <summary>
	/// Brace expansion, and rendering a unit list back to text.
	/// </summary>
	public static class GlobExpander
	{
		/// <summary>
		/// The maximum number of alternatives a single pattern may expand to.
		/// </summary>
		public const int MaxAlternatives = 64;

		/// <summary>
		/// Rewrites a brace group that has no top-level comma into literal braces around its content,
		/// which is what the dialect promises: only a comma makes a brace a choice.
		/// </summary>
		/// <param name="units">The units to normalize.</param>
		/// <returns>The units with single-alternative braces turned into literals.</returns>
		public static ImmutableList<GlobUnit> NormalizeBraces(IReadOnlyList<GlobUnit> units)
		{
			var builder = ImmutableList.CreateBuilder<GlobUnit>();

			foreach (var unit in units)
			{
				if (unit is not GlobBrace brace)
				{
					builder.Add(unit);
					continue;
				}

				if (brace.Alternatives.Count < 2)
				{
					builder.Add(new GlobLiteral(brace.Position, '{'));
					foreach (var inner in NormalizeBraces(brace.Alternatives[0]))
						builder.Add(inner);
					builder.Add(new GlobLiteral(brace.Position, '}'));
					continue;
				}

				var alternatives = ImmutableList.CreateBuilder<IReadOnlyList<GlobUnit>>();
				foreach (var alternative in brace.Alternatives)
					alternatives.Add(NormalizeBraces(alternative));

				builder.Add(brace with { Alternatives = alternatives.ToImmutable() });
			}

			return builder.ToImmutable();
		}

		/// <summary>
		/// Replaces every brace group with each of its branches, producing a flat list of
		/// alternative unit sequences.
		/// </summary>
		/// <param name="units">The parsed units to expand.</param>
		/// <param name="pattern">The source pattern, used in the error message.</param>
		/// <returns>One unit list per alternative, in source order.</returns>
		public static List<List<GlobUnit>> Expand(IReadOnlyList<GlobUnit> units, string pattern)
		{
			var results = new List<List<GlobUnit>> { new() };

			foreach (var unit in units)
			{
				if (unit is not GlobBrace brace)
				{
					foreach (var result in results)
						result.Add(unit);
					continue;
				}

				var branches = new List<List<GlobUnit>>();
				foreach (var alternative in brace.Alternatives)
					branches.AddRange(Expand(alternative, pattern));

				var crossed = new List<List<GlobUnit>>(results.Count * branches.Count);
				foreach (var result in results)
				{
					foreach (var branch in branches)
					{
						var combined = new List<GlobUnit>(result.Count + branch.Count);
						combined.AddRange(result);
						combined.AddRange(branch);
						crossed.Add(combined);
					}
				}

				results = crossed;

				if (results.Count > MaxAlternatives)
					throw new GlobException(GlobError.TooManyAlternatives, pattern, brace.Position,
						$"Glob pattern expands to more than {MaxAlternatives} alternatives.");
			}

			return results;
		}

		/// <summary>
		/// Renders a unit list back to text, for diagnostics and error messages.
		/// </summary>
		/// <param name="units">The units to render.</param>
		/// <returns>The pattern text the units describe.</returns>
		public static string Render(IReadOnlyList<GlobUnit> units)
		{
			var builder = new StringBuilder();

			foreach (var unit in units)
			{
				switch (unit)
				{
					case GlobLiteral literal:
						builder.Append(literal.Value);
						break;
					case GlobAnyChars:
						builder.Append('*');
						break;
					case GlobAnyChar:
						builder.Append('?');
						break;
					case GlobDoubleStar:
						builder.Append("**");
						break;
					case GlobSeparator:
						builder.Append('/');
						break;
					case GlobClass cls:
						builder.Append('[').Append(cls.Body).Append(']');
						break;
					case GlobBrace brace:
						builder.Append('{');
						for (var i = 0; i < brace.Alternatives.Count; i++)
						{
							if (i > 0)
								builder.Append(',');
							builder.Append(Render(brace.Alternatives[i]));
						}
						builder.Append('}');
						break;
				}
			}

			return builder.ToString();
		}
	}
}
