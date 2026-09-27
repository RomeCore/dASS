using System.Collections;
using System.Text;

namespace LLMDesktopAssistant.Utils.Files
{
	/// <summary>
	/// Represents the diff hunk groups collection, computed by <see cref="UnifiedDiff"/>.
	/// </summary>
	public readonly struct HunkGroups : IEnumerable<HunkGroup>
	{
		public required readonly List<HunkGroup> Groups { get; init; }

		public readonly bool HasGroups => Groups?.Count > 0;

		public IEnumerator<HunkGroup> GetEnumerator()
		{
			return ((IEnumerable<HunkGroup>)Groups).GetEnumerator();
		}

		IEnumerator IEnumerable.GetEnumerator()
		{
			return ((IEnumerable)Groups).GetEnumerator();
		}

		/// <summary>
		/// Returns the concatenated strings of <see cref="Groups"/>, delimited by double newlines. <br/>
		/// The output format (unified diff):
		/// <code>
		/// @@ -1,2 +3,4 @@
		///  Some line before change...
		/// -Removed line
		/// +Added line
		///  Some context line after change...
		/// 
		/// @@ -5,6 +7,8 @@
		///  The next group goes here...
		/// </code>
		/// </summary>
		public override string ToString()
		{
			if (Groups is null || Groups.Count == 0)
				return string.Empty;
			if (Groups.Count == 1)
				return Groups[0].ToString();

			var sb = new StringBuilder();
			for (int i = 0; i < Groups.Count; i++)
			{
				sb.Append(Groups[i].ToString());
				if (i < Groups.Count - 1)
					sb.AppendLine().AppendLine();
			}
			return sb.ToString();
		}

		/// <summary>
		/// Returns the concatenated strings of <see cref="Groups"/> without the hunk headers,
		/// with the groups delimited by the specified separator (an ellipsis line by default).
		/// The output format (clean diff):
		/// <code>
		///  Some line before change...
		/// -Removed line
		/// +Added line
		/// ...
		/// -Another removed line
		/// +Another added line
		/// </code>
		/// </summary>
		public string ToCleanString(string hunkSeparator = "...")
		{
			if (Groups is null || Groups.Count == 0)
				return string.Empty;
			if (Groups.Count == 1)
				return Groups[0].ToCleanString();

			var sb = new StringBuilder();
			for (int i = 0; i < Groups.Count; i++)
			{
				if (i > 0)
					sb.AppendLine(hunkSeparator);
				sb.Append(Groups[i].ToCleanString());
				if (i < Groups.Count - 1)
					sb.AppendLine();
			}
			return sb.ToString();
		}

		/// <summary>
		/// Applies this diff to the original text and returns the modified text.
		/// Only changes from the included groups are applied; context lines are preserved.
		/// The diff groups must be in order and non-overlapping (as produced by <see cref="UnifiedDiff.Compute"/>).
		/// </summary>
		/// <param name="originalText">The original text to apply changes to.</param>
		/// <returns>The modified text after applying all diff groups.</returns>
		public readonly string ApplyToText(string originalText)
		{
			if (Groups is null || Groups.Count == 0)
				return originalText;

			var originalLines = SplitLines(originalText);
			var resultLines = new List<string>(originalLines);

			// Apply chunks from last to first to preserve line indices
			for (int g = Groups.Count - 1; g >= 0; g--)
			{
				var group = Groups[g];
				int oldStart = group.OldStart - 1; // convert to 0-based
				int oldCount = group.OldCount;

				// Collect new lines from the group (context + additions)
				var newLines = new List<string>();
				foreach (var line in group.Lines)
				{
					if (line.Kind is ' ' or '+')
						newLines.Add(line.Content);
				}

				// Replace the range in original lines
				resultLines.RemoveRange(oldStart, oldCount);
				resultLines.InsertRange(oldStart, newLines);
			}

			return string.Join(Environment.NewLine, resultLines);
		}

		private static List<string> SplitLines(string text)
		{
			if (string.IsNullOrEmpty(text))
				return [];

			var lines = new List<string>();
			int start = 0;
			for (int i = 0; i < text.Length; i++)
			{
				if (text[i] == '\r' || text[i] == '\n')
				{
					int end = i;
					if (text[i] == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
						i++;
					lines.Add(text[start..end]);
					start = i + 1;
				}
			}
			if (start < text.Length)
				lines.Add(text[start..]);
			else if (text.Length > 0 && (text[^1] == '\r' || text[^1] == '\n'))
				lines.Add(string.Empty);

			return lines;
		}

		/// <summary>
		/// Returns the total number of lines
		/// <summary>
		/// Returns the total number of lines that have been removed and added across all hunk groups.
		/// </summary>
		public (int Removed, int Added) GetChangeCounts()
		{
			int removed = 0, added = 0;

			foreach (var group in Groups)
			{
				foreach (var line in group.Lines)
				{
					if (line.Kind == '-')
						removed++;
					else if (line.Kind == '+')
						added++;
				}
			}

			return (removed, added);
		}
	}
}