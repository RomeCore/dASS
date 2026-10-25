using System.Text;

namespace LLMDesktopAssistant.Utils.Files;

/// <summary>
/// The outcome of applying one plain-text patch to a file's content.
/// </summary>
/// <param name="Content">The patched content, or <see langword="null"/> when nothing matched.</param>
/// <param name="Occurrences">How many places the match was found in.</param>
public readonly record struct PatchApplication(string? Content, int Occurrences)
{
	/// <summary>
	/// Whether the match was found at least once.
	/// </summary>
	public bool Found => Occurrences > 0;
}

/// <summary>
/// Applies plain-text replacement patches with lenient matching and file-relative indentation.
/// </summary>
/// <remarks>
/// <para>
/// Matching is lenient: leading and trailing whitespace and common indentation of the patch are
/// ignored, so a patch never has to reproduce the file's spacing. Text that surrounds the match is
/// preserved.
/// </para>
/// <para>
/// Writing is deliberately not verbatim, and - unlike the earlier implementation - it does not
/// depend on how the match happened to be written either. The first replacement line lands on
/// whatever preceded the match in the file; every following line is shifted by one and the same
/// number of columns, so the replacement keeps exactly the shape it was written in. The shift comes
/// from the smallest indentation among the replacement's lines <em>after</em> the first, which is
/// skipped on purpose: models trained on verbatim-only editors write that line without indentation
/// and indent the continuation lines the way the file does.
/// </para>
/// <para>
/// An empty replacement deletes. Lines the match covered completely are removed together with their
/// line breaks, so no indentation residue is left where the block used to be.
/// </para>
/// </remarks>
public static class FilePatchedReplacer
{
	/// <summary>
	/// Replaces every occurrence of <paramref name="match"/> with <paramref name="replace"/> in
	/// <paramref name="content"/>, re-basing the replacement onto the file's own indentation.
	/// </summary>
	/// <param name="content">The file content. Line endings are normalized internally.</param>
	/// <param name="match">The literal text to search for (can span multiple lines).</param>
	/// <param name="replace">The replacement text. An empty string deletes the match.</param>
	/// <param name="tabSize">The number of columns a tab character occupies when measuring indentation.</param>
	/// <param name="ignoreCase">Whether matching is case-insensitive.</param>
	/// <returns>The patched content and the number of occurrences, or no content when nothing matched.</returns>
	public static PatchApplication Replace(string content, string match, string replace, int tabSize = 4, bool ignoreCase = false)
	{
		var comparison = ignoreCase ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
		var matchLines = SplitLines(match).Select(l => l.Trim()).ToArray();
		if (matchLines.Length == 0 || matchLines.All(l => l.Length == 0))
			return new PatchApplication(null, 0);

		var replaceLines = SplitLines(replace);
		var fileLines = Normalize(content).Split('\n').ToList();

		if (replaceLines.Count == 0)
		{
			return matchLines.Length == 1
				? ApplyInlineDelete(fileLines, matchLines[0], comparison)
				: ApplyBlockDelete(fileLines, matchLines, comparison);
		}

		if (matchLines.Length == 1 && replaceLines.Count == 1)
			return ApplyInline(fileLines, matchLines[0], replaceLines[0], comparison);

		return ApplyBlock(fileLines, matchLines, replaceLines, tabSize, comparison);
	}

	private static PatchApplication ApplyInline(
		List<string> fileLines, string match, string replacement, StringComparison comparison)
	{
		var occurrences = 0;
		for (var i = 0; i < fileLines.Count; i++)
		{
			var line = fileLines[i];
			var updated = RewriteLine(line, match, replacement, comparison, ref occurrences);
			if (updated != line)
				fileLines[i] = updated;
		}

		return Result(fileLines, occurrences);
	}

	private static PatchApplication ApplyInlineDelete(
		List<string> fileLines, string match, StringComparison comparison)
	{
		var occurrences = 0;
		for (var i = 0; i < fileLines.Count; i++)
		{
			var line = fileLines[i];
			var updated = RewriteLine(line, match, "", comparison, ref occurrences);
			if (updated == line)
				continue;

			if (string.IsNullOrWhiteSpace(updated))
			{
				// A single-line match deletes content, not structure: the line stays, emptied, so the
				// file keeps its shape and no indentation residue is left behind.
				fileLines[i] = "";
				continue;
			}

			fileLines[i] = updated;
		}

		return Result(fileLines, occurrences);
	}

