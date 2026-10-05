using System.Text;
using System.Text.RegularExpressions;
using LLMDesktopAssistant.Controls.Icons;

namespace LLMDesktopAssistant.Tests.Controls.Icons;

/// <summary>
/// Guards against icon literals in XAML that no longer resolve - this catches typos and, more
/// importantly, material icons that are only reachable under an *aliased* enum name (which a
/// naive <c>Enum.GetValues()</c>-based lookup would miss).
/// </summary>
public partial class XamlIconLiteralsTests
{
	[Fact]
	public void AllKindLiteralsResolve()
	{
		var root = FindRepoRoot();
		var sb = new StringBuilder();
		var total = 0;
		var failing = new List<string>();

		foreach (var file in Directory.EnumerateFiles(Path.Combine(root, "src"), "*.axaml", SearchOption.AllDirectories))
		{
			if (file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}")
				|| file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
				continue;

			var text = File.ReadAllText(file);
			foreach (Match match in KindRegex().Matches(text))
			{
				var value = match.Groups[1].Value;
				total++;

				var data = VisualIconDataHandler.Resolve(VisualIconKind.Parse(value));
				if (!data.IsOk)
					failing.Add($"{Path.GetFileName(file)}: Kind=\"{value}\" -> {data.Status}");
			}
		}

		var distinct = failing.Distinct().OrderBy(x => x).ToList();
		sb.AppendLine($"scanned Kind literals: {total}; failing: {distinct.Count}");
		foreach (var line in distinct.Take(30))
			sb.AppendLine("  " + line);

		Assert.True(distinct.Count == 0, sb.ToString());
	}

	private static string FindRepoRoot()
	{
		var dir = new DirectoryInfo(AppContext.BaseDirectory);
		while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "src", "LLMDesktopAssistant")))
			dir = dir.Parent;

		return dir?.FullName ?? throw new InvalidOperationException("Repository root not found.");
	}

	[GeneratedRegex("Kind=\"([^\"{}]+)\"")]
	private static partial Regex KindRegex();
}
