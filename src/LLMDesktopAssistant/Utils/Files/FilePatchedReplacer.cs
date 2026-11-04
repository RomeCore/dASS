using System.Text;
using Markdig.Helpers;

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
/// Writing is not verbatim, but it is one rule, and every line of a replacement moves by the same
/// shift. A patch copied out of the file - its second line at the depth of the line it matched -
/// lands verbatim; a patch that re-nests the matched block instead is relative to the match, so its
/// lines keep their own offset from the first matched line. The first replacement line always lands
/// on whatever preceded the match in the file.
/// </para>
/// <para>
/// A match that ends with a line break reaches the end of its last line. That is load-bearing twice
/// over. An empty replacement then deletes those lines together with their breaks, so no empty line
/// is left where they used to be; a replacement is spliced in where they were, and whatever followed
/// them continues on the line the replacement ended on, because the match took that break with it.
/// A match that stops inside a line only ever deletes its own text and leaves the line in place.
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
		var matchText = SplitLines(match);
		var matchLines = matchText.Select(l => l.Trim()).ToList();
		var matchIndents = matchText.Select(l => GetIndentation(l, tabSize)).ToList();
		var matchEndsLine = TakeTrailingBreak(matchLines);
		if (matchEndsLine)
			matchIndents.RemoveAt(matchIndents.Count - 1);
		if (matchLines.Count == 0 || matchLines.All(l => l.Length == 0))
			return new PatchApplication(null, 0);

		var fileLines = Normalize(content).Split('\n').ToList();

		// An empty replacement deletes. The raw text decides, not a line count: the empty string is
		// one blank line once split, so counting could never tell deletion from a blank replacement.
		if (replace.Length == 0)
		{
			if (matchEndsLine && matchLines.Count == 1)
				return ApplyLineDelete(fileLines, matchLines[0], comparison);

			return matchLines.Count == 1
				? ApplyInlineDelete(fileLines, matchLines[0], comparison)
				: ApplyBlockDelete(fileLines, [.. matchLines], comparison);
		}

		var replaceLines = SplitLines(replace);
		if (!matchEndsLine && matchLines.Count == 1 && replaceLines.Count == 1)
			return ApplyInline(fileLines, matchLines[0], replaceLines[0], comparison);

		return ApplyBlock(fileLines, [.. matchLines], matchIndents, replaceLines, matchEndsLine, tabSize, comparison);
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

	/// <summary>
	/// Deletes every line the match covers entirely, together with its break. Used for a patch whose
	/// match ends with a line break: it reaches the end of its line, so it means the line, not the
	/// word, and leaving an empty line behind would cost the caller another turn to clean it up.
	/// </summary>
	private static PatchApplication ApplyLineDelete(
		List<string> fileLines, string match, StringComparison comparison)
	{
		if (!fileLines.Any(l => string.Equals(l.Trim(), match, comparison)))
			// The break-terminated match turned out to sit inside a line: delete the text instead.
			return ApplyInlineDelete(fileLines, match, comparison);

		var removed = fileLines.RemoveAll(l => string.Equals(l.Trim(), match, comparison));
		return Result(fileLines, removed);
	}

	private static PatchApplication ApplyBlock(
		List<string> fileLines, string[] matchLines, List<int> matchIndents, List<string> replaceLines,
		bool matchEndsLine, int tabSize, StringComparison comparison)
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
			var shift = GetShift(fileLines, i, matchLines, matchIndents, replaceLines, tabSize);
			var replacement = BuildBlock(replaceLines, shift, anchor, tabSize, prefixes, suffixes);

			fileLines.RemoveRange(i, matchLines.Length);
			fileLines.InsertRange(i, replacement);

			if (matchEndsLine && i + replacement.Count < fileLines.Count)
			{
				// The match took its line break with it, so whatever followed it continues on the line
				// where the replacement ended. Its indentation goes: the line start it belonged to is
				// gone, and the replacement already carries the file's own prefix.
				var next = i + replacement.Count;
				fileLines[next - 1] += fileLines[next].TrimStart();
				fileLines.RemoveAt(next);
			}

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

	/// <summary>
	/// Splits the text into lines, keeping the empty element a terminating line break leaves behind:
	/// that element is what records that the text reached the end of its last line.
	/// </summary>
	private static List<string> SplitLines(string text)
		=> Normalize(text).Split('\n').ToList();

	/// <summary>
	/// Removes the empty element a terminating line break leaves behind and reports whether there was
	/// one, so the caller knows the text ended at the end of its last line rather than inside it.
	/// </summary>
	private static bool TakeTrailingBreak(List<string> lines)
	{
		if (lines.Count <= 1 || lines[^1].Length != 0)
			return false;

		lines.RemoveAt(lines.Count - 1);
		return true;
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
	/// <para>
	/// When the match's second line sits at the same depth as the second line it matched, the two were
	/// written in one coordinate system: line two of the replacement takes the place of line two of
	/// the match, and the shift is simply their difference. That is a patch copied out of the file,
	/// and it is what keeps such a patch verbatim.
	/// </para>
	/// <para>
	/// When they differ, the patch re-nested the matched block - a brace, a wrapper, a body line that
	/// moved a level - and pairing the lines would pair them with lines they have nothing to do with,
	/// which moved the whole block a level up or down (AR-0024). The patch is then relative to the
	/// match, so the shift is the difference between the first matched file line and the match's own
	/// first line, and every replacement line keeps its offset from the match.
	/// </para>
	/// </remarks>
	private static int GetShift(
		List<string> fileLines, int start, string[] matchLines, List<int> matchIndents,
		List<string> replaceLines, int tabSize)
	{
		if (matchLines.Length > 1 && replaceLines.Count > 1 && start + 1 < fileLines.Count
			&& !string.IsNullOrWhiteSpace(matchLines[1])
			&& !string.IsNullOrWhiteSpace(replaceLines[1])
			&& matchIndents[1] == GetIndentation(fileLines[start + 1], tabSize))
			return matchIndents[1] - GetIndentation(replaceLines[1], tabSize);

		return GetIndentation(fileLines[start], tabSize) - matchIndents[0];
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
