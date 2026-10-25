namespace LLMDesktopAssistant.Utils.Files
{
	/// <summary>
	/// Unified diff generator using LCS (Longest Common Subsequence).
	/// Produces output similar to `git diff --no-color` - no external dependencies.
	/// </summary>
	public static class UnifiedDiff
	{
		public static HunkGroups Compute(string oldText, string newText, int contextLines = 3)
		{
			if (oldText == newText)
				return new HunkGroups { Groups = [] };

			var result = new HunkGroups { Groups = [] };

			var oldLines = SplitLines(oldText);
			var newLines = SplitLines(newText);

			// Build the LCS table (Wagner-Fischer)
			int m = oldLines.Length;
			int n = newLines.Length;
			var lcs = new int[m + 1, n + 1];

			for (int i = 1; i <= m; i++)
			{
				for (int j = 1; j <= n; j++)
				{
					lcs[i, j] = oldLines[i - 1] == newLines[j - 1]
						? lcs[i - 1, j - 1] + 1
						: Math.Max(lcs[i - 1, j], lcs[i, j - 1]);
				}
			}

			// Backtrack to build edit operations
			var ops = new List<(int oldLine, int newLine, char kind, string text)>();
			int oi = m, ni = n;

			while (oi > 0 || ni > 0)
			{
				if (oi > 0 && ni > 0 && oldLines[oi - 1] == newLines[ni - 1])
				{
					ops.Add((oi - 1, ni - 1, ' ', oldLines[oi - 1]));
					oi--;
					ni--;
				}
				else if (ni > 0 && (oi == 0 || lcs[oi, ni - 1] >= lcs[oi - 1, ni]))
				{
					ops.Add((oi > 0 ? oi - 1 : -1, ni - 1, '+', newLines[ni - 1]));
					ni--;
				}
				else if (oi > 0)
				{
					ops.Add((oi - 1, ni > 0 ? ni - 1 : -1, '-', oldLines[oi - 1]));
					oi--;
				}
			}

			ops.Reverse();

			int lastOld = -1, lastNew = -1;
			int hunkStart = 0;
			var hunkLines = new List<(char kind, string text)>();

			void FlushHunk()
			{
				if (hunkLines.Count == 0)
					return;

				int oldStart = hunkStart + 1;
				int newStart = hunkStart + 1;
				int oldCount = 0, newCount = 0;

				// Count actual changes in the hunk (excluding context)
				foreach (var (kind, _) in hunkLines)
				{
					if (kind == '-') oldCount++;
					else if (kind == '+') newCount++;
					else { oldCount++; newCount++; }
				}

				var currentGroup = new HunkGroup
				{
					Lines = [],
					OldStart = oldStart,
					OldCount = oldCount,
					NewStart = newStart,
					NewCount = newCount
				};
				result.Groups.Add(currentGroup);

				foreach (var (kind, text) in hunkLines)
					currentGroup.Lines.Add(new HunkLine
					{
						Kind = kind,
						Content = text
					});
			}

			foreach (var (oldLine, newLine, kind, text) in ops)
			{
				if (kind == ' ')
				{
					// Context line
					if (hunkLines.Count > 0)
					{
						hunkLines.Add((kind, text));
						// Count context lines after last change
						int contextCount = 0;
						for (int i = hunkLines.Count - 1; i >= 0; i--)
						{
							if (hunkLines[i].kind is '-' or '+')
							{
								contextCount = 0;
								break;
							}
							contextCount++;
							if (contextCount > contextLines)
								break;
						}

						if (contextCount > contextLines)
						{
							// Remove extra context lines from end
							int remove = contextCount - contextLines;
							hunkLines.RemoveRange(hunkLines.Count - remove, remove);
							FlushHunk();
							hunkLines.Clear();
							// Add trailing context as leading context for next hunk
							// (simplified: just reset)
						}
					}
					lastOld = oldLine;
					lastNew = newLine;
				}
				else
				{
					// Change line
					if (hunkLines.Count == 0)
					{
						hunkStart = Math.Max(0, oldLine - contextLines);
						// Add leading context
						int ctxStart = Math.Max(0, oldLine - contextLines);
						// For insertions (+), oldLine points to the anchor line (which is context),
						// so we include it. For deletions (-), oldLine IS the change itself.
						int ctxEnd = kind == '+' ? oldLine + 1 : oldLine;
						for (int i = ctxStart; i < ctxEnd; i++)
							hunkLines.Add((' ', oldLines[i]));
					}
					hunkLines.Add((kind, text));
					lastOld = oldLine;
					lastNew = newLine;
				}
			}

			if (hunkLines.Count > 0)
				FlushHunk();

			return result;
		}

		private static string[] SplitLines(string text)
		{
			if (string.IsNullOrEmpty(text))
				return [];
			return text.Split(["\r\n", "\n", "\r"], StringSplitOptions.None);
		}
	}
}