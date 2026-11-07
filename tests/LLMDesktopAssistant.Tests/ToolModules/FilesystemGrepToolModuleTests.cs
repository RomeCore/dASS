using System.Text.Json.Nodes;
using LLMDesktopAssistant.LLM.Services;
using LLMDesktopAssistant.LLM.Settings;
using LLMDesktopAssistant.Tests.Tools;
using LLMDesktopAssistant.Tools;
using LLMDesktopAssistant.Tools.Implementations.Filesystem;

namespace LLMDesktopAssistant.Tests.ToolModules;

/// <summary>
/// Tests for the <see cref="FilesystemGrepToolModule"/> class. The path argument is a glob path,
/// so the cases here cover the shapes a caller writes: one file, a glob set, a bare name in the
/// working directory and the two diagnostics for a path that selects nothing.
/// </summary>
public class FilesystemGrepToolModuleTests
{
	private sealed class TempDirFileAccess(string root) : IWorkingDirectoryAccessService
	{
		public string CheckedAccessPath(string path, DirectoryAccessMode mode, out bool isAccessed)
		{
			isAccessed = true;
			return Path.Combine(root, path);
		}

		public string GetWorkingDirectory() => root;
	}

	private sealed class TempDir : IDisposable
	{
		public string Root { get; } = Path.Combine(Path.GetTempPath(), "fs-grep-tests-" + Guid.NewGuid().ToString("N"));

		public TempDir() => Directory.CreateDirectory(Root);

		public string Write(string relativePath, string content)
		{
			var fullPath = Path.Combine(Root, relativePath);
			Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
			File.WriteAllText(fullPath, content);
			return fullPath;
		}

		public void Dispose()
		{
			try
			{
				Directory.Delete(Root, true);
			}
			catch
			{
				// Best effort cleanup
			}
		}
	}

	private static ToolInfo GetGrepTool(string root)
	{
		var module = new FilesystemGrepToolModule(new TempDirFileAccess(root));
		return module.GetTools().Single(tool => tool.Name == "fs-grep");
	}

	private static Task<LLMDesktopAssistant.LLM.Domain.ToolCall> ExecuteAsync(ToolInfo tool, string path, string pattern)
	{
		var args = new JsonObject
		{
			["path"] = path,
			["pattern"] = pattern
		};

		return ToolTestContext.ExecuteAsync(tool, args);
	}

	[Fact]
	public async Task FileInASubdirectory_IsSearched()
	{
		using var temp = new TempDir();
		temp.Write("src/a.cs", "public class A { }");

		var result = await ExecuteAsync(GetGrepTool(temp.Root), "src/a.cs", "class");

		Assert.Contains("public class A", result.ResultContent);
	}

	[Fact]
	public async Task GlobSet_IsSearched()
	{
		using var temp = new TempDir();
		temp.Write("src/a.cs", "public class A { }");
		temp.Write("src/deep/b.cs", "public class B { }");
		temp.Write("docs/readme.md", "public class Docs { }");

		var result = await ExecuteAsync(GetGrepTool(temp.Root), "src/**/*.cs", "class");

		Assert.Contains("public class A", result.ResultContent);
		Assert.Contains("public class B", result.ResultContent);
		Assert.DoesNotContain("public class Docs", result.ResultContent);
	}

	[Fact]
	public async Task BareFileName_IsSearched()
	{
		using var temp = new TempDir();
		temp.Write("root.txt", "needle");

		var result = await ExecuteAsync(GetGrepTool(temp.Root), "root.txt", "needle");

		Assert.Contains("needle", result.ResultContent);
	}

	[Fact]
	public async Task DirectoryPath_IsReportedAsADirectory()
	{
		using var temp = new TempDir();
		temp.Write("src/a.cs", "public class A { }");

		var result = await ExecuteAsync(GetGrepTool(temp.Root), "src", "class");

		Assert.Contains("names a directory, not files", result.ResultContent);
	}

	[Fact]
	public async Task MissingDirectory_IsReportedAsNotFound()
	{
		using var temp = new TempDir();

		var result = await ExecuteAsync(GetGrepTool(temp.Root), "nope/a.cs", "class");

		Assert.Contains("Path not found", result.ResultContent);
	}
}