	private static PatchApplication ApplyBlock(
		List<string> fileLines, string[] matchLines, List<string> replaceLines,
		int tabSize, StringComparison comparison)
	{
		if (fileLines.Count < matchLines.Length)
			return new PatchApplication(null, 0);

		var occurrences = 0;

		for (var i = 0; i <= fileLines.Count - matchLines.Length; i++)
		{
			if (!TryMatchBlock(fileLines, i, matchLines, comparison, out var prefixes, out var suffixes))
				continue;

			occurrences++;

			var anchor = fileLines[i];
			var shift = GetShift(fileLines, i, matchLines.Length, replaceLines, tabSize);
			var replacement = BuildBlock(replaceLines, shift, anchor, tabSize, prefixes, suffixes);

			fileLines.RemoveRange(i, matchLines.Length);
			fileLines.InsertRange(i, replacement);

			// Continue after the replaced block; the loop's i++ moves past it.
			i += replacement.Count - 1;
		}

		return Result(fileLines, occurrences);
	}

	private static PatchApplication ApplyBlockDelete(
		List<string> fileLines, string[] matchLines, StringComparison comparison)
	{
		if (fileLines.Count < matchLines.Length)
			return new PatchApplication(null, 0);

		var occurrences = 0;

		for (var i = 0; i <= fileLines.Count - matchLines.Length; i++)
		{
			if (!TryMatchBlock(fileLines, i, matchLines, comparison, out var prefixes, out var suffixes))
				continue;

			occurrences++;

			List<string> replacement;
			if (prefixes.All(p => !HasNonWhitespace(p)) && suffixes.All(s => !HasNonWhitespace(s)))
			{
				// The match covered whole lines: they go away with their line breaks.
				replacement = [];
			}
			else
			{
				// The match is a fragment: keep the text before it and after it, joined.
				replacement = [prefixes[0] + suffixes[^1]];
			}

			fileLines.RemoveRange(i, matchLines.Length);
			if (replacement.Count > 0)
				fileLines.InsertRange(i, replacement);

			i += replacement.Count - 1;
		}

		return Result(fileLines, occurrences);
	}

	/// <summary>
	/// Reproduces the replacement block at one matched position: the first line lands on the file's
	/// own prefix, the rest follow a single shift derived from the patch's base indentation.
	/// </summary>
	private static List<string> BuildBlock(
		List<string> replaceLines, int shift, string anchor, int tabSize, string[] prefixes, string[] suffixes)
	{
		var block = new List<string>(replaceLines.Count);

		for (var j = 0; j < replaceLines.Count; j++)
		{
			var line = replaceLines[j];
			string rebuilt;

			if (string.IsNullOrWhiteSpace(line))
			{
				rebuilt = "";
			}
			else if (j == 0)
			{
				var prefix = prefixes[0];
				// The file already carries this line's indentation; a second one would be doubled.
				rebuilt = prefix + (IsIndentation(prefix) ? line.TrimStart() : line);
			}
			else
			{
				rebuilt = MakeIndent(GetIndentation(line, tabSize) + shift, anchor, tabSize) + line.TrimStart();
			}

			// Text outside the match (e.g. a trailing comment) is never part of the match and must not
			// be deleted. The last matched line's tail closes the block; a middle line's tail rides on
			// the replacement line at the same offset, which is where the author saw it.
			if (j == replaceLines.Count - 1)
				rebuilt += suffixes[^1];
			else if (j < suffixes.Length - 1 && HasNonWhitespace(suffixes[j]))
				rebuilt += suffixes[j];

			block.Add(rebuilt);
		}

		return block;
	}

	private static bool TryMatchBlock(
		List<string> fileLines, int start, string[] matchLines, StringComparison comparison,
		out string[] prefixes, out string[] suffixes)
	{
		prefixes = new string[matchLines.Length];
		suffixes = new string[matchLines.Length];

		for (var j = 0; j < matchLines.Length; j++)
		{
			var line = fileLines[start + j];

			if (matchLines[j].Length == 0)
			{
				// A blank patch line stands for a blank file line - it must not swallow real content.
				if (!string.IsNullOrWhiteSpace(line))
					return false;

				prefixes[j] = line;
				suffixes[j] = "";
				continue;
			}

			var index = line.IndexOf(matchLines[j], comparison);
			if (index < 0)
				return false;

			var prefix = line[..index];
			var suffix = line[(index + matchLines[j].Length)..];

			// Non-whitespace on both sides of the match would make the replacement position ambiguous.
			if (HasNonWhitespace(prefix) && HasNonWhitespace(suffix))
				return false;

			prefixes[j] = prefix;
			suffixes[j] = suffix;
		}

		return true;
	}

