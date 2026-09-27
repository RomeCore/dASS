using System.Text;

namespace LLMDesktopAssistant.Utils.Files
{
	/// <summary>
	/// Represents the diff hunk group of lines, computed by <see cref="UnifiedDiff"/>.
	/// </summary>
	public readonly struct HunkGroup
	{
		public required readonly int OldStart { get; init; }

		public required readonly int OldCount { get; init; }

		public required readonly int NewStart { get; init; }

		public required readonly int NewCount { get; init; }

		public required readonly List<HunkLine> Lines { get; init; }

		/// <summary>
		/// Returns the unified diff for this group, including header (@@ ... @@). <br/>
		/// The output format (unified diff):
		/// <code>
		/// @@ -1,2 +3,4 @@
		///  Some line before change...
		/// -Removed line
		/// +Added line
		///  Some context line after change...
		/// </code>
		/// </summary>
		public override string ToString()
		{
			if (Lines is null || Lines.Count == 0)
				return $"@@ -{OldStart},{OldCount} +{NewStart},{NewCount} @@";
			
			var sb = new StringBuilder();
			sb.AppendLine($"@@ -{OldStart},{OldCount} +{NewStart},{NewCount} @@");
			for (int i = 0; i < Lines.Count; i++)
			{
				var line = Lines[i];
				if (i == Lines.Count - 1)
					sb.Append(line.ToString());
				else
					sb.AppendLine(line.ToString());
			}
			return sb.ToString();
		}

		/// <summary>
		/// Returns the lines of this group without the hunk header (@@ ... @@),
		/// suitable for a "clean" diff rendering.
		/// </summary>
		public string ToCleanString()
		{
			if (Lines is null || Lines.Count == 0)
				return string.Empty;

			var sb = new StringBuilder();
			for (int i = 0; i < Lines.Count; i++)
			{
				sb.Append(Lines[i].ToString());
				if (i < Lines.Count - 1)
					sb.AppendLine();
			}
			return sb.ToString();
		}

		/// <summary>
		/// Returns the total number of lines
		/// <summary>
		/// Returns the total number of lines that have been removed and added across all hunk groups.
		/// </summary>
		public (int Removed, int Added) GetChangeCounts()
		{
			int removed = 0, added = 0;

			foreach (var line in Lines)
			{
				if (line.Kind == '-')
					removed++;
				else if (line.Kind == '+')
					added++;
			}

			return (removed, added);
		}
	}
}