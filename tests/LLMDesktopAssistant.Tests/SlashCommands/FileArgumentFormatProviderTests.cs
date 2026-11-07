using LLMDesktopAssistant.LLM.Services;
using LLMDesktopAssistant.LLM.Settings;
using LLMDesktopAssistant.SlashCommands.Arguments;
using Microsoft.Extensions.DependencyInjection;

namespace LLMDesktopAssistant.Tests.SlashCommands;

/// <summary>
/// The file-path format provider of a tool command argument: it lists one directory, continues a directory
/// with the typed separator and ends a file, so a path can be walked with several tabs.
/// </summary>
public sealed class FileArgumentFormatProviderTests : IDisposable
{
	private readonly string _root;

	public FileArgumentFormatProviderTests()
	{
		_root = Path.Combine(Path.GetTempPath(), "dass-file-completion-" + Guid.NewGuid().ToString("N"));
		Directory.CreateDirectory(Path.Combine(_root, "src", "nested"));
		File.WriteAllText(Path.Combine(_root, "src", "main.cs"), string.Empty);
		File.WriteAllText(Path.Combine(_root, "README.md"), string.Empty);
	}

	public void Dispose()
	{
		if (Directory.Exists(_root))
			Directory.Delete(_root, recursive: true);
	}

	private FileArgumentFormatProvider Provider(bool onlyDirectories = false) =>
		new(new ServiceCollection()
			.AddSingleton<IWorkingDirectoryAccessService>(new StubWorkingDirectory(_root))
			.BuildServiceProvider(), onlyDirectories);

	private static SlashCommandCompletionContext Context() => new()
	{
		Command = null!, // this provider lists the filesystem and never reads the command
		RawArguments = string.Empty,
		CurrentPrefix = string.Empty
	};

	[Fact]
	public void EmptyPrefix_ListsDirectoriesFirst()
	{
		var items = Provider().Complete(string.Empty, Context()).ToList();

		Assert.Equal(["src" + Path.DirectorySeparatorChar, "README.md"], items.Select(item => item.Value));
		Assert.False(items[0].InsertsSpace);
		Assert.True(items[1].InsertsSpace);
	}

	[Fact]
	public void DirectoryPrefix_ContinuesInsideTheDirectory()
	{
		var items = Provider().Complete("src/", Context()).ToList();

		// The continuation keeps the separator the user typed.
		Assert.Equal(["src/nested/", "src/main.cs"], items.Select(item => item.Value));
		Assert.False(items[0].InsertsSpace);
		Assert.True(items[1].InsertsSpace);
	}

	[Fact]
	public void Prefix_FiltersEntries()
	{
		var items = Provider().Complete("RE", Context()).ToList();

		Assert.Equal(["README.md"], items.Select(item => item.Value));
	}

	[Fact]
	public void DirectoriesOnly_HidesFiles()
	{
		var items = Provider(onlyDirectories: true).Complete("src/", Context()).ToList();

		Assert.Equal(["src/nested/"], items.Select(item => item.Value));
	}

	[Fact]
	public void MissingDirectory_OffersNothing()
	{
		Assert.Empty(Provider().Complete("nope/", Context()));
	}

	[Fact]
	public void AbsolutePrefix_IsListedAsItIs()
	{
		var items = Provider().Complete(_root + Path.DirectorySeparatorChar + "RE", Context()).ToList();

		Assert.Equal([_root + Path.DirectorySeparatorChar + "README.md"], items.Select(item => item.Value));
	}

	private sealed class StubWorkingDirectory(string root) : IWorkingDirectoryAccessService
	{
		public string CheckedAccessPath(string path, DirectoryAccessMode mode, out bool isAccessed)
		{
			isAccessed = true;
			return Path.Combine(root, path);
		}

		public string GetWorkingDirectory() => root;
	}
}
