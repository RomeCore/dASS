using LLMDesktopAssistant.Utils.Files;
using LLMDesktopAssistant.Utils.Patterns;

namespace LLMDesktopAssistant.Tests.Utils.Files;

public class GlobWalkerTests : IDisposable
{
	private readonly string _root;

	public GlobWalkerTests()
	{
		_root = Path.Combine(Path.GetTempPath(), "dass-glob-tests", Guid.NewGuid().ToString("N"));

		Write(".git/config", "hidden");
		Write("src/a.cs", "class A { }");
		Write("src/deep/b.cs", "class B { }");
		Write("src/obj/c.cs", "class C { }");
		Write("docs/readme.md", "readme");
	}

	public void Dispose()
	{
		if (Directory.Exists(_root))
			Directory.Delete(_root, true);
	}

	private void Write(string relative, string content)
	{
		var path = Path.Combine(_root, relative);
		Directory.CreateDirectory(Path.GetDirectoryName(path)!);
		File.WriteAllText(path, content);
	}

	private List<string> Enumerate(string pattern, GlobWalkOptions? options = null)
	{
		var search = GlobSearch.From(pattern, _root);

		return GlobWalker
			.Enumerate(search, options ?? GlobWalkOptions.FilesOnly)
			.Select(entry => entry.RelativeToRoot.Replace('\\', '/'))
			.OrderBy(path => path)
			.ToList();
	}

	[Fact]
	public void DoubleStar_FindsFilesRecursively()
	{
		var found = Enumerate("**/*.cs");

		Assert.Equal(["src/a.cs", "src/deep/b.cs"], found);
	}

	[Fact]
	public void Ignore_SkipsBuildOutput()
	{
		Assert.DoesNotContain("src/obj/c.cs", Enumerate("**/*.cs"));
	}

	[Fact]
	public void Ignore_YieldsToAnExplicitlyNamedDirectory()
	{
		Assert.Contains("src/obj/c.cs", Enumerate("**/obj/*.cs"));
	}

	[Fact]
	public void Ignore_SkipsHiddenTreesByName()
	{
		Assert.DoesNotContain("config", Enumerate("**/config"));
	}

	[Fact]
	public void Prefix_ReachesIntoAHiddenTreeWhenNamed()
	{
		Assert.Equal(["config"], Enumerate(".git/**/*"));
	}

	[Fact]
	public void Limit_StopsTheWalk()
	{
		var found = Enumerate("**/*.cs", GlobWalkOptions.FilesOnly with { Limit = 1 });

		Assert.Single(found);
	}

	[Fact]
	public void EmptyRemainder_ReportsTheRootItself()
	{
		var search = GlobSearch.From("src/", _root);

		var entries = GlobWalker
			.Enumerate(search, GlobWalkOptions.FilesOnly with { Directories = true, Files = false })
			.ToList();

		var entry = Assert.Single(entries);
		Assert.Equal("src", entry.Name);
		Assert.Equal(string.Empty, entry.RelativeToRoot);
	}

	[Fact]
	public void SingleSegmentFilePattern_ReportsThatFile()
	{
		var search = GlobSearch.From("readme.md", Path.Combine(_root, "docs"));

		var entry = Assert.Single(GlobWalker.Enumerate(search, GlobWalkOptions.FilesOnly));

		Assert.Equal("readme.md", entry.Name);
		Assert.False(entry.IsDirectory);
	}

	[Fact]
	public void SingleSegmentFilePattern_KeepsTheStartingDirectoryAsTheRoot()
	{
		var start = Path.Combine(_root, "docs");

		var search = GlobSearch.From("readme.md", start);

		Assert.Equal(start, search.Root);
		Assert.Equal(["readme.md"], GlobWalker.Enumerate(search, GlobWalkOptions.FilesOnly)
			.Select(entry => entry.RelativeToRoot.Replace('\\', '/')));
	}

	[Fact]
	public void FilePatternInADirectory_ReportsThatFile()
	{
		var search = GlobSearch.From("src/a.cs", _root);

		var entry = Assert.Single(GlobWalker.Enumerate(search, GlobWalkOptions.FilesOnly));

		Assert.Equal("a.cs", entry.Name);
	}

	[Fact]
	public void DirectoryPattern_ReportsNoFiles()
	{
		var search = GlobSearch.From("src", _root);

		Assert.Empty(GlobWalker.Enumerate(search, GlobWalkOptions.FilesOnly));
	}

	[Fact]
	public void MissingRoot_YieldsNothing()
	{
		var search = GlobSearch.From("nope/**/*.cs", _root);

		Assert.Empty(GlobWalker.Enumerate(search, GlobWalkOptions.FilesOnly));
	}

	[Fact]
	public void ExplicitPath_IsResolvedAgainstIt()
	{
		var search = GlobSearch.From("**/*.cs", Path.Combine(_root, "src"));

		Assert.Equal(Path.Combine(_root, "src"), search.Root);
		Assert.Equal(["a.cs", "deep/b.cs"], GlobWalker
			.Enumerate(search, GlobWalkOptions.FilesOnly)
			.Select(entry => entry.RelativeToRoot.Replace('\\', '/'))
			.OrderBy(path => path));
	}

	[Fact]
	public void RootedPattern_ResolvesItsOwnRoot()
	{
		var absolute = Path.Combine(_root, "src", "**", "*.cs");

		var search = GlobSearch.From(absolute, _root);

		Assert.Equal(Path.Combine(_root, "src"), search.Root);
	}

	[Fact]
	public void RelativeStartingPath_IsRejected()
	{
		Assert.Throws<ArgumentException>(() => GlobSearch.From("**/*.cs", "relative"));
	}

	[Fact]
	public void WithRoot_KeepsThePatternAndRecomputesTheDisplay()
	{
		var search = GlobSearch.From("**/*.cs", _root);
		var moved = search.WithRoot(Path.Combine(_root, "src"));

		Assert.Same(search.Pattern, moved.Pattern);
	}
}