	private static string RewriteLine(
		string line, string match, string replacement, StringComparison comparison, ref int occurrences)
	{
		var result = new StringBuilder();
		var position = 0;

		while (true)
		{
			var index = line.IndexOf(match, position, comparison);
			if (index < 0)
				break;

			occurrences++;
			result.Append(line, position, index - position);

			var prefix = line[position..index];
			// If the text before the match is pure indentation, the file's indentation wins and the
			// replacement's own leading whitespace is dropped to avoid doubling it.
			result.Append(IsIndentation(prefix) ? replacement.TrimStart() : replacement);

			position = index + match.Length;
		}

		if (position == 0)
			return line;

		result.Append(line, position, line.Length - position);
		return result.ToString();
	}

	private static PatchApplication Result(List<string> fileLines, int occurrences) =>
		occurrences == 0
			? new PatchApplication(null, 0)
			: new PatchApplication(string.Join('\n', fileLines), occurrences);

	private static List<string> SplitLines(string text)
	{
		var lines = Normalize(text).Split('\n').ToList();
		if (lines.Count > 0 && string.IsNullOrEmpty(lines[^1]))
			lines.RemoveAt(lines.Count - 1);
		return lines;
	}

	private static string Normalize(string text)
		=> text.Replace("\r\n", "\n").Replace("\r", "\n");

	private static bool IsIndentation(string text)
		=> text.Length > 0 && text.All(c => c is ' ' or '\t');

	private static bool HasNonWhitespace(string text)
		=> text.Any(c => c is not ' ' and not '\t');

	/// <summary>
	/// The column offset applied to every replacement line after the first.
	/// </summary>
	/// <remarks>
	/// The second line is the reference, never the first: the first line is the one models write
	/// bare (verbatim-trained habits), while the continuation lines carry the indentation the author
	/// actually expects. Lining the replacement's second line up with the second matched line of the
	/// file therefore places the whole block where the author saw it, without per-line bookkeeping.
	/// A single-line match has no second line to align with, so the replacement's own continuation
	/// lines supply the base instead.
	/// </remarks>
	private static int GetShift(
		List<string> fileLines, int start, int matchCount, List<string> replaceLines, int tabSize)
	{
		for (var j = 1; j < replaceLines.Count && j < matchCount; j++)
		{
			if (string.IsNullOrWhiteSpace(replaceLines[j]) || string.IsNullOrWhiteSpace(fileLines[start + j]))
				continue;

			return GetIndentation(fileLines[start + j], tabSize) - GetIndentation(replaceLines[j], tabSize);
		}

		// A single-line match has no second line to align with, so the replacement supplies the base
		// from its own continuation lines.
		return GetIndentation(fileLines[start], tabSize) - GetBaseIndentation(replaceLines, tabSize);
	}

	/// <summary>
	/// The smallest indentation among the replacement's lines after the first, in columns. The first
	/// line is skipped on purpose: verbatim-trained models write it without indentation, and taking
	/// it into account would drag the whole block to column zero.
	/// </summary>
	private static int GetBaseIndentation(List<string> lines, int tabSize)
	{
		var min = int.MaxValue;
		for (var i = 1; i < lines.Count; i++)
		{
			if (string.IsNullOrWhiteSpace(lines[i]))
				continue;

			min = Math.Min(min, GetIndentation(lines[i], tabSize));
		}

		return min == int.MaxValue ? 0 : min;
	}

	private static int GetIndentation(string line, int tabSize)
	{
		var columns = 0;
		foreach (var c in line)
		{
			if (c == ' ')
				columns++;
			else if (c == '\t')
				columns += tabSize - (columns % tabSize);
			else
				break;
		}

		return columns;
	}

	private static string MakeIndent(int columns, string fileLine, int tabSize)
	{
		if (columns <= 0)
			return "";
		if (UsesTabs(fileLine))
			return new string('\t', columns / tabSize) + new string(' ', columns % tabSize);
		return new string(' ', columns);
	}

	private static bool UsesTabs(string line)
	{
		foreach (var c in line)
		{
			if (c == '\t')
				return true;
			if (c != ' ')
				return false;
		}

		return false;
	}
}
