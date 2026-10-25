using System.Text.Json.Nodes;
using LLMDesktopAssistant.Addons;
using LLMDesktopAssistant.Addons.Loading;
using LLMDesktopAssistant.Addons.Management;
using LLMDesktopAssistant.LLM.Services;
using LLMDesktopAssistant.LLM.Settings;
using LLMDesktopAssistant.Tests.Tools;
using LLMDesktopAssistant.Tools;
using LLMDesktopAssistant.Tools.Implementations.Filesystem;
using LLMDesktopAssistant.Utils.Files;
using Xunit.Abstractions;

namespace LLMDesktopAssistant.Tests.ToolModules;

/// <summary>
/// Tests for the <see cref="FilesystemEditToolModule"/> class.
/// </summary>
public class FilesystemEditToolModuleTests(ITestOutputHelper output)
{
	private sealed class TempDirFileAccess : IWorkingDirectoryAccessService
	{
		private readonly string _root;

		public TempDirFileAccess(string root) => _root = root;

		public string AccessPath(string path, DirectoryAccessMode mode) => Path.Combine(_root, path);

		public string CheckedAccessPath(string path, DirectoryAccessMode mode, out bool isAccessed)
		{
			isAccessed = true;
			return Path.Combine(_root, path);
		}

		public string GetWorkingDirectory() => _root;

		public string? TryAccessPath(string path, DirectoryAccessMode mode) => Path.Combine(_root, path);
	}

	private sealed class TempDir : IDisposable
	{
		public string Root { get; } = Path.Combine(Path.GetTempPath(), "fs-edit-tests-" + Guid.NewGuid().ToString("N"));

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

	private class StubAddonPathImpactDetector : IAddonPathImpactDetector
	{
		public AddonKind Detect(string path, bool? isFile, FileOperation operation = FileOperation.Create, string? existingAncestorDirectory = null)
		{
			return AddonKind.None;
		}
	}

	private class StubAddonManagerInvalidator : IAddonManagerInvalidator
	{
		public void Invalidate(AddonKind kinds)
		{
		}

		public void Reload(AddonKind kinds)
		{
		}

		public void ReloadIfInvalid(AddonKind kinds)
		{
		}
	}

	private static JsonObject Patch(string match, string? replace = null, bool useRegex = false, bool ignoreCase = false)
	{
		var patch = new JsonObject
		{
			["match"] = match
		};
		if (replace != null)
			patch["replace"] = replace;
		if (useRegex)
			patch["useRegex"] = true;
		if (ignoreCase)
			patch["ignoreCase"] = true;
		return patch;
	}

	private static FilesystemEditToolModule CreateModule(string root) => new(new TempDirFileAccess(root), new StubAddonPathImpactDetector(), new StubAddonManagerInvalidator());

	private static ToolInfo GetFsEditTool(FilesystemEditToolModule module)
		=> module.GetTools().Single(t => t.Name == "fs-edit");

	private static Task<LLMDesktopAssistant.LLM.Domain.ToolCall> ExecuteAsync(ToolInfo tool, JsonObject args)
	{
		return ToolTestContext.ExecuteAsync(tool, args);
	}

	[Fact]
	public async Task Replace_Plain_PreservesPrefixAndSuffix()
	{
		using var temp = new TempDir();
		var file = temp.Write("file.txt", "    abc");

		var result = await ExecuteAsync(GetFsEditTool(CreateModule(temp.Root)), new JsonObject
		{
			["path"] = "file.txt",
			["patches"] = new JsonArray { Patch("ab", "de") }
		});

		Assert.Contains("File edited successfully", result.ResultContent);
		Assert.Equal("    dec", File.ReadAllText(file));
	}

	[Fact]
	public async Task Replace_Plain_IgnoresIndentation()
	{
		using var temp = new TempDir();
		var file = temp.Write("file.cs", "    public class Foo {");

		var result = await ExecuteAsync(GetFsEditTool(CreateModule(temp.Root)), new JsonObject
		{
			["path"] = "file.cs",
			["patches"] = new JsonArray { Patch("public class Foo", "public class Bar") }
		});

		Assert.Contains("File edited successfully", result.ResultContent);
		Assert.Equal("    public class Bar {", File.ReadAllText(file));
	}

	[Fact]
	public async Task Replace_Plain_AllOccurrences()
	{
		using var temp = new TempDir();
		var file = temp.Write("file.txt", "foo foo foo");

		var result = await ExecuteAsync(GetFsEditTool(CreateModule(temp.Root)), new JsonObject
		{
			["path"] = "file.txt",
			["patches"] = new JsonArray { Patch("foo", "bar") }
		});

		Assert.Contains("File edited successfully", result.ResultContent);
		Assert.Equal("bar bar bar", File.ReadAllText(file));
	}

	[Fact]
	public async Task Replace_Plain_IgnoreCase()
	{
		using var temp = new TempDir();
		var file = temp.Write("file.txt", "Hello World");

		var result = await ExecuteAsync(GetFsEditTool(CreateModule(temp.Root)), new JsonObject
		{
			["path"] = "file.txt",
			["patches"] = new JsonArray { Patch("hello", "Hi", ignoreCase: true) }
		});

		Assert.Contains("File edited successfully", result.ResultContent);
		Assert.Equal("Hi World", File.ReadAllText(file));
	}

	[Fact]
	public async Task Replace_Plain_MultilineMatch()
	{
		using var temp = new TempDir();
		var file = temp.Write("file.txt", "a\nb\nc");

		var result = await ExecuteAsync(GetFsEditTool(CreateModule(temp.Root)), new JsonObject
		{
			["path"] = "file.txt",
			["patches"] = new JsonArray { Patch("a\nb", "x\ny") }
		});

		Assert.Contains("File edited successfully", result.ResultContent);
		Assert.Equal("x\ny\nc", File.ReadAllText(file));
	}

	[Fact]
	public async Task Replace_Plain_MultilineMatch_PreservesIndentation()
	{
		using var temp = new TempDir();
		var file = temp.Write("file.txt", "    a\n    b\n    c");

		var result = await ExecuteAsync(GetFsEditTool(CreateModule(temp.Root)), new JsonObject
		{
			["path"] = "file.txt",
			["patches"] = new JsonArray { Patch("a\nb", "x") }
		});

		Assert.Contains("File edited successfully", result.ResultContent);
		Assert.Equal("    x\n    c", File.ReadAllText(file));
	}

	[Fact]
	public async Task Replace_Plain_MultilineReplacement()
	{
		using var temp = new TempDir();
		var file = temp.Write("file.txt", "    a\n    b");

		var result = await ExecuteAsync(GetFsEditTool(CreateModule(temp.Root)), new JsonObject
		{
			["path"] = "file.txt",
			["patches"] = new JsonArray { Patch("a", "x\ny") }
		});

		Assert.Contains("File edited successfully", result.ResultContent);
		Assert.Equal("    x\n    y\n    b", File.ReadAllText(file));
	}

	[Fact]
	public async Task Delete_Plain_EmptyReplace()
	{
		using var temp = new TempDir();
		var file = temp.Write("file.txt", "line1\nfoo\nline3");

		var result = await ExecuteAsync(GetFsEditTool(CreateModule(temp.Root)), new JsonObject
		{
			["path"] = "file.txt",
			["patches"] = new JsonArray { Patch("foo", "") }
		});

		Assert.Contains("File edited successfully", result.ResultContent);
		Assert.Equal("line1\n\nline3", File.ReadAllText(file));
	}

	[Fact]
	public async Task Replace_Regex_CaptureGroup()
	{
		using var temp = new TempDir();
		var file = temp.Write("file.cs", "class Foo\nclass Bar");

		var result = await ExecuteAsync(GetFsEditTool(CreateModule(temp.Root)), new JsonObject
		{
			["path"] = "file.cs",
			["patches"] = new JsonArray { Patch("class (\\w+)", "class Renamed_$1", useRegex: true) }
		});

		Assert.Contains("File edited successfully", result.ResultContent);
		Assert.Equal("class Renamed_Foo\nclass Renamed_Bar", File.ReadAllText(file));
	}

	[Fact]
	public async Task Replace_Regex_NamedGroup()
	{
		using var temp = new TempDir();
		var file = temp.Write("file.txt", "val = 42");

		var result = await ExecuteAsync(GetFsEditTool(CreateModule(temp.Root)), new JsonObject
		{
			["path"] = "file.txt",
			["patches"] = new JsonArray { Patch("val = (?<n>\\d+)", "val = ${n}x", useRegex: true) }
		});

		Assert.Contains("File edited successfully", result.ResultContent);
		Assert.Equal("val = 42x", File.ReadAllText(file));
	}

	[Fact]
	public async Task Replace_Plain_MultiLineFile_AllOccurrences()
	{
		using var temp = new TempDir();
		var file = temp.Write("file.txt", "foo\nx\nfoo");

		var result = await ExecuteAsync(GetFsEditTool(CreateModule(temp.Root)), new JsonObject
		{
			["path"] = "file.txt",
			["patches"] = new JsonArray { Patch("foo", "bar") }
		});

		Assert.Contains("File edited successfully", result.ResultContent);
		Assert.Equal("bar\nx\nbar", File.ReadAllText(file));
	}

	[Fact]
	public async Task Replace_Plain_MultipleMultilineBlocks()
	{
		using var temp = new TempDir();
		var file = temp.Write("file.txt", "a\nb\nsep\na\nb");

		var result = await ExecuteAsync(GetFsEditTool(CreateModule(temp.Root)), new JsonObject
		{
			["path"] = "file.txt",
			["patches"] = new JsonArray { Patch("a\nb", "X") }
		});

		Assert.Contains("File edited successfully", result.ResultContent);
		Assert.Equal("X\nsep\nX", File.ReadAllText(file));
	}

	[Fact]
	public async Task Delete_Plain_MultilineBlock_PreservesSurroundings()
	{
		using var temp = new TempDir();
		var file = temp.Write("file.txt", "x a\nb y");

		var result = await ExecuteAsync(GetFsEditTool(CreateModule(temp.Root)), new JsonObject
		{
			["path"] = "file.txt",
			["patches"] = new JsonArray { Patch("a\nb", "") }
		});

		Assert.Contains("File edited successfully", result.ResultContent);
		Assert.Equal("x  y", File.ReadAllText(file));
	}

	[Fact]
	public async Task Patch_WithoutReplace_DeletesMatch()
	{
		using var temp = new TempDir();
		var file = temp.Write("file.txt", "foo bar");

		var result = await ExecuteAsync(GetFsEditTool(CreateModule(temp.Root)), new JsonObject
		{
			["path"] = "file.txt",
			["patches"] = new JsonArray { Patch("foo") }
		});

		Assert.Contains("File edited successfully", result.ResultContent);
		Assert.Equal(" bar", File.ReadAllText(file));
	}

	[Fact]
	public async Task Replace_Plain_TabsInIndentation_Ignored()
	{
		using var temp = new TempDir();
		var file = temp.Write("file.cs", "\tpublic class Foo");

		var result = await ExecuteAsync(GetFsEditTool(CreateModule(temp.Root)), new JsonObject
		{
			["path"] = "file.cs",
			["patches"] = new JsonArray { Patch("public class Foo", "public class Bar") }
		});

		Assert.Contains("File edited successfully", result.ResultContent);
		Assert.Equal("\tpublic class Bar", File.ReadAllText(file));
	}

	[Fact]
	public async Task Replace_Plain_NonOverlappingOccurrences()
	{
		using var temp = new TempDir();
		var file = temp.Write("file.txt", "aaaa");

		var result = await ExecuteAsync(GetFsEditTool(CreateModule(temp.Root)), new JsonObject
		{
			["path"] = "file.txt",
			["patches"] = new JsonArray { Patch("aa", "b") }
		});

		Assert.Contains("File edited successfully", result.ResultContent);
		Assert.Equal("bb", File.ReadAllText(file));
	}

	[Fact]
	public async Task Replace_Plain_NoRecursionIntoReplacement()
	{
		using var temp = new TempDir();
		var file = temp.Write("file.txt", "foo");

		var result = await ExecuteAsync(GetFsEditTool(CreateModule(temp.Root)), new JsonObject
		{
			["path"] = "file.txt",
			["patches"] = new JsonArray { Patch("foo", "foofoo") }
		});

		Assert.Contains("File edited successfully", result.ResultContent);
		Assert.Equal("foofoo", File.ReadAllText(file));
	}

	[Fact]
	public async Task Replace_Plain_EmptyFile_NotFound()
	{
		using var temp = new TempDir();
		var file = temp.Write("file.txt", "");

		var result = await ExecuteAsync(GetFsEditTool(CreateModule(temp.Root)), new JsonObject
		{
			["path"] = "file.txt",
			["patches"] = new JsonArray { Patch("foo", "bar") }
		});

		Assert.Contains("No changes were made to the file.", result.ResultContent);
		Assert.Contains("Patch #1 (match: \"foo\"): no occurrences found.", result.ResultContent);
		Assert.Equal("", File.ReadAllText(file));
	}

	[Fact]
	public async Task Delete_Regex_EmptyReplace()
	{
		using var temp = new TempDir();

		var file = temp.Write("file.txt", "x = 1;\nfoo()\ny = 2;");

		var result = await ExecuteAsync(GetFsEditTool(CreateModule(temp.Root)), new JsonObject
		{
			["path"] = "file.txt",
			["patches"] = new JsonArray { Patch("foo\\(\\)", "", useRegex: true) }
		});

		Assert.Contains("File edited successfully", result.ResultContent);
		Assert.Equal("x = 1;\n\ny = 2;", File.ReadAllText(file));
	}

	[Fact]
	public async Task Replace_Regex_IgnoreCase()
	{
		using var temp = new TempDir();
		var file = temp.Write("file.txt", "Hello World");

		var result = await ExecuteAsync(GetFsEditTool(CreateModule(temp.Root)), new JsonObject
		{
			["path"] = "file.txt",
			["patches"] = new JsonArray { Patch("hello", "Hi", useRegex: true, ignoreCase: true) }
		});

		Assert.Contains("File edited successfully", result.ResultContent);
		Assert.Equal("Hi World", File.ReadAllText(file));
	}

	[Fact]
	public async Task Patches_AppliedSequentially()
	{
		using var temp = new TempDir();
		var file = temp.Write("file.txt", "foo bar");

		var result = await ExecuteAsync(GetFsEditTool(CreateModule(temp.Root)), new JsonObject
		{
			["path"] = "file.txt",
			["patches"] = new JsonArray { Patch("foo", "baz"), Patch("bar", "qux") }
		});

		Assert.Contains("File edited successfully", result.ResultContent);
		Assert.Equal("baz qux", File.ReadAllText(file));
	}

	[Fact]
	public async Task Patches_MixedPlainAndRegex()
	{
		using var temp = new TempDir();
		var file = temp.Write("file.txt", "foo\nclass Foo");

		var result = await ExecuteAsync(GetFsEditTool(CreateModule(temp.Root)), new JsonObject
		{
			["path"] = "file.txt",
			["patches"] = new JsonArray { Patch("foo", "x"), Patch("class (\\w+)", "class C_$1", useRegex: true) }
		});

		Assert.Contains("File edited successfully", result.ResultContent);
		Assert.Equal("x\nclass C_Foo", File.ReadAllText(file));
	}

	[Fact]
	public async Task Patch_NotFound_OthersStillApplied()
	{
		using var temp = new TempDir();
		var file = temp.Write("file.txt", "foo");

		var result = await ExecuteAsync(GetFsEditTool(CreateModule(temp.Root)), new JsonObject
		{
			["path"] = "file.txt",
			["patches"] = new JsonArray { Patch("foo", "bar"), Patch("zzz", "x") }
		});

		Assert.Contains("File edited successfully", result.ResultContent);
		Assert.Contains("[NOT APPLIED PATCHES]", result.ResultContent);
		Assert.Contains("Patch #2 (match: \"zzz\"): no occurrences found.", result.ResultContent);
		Assert.Equal("bar", File.ReadAllText(file));
	}

	[Fact]
	public async Task Patch_NotFound_NoChangesMade()
	{
		using var temp = new TempDir();
		var file = temp.Write("file.txt", "abc");

		var result = await ExecuteAsync(GetFsEditTool(CreateModule(temp.Root)), new JsonObject
		{
			["path"] = "file.txt",
			["patches"] = new JsonArray { Patch("zzz", "x") }
		});

		Assert.Contains("No changes were made to the file.", result.ResultContent);
		Assert.Contains("Patch #1 (match: \"zzz\"): no occurrences found.", result.ResultContent);
		Assert.Equal("abc", File.ReadAllText(file));
	}

	[Fact]
	public async Task Patches_Empty_ReturnsError()
	{
		using var temp = new TempDir();
		temp.Write("file.txt", "abc");

		var result = await ExecuteAsync(GetFsEditTool(CreateModule(temp.Root)), new JsonObject
		{
			["path"] = "file.txt",
			["patches"] = new JsonArray()
		});

		Assert.Contains("'patches' parameter cannot be empty.", result.ResultContent);
	}

	[Fact]
	public async Task InvalidRegex_Reported_OthersStillApplied()
	{
		using var temp = new TempDir();
		var file = temp.Write("file.txt", "foo");

		var result = await ExecuteAsync(GetFsEditTool(CreateModule(temp.Root)), new JsonObject
		{
			["path"] = "file.txt",
			["patches"] = new JsonArray { Patch("(", "x", useRegex: true), Patch("foo", "bar") }
		});

		Assert.Contains("File edited successfully", result.ResultContent);
		Assert.Contains("[NOT APPLIED PATCHES]", result.ResultContent);
		Assert.Contains("Patch #1 (match: \"(\"): invalid pattern:", result.ResultContent);
		Assert.Equal("bar", File.ReadAllText(file));
	}

	[Fact]
	public async Task Match_TrailingNewline_Ignored()
	{
		using var temp = new TempDir();
		var file = temp.Write("file.txt", "foo\nbar");

		var result = await ExecuteAsync(GetFsEditTool(CreateModule(temp.Root)), new JsonObject
		{
			["path"] = "file.txt",
			["patches"] = new JsonArray { Patch("foo\n", "baz") }
		});

		Assert.Contains("File edited successfully", result.ResultContent);
		Assert.Equal("baz\nbar", File.ReadAllText(file));
	}

	[Fact]
	public async Task Replace_PreservesCrlfLineEndings()
	{
		using var temp = new TempDir();
		var file = temp.Write("file.txt", "foo\r\nbar");

		var result = await ExecuteAsync(GetFsEditTool(CreateModule(temp.Root)), new JsonObject
		{
			["path"] = "file.txt",
			["patches"] = new JsonArray { Patch("foo", "baz") }
		});

		Assert.Contains("File edited successfully", result.ResultContent);
		Assert.Equal("baz\r\nbar", File.ReadAllText(file));
	}

	[Fact]
	public async Task Replace_PythonCode_IndentedMatch_IndentedReplace_NoDoubledIndent()
	{
		using var temp = new TempDir();
		var file = temp.Write("file.py", """
			def foo():
			    if x:
			        do_something()
			    else:
			        do_other()
			""");

		var result = await ExecuteAsync(GetFsEditTool(CreateModule(temp.Root)), new JsonObject
		{
			["path"] = "file.py",
			["patches"] = new JsonArray { Patch("    if x:", "    if x and y:") }
		});

		Assert.Contains("File edited successfully", result.ResultContent);
		Assert.Equal("""
			def foo():
			    if x and y:
			        do_something()
			    else:
			        do_other()
			""", File.ReadAllText(file));
	}

	[Fact]
	public async Task Replace_PythonCode_MatchWithoutIndent_ReplaceWithIndent_UsesFileIndent()
	{
		using var temp = new TempDir();
		var file = temp.Write("file.py", """
			def foo():
			    if x:
			        do_other()
			""");

		var result = await ExecuteAsync(GetFsEditTool(CreateModule(temp.Root)), new JsonObject
		{
			["path"] = "file.py",
			["patches"] = new JsonArray { Patch("if x:", "if x and y:") }
		});

		Assert.Contains("File edited successfully", result.ResultContent);
		Assert.Equal("""
			def foo():
			    if x and y:
			        do_other()
			""", File.ReadAllText(file));
	}

	[Fact]
	public async Task Replace_PythonCode_MultilineBlock_LineByLine_KeepsIndentation()
	{
		using var temp = new TempDir();
		var file = temp.Write("file.py", """
			def foo():
			    if x:
			        do_something()
			    else:
			        do_other()
			""");

		var result = await ExecuteAsync(GetFsEditTool(CreateModule(temp.Root)), new JsonObject
		{
			["path"] = "file.py",
			["patches"] = new JsonArray
			{
				Patch("""
				    if x:
				        do_something()
				    """, """
				    if x and y:
				        do_something_else()
				    """)
			}
		});

		Assert.Contains("File edited successfully", result.ResultContent);
		Assert.Equal("""
			def foo():
			    if x and y:
			        do_something_else()
			    else:
			        do_other()
			""", File.ReadAllText(file));
	}

	[Theory]
	[InlineData("""
		if x:
		    for i in range(3):
		        print(i)
		""", """
		if x:
		    for i in range(3):
		        print(i * 2)
		""")]
	[InlineData("""
		 if x:
		     for i in range(3):
		         print(i)
		""", """
		 if x:
		     for i in range(3):
		         print(i * 2)
		""")]
	[InlineData("""
		  if x:
		      for i in range(3):
		          print(i)
		""", """
		  if x:
		      for i in range(3):
		          print(i * 2)
		""")]
	[InlineData("""
		   if x:
		       for i in range(3):
		           print(i)
		""", """
		   if x:
		       for i in range(3):
		           print(i * 2)
		""")]
	[InlineData("""
		    if x:
		        for i in range(3):
		            print(i)
		""", """
		    if x:
		        for i in range(3):
		            print(i * 2)
		""")]
	public async Task Replace_PythonCode_NestedBlock_IgnoresSameIndentation(string match, string text)
	{
		await TestReplaceAsync("""
			def foo():
			    if x:
			        for i in range(3):
			            print(i)
			""", """
			def foo():
			    if x:
			        for i in range(3):
			            print(i * 2)
			""", Patch(match, text));
	}

	[Fact]
	public async Task Replace_PythonCode_DeleteBlock_ReplacedBySingleLine()
	{
		using var temp = new TempDir();
		var file = temp.Write("file.py", """
			def foo():
			    if x:
			        do_something()
			    else:
			        do_other()
			""");

		var result = await ExecuteAsync(GetFsEditTool(CreateModule(temp.Root)), new JsonObject
		{
			["path"] = "file.py",
			["patches"] = new JsonArray
			{
				Patch("""
				    if x:
				        do_something()
				    else:
				        do_other()
				""", "    pass")
			}
		});

		Assert.Contains("File edited successfully", result.ResultContent);
		Assert.Equal("""
			def foo():
			    pass
			""", File.ReadAllText(file));
	}

	[Fact]
	public async Task Replace_PythonCode_TabsIndentation_NoDoubling()
	{
		using var temp = new TempDir();
		var file = temp.Write("file.py", "def foo():\n\tif x:\n\t\tdo_something()");

		var result = await ExecuteAsync(GetFsEditTool(CreateModule(temp.Root)), new JsonObject
		{
			["path"] = "file.py",
			["patches"] = new JsonArray { Patch("\tif x:", "\tif x and y:") }
		});

		Assert.Contains("File edited successfully", result.ResultContent);
		Assert.Equal("def foo():\n\tif x and y:\n\t\tdo_something()", File.ReadAllText(file));
	}

	[Fact]
	public async Task Replace_Plain_Multiline_MatchWithIndent_ReplacementWithoutIndent()
	{
		using var temp = new TempDir();
		var file = temp.Write("file.py", """
			def foo():
			    if x:
			        do_something()
			    else:
			        do_other()
			""");

		var result = await ExecuteAsync(GetFsEditTool(CreateModule(temp.Root)), new JsonObject
		{
			["path"] = "file.py",
			["patches"] = new JsonArray
			{
				Patch("""
				    if x:
				        do_something()
				    """, """
				    if z:
				        do_z()
				    """)
			}
		});

		Assert.Contains("File edited successfully", result.ResultContent);
		Assert.Equal("""
			def foo():
			    if z:
			        do_z()
			    else:
			        do_other()
			""", File.ReadAllText(file));
	}
	
	private async Task TestReplaceAsync(string original, string expected, params JsonObject[] patches)
	{
		using var temp = new TempDir();
		var file = temp.Write("file.txt", original);

		var result = await ExecuteAsync(GetFsEditTool(CreateModule(temp.Root)), new JsonObject
		{
			["path"] = "file.txt",
			["patches"] = new JsonArray([.. patches])
		});

		output.WriteLine(result.ResultContent);
		Assert.Contains("File edited successfully", result.ResultContent);
		Assert.Equal(expected, File.ReadAllText(file));
	}

	[Fact]
	public async Task Replace_CSharpCode_ComplexPatches()
	{
		await TestReplaceAsync("""
			namespace MyNamespace
			{
				public static class Program
				{
					public void Main()
					{
						var foo = new Foo
						{
							Property1 = "value1",
							Property2 = "value2"
						};
					}
				}
			}
			""", """
			namespace MyNamespace
			{
				public static class Program
				{
					public void Main(string[] args)
					{
						var bar = new Bar
						{
							Property3 = "value3",
							Property4 = "value4"
						};
					}
				}
			}
			""", Patch("""
			Main()
			""", """
			Main(string[] args)
			"""), Patch("""
			= new Foo
			{
				Property1 = "value1",
				Property2 = "value2
			""", """
			= new Bar
			{
				Property3 = "value3",
				Property4 = "value4
			"""), Patch("""
			var foo
			""", """
			var bar
			"""));
	}

	[Fact]
	public async Task Replace_CSharpCode_RealFailure1()
	{
		await TestReplaceAsync("""
			using System.Net;

			namespace LLMDesktopAssistant.Utils.Web
			{
				/// <summary>
				/// The result of a page fetch operation containing the raw HTML, HTTP status code, and response headers.
				/// </summary>
				public sealed record FetchResult(
					string Html,
					int? HttpStatus,
					IReadOnlyDictionary<string, string> Headers
				);

				/// <summary>
				/// Fetches web page content over HTTP with retries, cookie support, proxy support,
				/// SSL bypass, and response caching.
				/// </summary>
				public static class HtmlContentFetcher
				{
					private static readonly AsyncCache<string, FetchResult> _cache = new(
						FetchCoreAsync,
						slidingExpirationTime: TimeSpan.FromMinutes(15));
			""", """
			using System.Net;

			namespace LLMDesktopAssistant.Utils.Web
			{
			    /// <summary>
			    /// Fetches web page content over HTTP with retries, cookie support, proxy support,
			    /// SSL bypass, and response caching.
			    /// </summary>
			    public static class HtmlContentFetcher
				{
					private static readonly AsyncCache<string, FetchResult> _cache = new(
						FetchCoreAsync,
						slidingExpirationTime: TimeSpan.FromMinutes(15));
			""", Patch("""
			namespace LLMDesktopAssistant.Utils.Web
			{
			    /// <summary>
			    /// The result of a page fetch operation containing the raw HTML, HTTP status code, and response headers.
			    /// </summary>
			    public sealed record FetchResult(
			        string Html,
			        int? HttpStatus,
			        IReadOnlyDictionary<string, string> Headers
			    );
			  
			    /// <summary>
			    /// Fetches web page content over HTTP with retries, cookie support, proxy support,
			    /// SSL bypass, and response caching.
			    /// </summary>
			    public static class HtmlContentFetcher
			""", """
			namespace LLMDesktopAssistant.Utils.Web
			{
			    /// <summary>
			    /// Fetches web page content over HTTP with retries, cookie support, proxy support,
			    /// SSL bypass, and response caching.
			    /// </summary>
			    public static class HtmlContentFetcher
			"""));
	}

	[Theory]
	[InlineData("\t", "\n\t\t")]
	[InlineData("", "\n\t\t")]
	[InlineData("\t", "\n")]
	[InlineData("", "\n")]
	[InlineData("\t", "")]
	[InlineData("", "")]
	public async Task Replace_CSharpCode_RealFailure2(string indentation, string extraNewline)
	{
		await TestReplaceAsync("""
			using System.Net;

			namespace LLMDesktopAssistant.Utils.Web
			{
				/// <summary>
				/// The result of a page fetch operation containing the raw HTML, HTTP status code, and response headers.
				/// </summary>
				public sealed record FetchResult(
					string Html,$NEWLINE$
					int? HttpStatus,
					IReadOnlyDictionary<string, string> Headers
				);
			
				/// <summary>
				/// Fetches web page content over HTTP with retries, cookie support, proxy support,
				/// SSL bypass, and response caching.
				/// </summary>
				public static class HtmlContentFetcher
				{
					private static readonly AsyncCache<string, FetchResult> _cache = new(
						FetchCoreAsync,
						slidingExpirationTime: TimeSpan.FromMinutes(15));
			""".Replace("$NEWLINE$", extraNewline), """
			using System.Net;
			
			namespace LLMDesktopAssistant.Utils.Web
			{
				/// <summary>
				/// The result of a page fetch operation containing the raw HTML, HTTP status code, and response headers.
				/// </summary>
				public sealed record FetchResult(
					string Html,
					IReadOnlyDictionary<string, string> Headers
				);
			
				/// <summary>
				/// Fetches web page content over HTTP with retries, cookie support, proxy support,
				/// SSL bypass, and response caching.
				/// </summary>
				public static class HtmlContentFetcher
				{
					private static readonly AsyncCache<string, FetchResult> _cache = new(
						FetchCoreAsync,
						slidingExpirationTime: TimeSpan.FromMinutes(15));
			""", Patch($"""
			{indentation}	string Html,{extraNewline}
			{indentation}	int? HttpStatus,
			{indentation}	IReadOnlyDictionary<string, string> Headers
			{indentation});
			""", $"""
			{indentation}	string Html,
			{indentation}	IReadOnlyDictionary<string, string> Headers
			{indentation});
			"""));
	}

	[Fact]
	public void ArgumentSchema_ContainsPatchesArray()
	{
		var tool = GetFsEditTool(CreateModule(Path.GetTempPath()));

		var schema = tool.ArgumentSchema;
		var patchesSchema = schema["properties"]?["patches"] as JsonObject;
		Assert.NotNull(patchesSchema);
		Assert.Equal("array", (string?)patchesSchema!["type"]);

		output.WriteLine(schema.ToJsonString(new System.Text.Json.JsonSerializerOptions
		{
			WriteIndented = true,
			Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
		}));

		var items = patchesSchema["items"] as JsonObject;
		Assert.NotNull(items);
		var properties = items!["properties"] as JsonObject;
		Assert.NotNull(properties);
		Assert.NotNull(properties!["match"]);
		Assert.NotNull(properties["replace"]);
		Assert.NotNull(properties["useRegex"]);
		Assert.NotNull(properties["ignoreCase"]);
	}

	[Fact]
	public async Task Replace_CSharpCode_RealFailure_GetCategoriesFormatting()
	{
		await TestReplaceAsync("""
			namespace LLMDesktopAssistant.Tools;

			/// <summary>
			/// Maps <see cref="ToolBehaviour"/> flags to their corresponding <see cref="ToolBehaviourCategory"/>.
			/// </summary>
			public static class ToolBehaviourCategoryClassifier
			{
				/// <summary>
				/// Returns the category of the given tool behaviour flag,
				/// or <see cref="ToolBehaviourCategory.Unknown"/> for <see cref="ToolBehaviour.None"/>,
				/// <see cref="ToolBehaviour.All"/>, or composite values spanning multiple categories.
				/// </summary>
				/// <param name="behaviour">The tool behaviour flag to classify.</param>
				/// <returns>The corresponding category, or <see cref="ToolBehaviourCategory.Unknown"/> if the value cannot be classified.</returns>
				public static ToolBehaviourCategory GetCategory(ToolBehaviour behaviour)
				{
					return behaviour switch
					{
						ToolBehaviour.None or ToolBehaviour.All => ToolBehaviourCategory.Unknown,

						ToolBehaviour.FileDirectoryCreate or ToolBehaviour.FileRead or ToolBehaviour.FileEdit
							or ToolBehaviour.FileDelete or ToolBehaviour.DirectoryRead
							or ToolBehaviour.DirectoryEdit or ToolBehaviour.DirectoryDelete => ToolBehaviourCategory.Filesystem,

						ToolBehaviour.SemanticMemoryRead or ToolBehaviour.SemanticMemoryWrite
							or ToolBehaviour.SemanticMemoryDelete or ToolBehaviour.SemanticMemoryClear => ToolBehaviourCategory.SemanticMemory,

						ToolBehaviour.DatabaseRead or ToolBehaviour.DatabaseChange
							or ToolBehaviour.DatabaseCustomConnect => ToolBehaviourCategory.Database,

						ToolBehaviour.ReadSecrets or ToolBehaviour.AccessOutsideWorkdir or ToolBehaviour.WorkdirChange
							or ToolBehaviour.ClipboardWrite or ToolBehaviour.ClipboardRead => ToolBehaviourCategory.Security,

						ToolBehaviour.InternetAccess => ToolBehaviourCategory.Network,

						ToolBehaviour.LongRunningTask => ToolBehaviourCategory.Performance,

						ToolBehaviour.ExecuteExternalProcess or ToolBehaviour.PossiblyUnexpected
							or ToolBehaviour.RunTerminal => ToolBehaviourCategory.Execution,

						ToolBehaviour.UserInteraction => ToolBehaviourCategory.UserInteraction,

						ToolBehaviour.AgentExecution => ToolBehaviourCategory.Agents,

						ToolBehaviour.ScriptAccess => ToolBehaviourCategory.Meta,

						ToolBehaviour.MCP or ToolBehaviour.Meta or ToolBehaviour.AdHoc => ToolBehaviourCategory.Source,

						_ => ToolBehaviourCategory.Unknown
					};
				}

				public static ToolBehaviourCategory GetCategories(ToolBehaviour behaviourFlags)
				{

				}
			}
			""", """
			namespace LLMDesktopAssistant.Tools;

			/// <summary>
			/// Maps <see cref="ToolBehaviour"/> flags to their corresponding <see cref="ToolBehaviourCategory"/>.
			/// </summary>
			public static class ToolBehaviourCategoryClassifier
			{
				/// <summary>
				/// Returns the category of the given tool behaviour flag,
				/// or <see cref="ToolBehaviourCategory.Unknown"/> for <see cref="ToolBehaviour.None"/>,
				/// <see cref="ToolBehaviour.All"/>, or composite values spanning multiple categories.
				/// </summary>
				/// <param name="behaviour">The tool behaviour flag to classify.</param>
				/// <returns>The corresponding category, or <see cref="ToolBehaviourCategory.Unknown"/> if the value cannot be classified.</returns>
				public static ToolBehaviourCategory GetCategory(ToolBehaviour behaviour)
				{
					return behaviour switch
					{
						ToolBehaviour.None or ToolBehaviour.All => ToolBehaviourCategory.Unknown,

						ToolBehaviour.FileDirectoryCreate or ToolBehaviour.FileRead or ToolBehaviour.FileEdit
							or ToolBehaviour.FileDelete or ToolBehaviour.DirectoryRead
							or ToolBehaviour.DirectoryEdit or ToolBehaviour.DirectoryDelete => ToolBehaviourCategory.Filesystem,

						ToolBehaviour.SemanticMemoryRead or ToolBehaviour.SemanticMemoryWrite
							or ToolBehaviour.SemanticMemoryDelete or ToolBehaviour.SemanticMemoryClear => ToolBehaviourCategory.SemanticMemory,

						ToolBehaviour.DatabaseRead or ToolBehaviour.DatabaseChange
							or ToolBehaviour.DatabaseCustomConnect => ToolBehaviourCategory.Database,

						ToolBehaviour.ReadSecrets or ToolBehaviour.AccessOutsideWorkdir or ToolBehaviour.WorkdirChange
							or ToolBehaviour.ClipboardWrite or ToolBehaviour.ClipboardRead => ToolBehaviourCategory.Security,

						ToolBehaviour.InternetAccess => ToolBehaviourCategory.Network,

						ToolBehaviour.LongRunningTask => ToolBehaviourCategory.Performance,

						ToolBehaviour.ExecuteExternalProcess or ToolBehaviour.PossiblyUnexpected
							or ToolBehaviour.RunTerminal => ToolBehaviourCategory.Execution,

						ToolBehaviour.UserInteraction => ToolBehaviourCategory.UserInteraction,

						ToolBehaviour.AgentExecution => ToolBehaviourCategory.Agents,

						ToolBehaviour.ScriptAccess => ToolBehaviourCategory.Meta,

						ToolBehaviour.MCP or ToolBehaviour.Meta or ToolBehaviour.AdHoc => ToolBehaviourCategory.Source,

						_ => ToolBehaviourCategory.Unknown
					};
				}

				/// <summary>
				/// Returns the set of categories spanned by the given tool behaviour flags.
				/// </summary>
				/// <param name="behaviourFlags">The tool behaviour flags to classify.</param>
				/// <returns>
				/// A combination of <see cref="ToolBehaviourCategory"/> values covering all categories
				/// of the set flags, or <see cref="ToolBehaviourCategory.Unknown"/> when
				/// <paramref name="behaviourFlags"/> is <see cref="ToolBehaviour.None"/>.
				/// </returns>
				public static ToolBehaviourCategory GetCategories(ToolBehaviour behaviourFlags)
				{
					if (behaviourFlags is ToolBehaviour.None)
					{
						return ToolBehaviourCategory.Unknown;
					}

					var categories = ToolBehaviourCategory.Unknown;
					foreach (var flag in Enum.GetValues<ToolBehaviour>())
					{
						if (flag is ToolBehaviour.None or ToolBehaviour.All)
						{
							continue;
						}

						if (behaviourFlags.HasFlag(flag))
						{
							categories |= GetCategory(flag);
						}
					}
					return categories;
				}
			}
			""", Patch("""
			public static ToolBehaviourCategory GetCategories(ToolBehaviour behaviourFlags)
			{

			}
			""", """
			/// <summary>
			/// Returns the set of categories spanned by the given tool behaviour flags.
			/// </summary>
			/// <param name="behaviourFlags">The tool behaviour flags to classify.</param>
			/// <returns>
			/// A combination of <see cref="ToolBehaviourCategory"/> values covering all categories
			/// of the set flags, or <see cref="ToolBehaviourCategory.Unknown"/> when
			/// <paramref name="behaviourFlags"/> is <see cref="ToolBehaviour.None"/>.
			/// </returns>
			public static ToolBehaviourCategory GetCategories(ToolBehaviour behaviourFlags)
			{
				if (behaviourFlags is ToolBehaviour.None)
				{
					return ToolBehaviourCategory.Unknown;
				}

				var categories = ToolBehaviourCategory.Unknown;
				foreach (var flag in Enum.GetValues<ToolBehaviour>())
				{
					if (flag is ToolBehaviour.None or ToolBehaviour.All)
					{
						continue;
					}

					if (behaviourFlags.HasFlag(flag))
					{
						categories |= GetCategory(flag);
					}
				}
				return categories;
			}
			"""));
	}


	[Fact]
	public async Task Replace_PythonCode_NestedBlock_DeeperReplacement_PreservesRelativeNesting()
	{
		await TestReplaceAsync("""
			def process(items):
			    for item in items:
			        handle(item)
			""", """
			def process(items):
			    for item in items:
			        if item.valid:
			            handle(item)
			        else:
			            skip(item)
			""", Patch("""
			for item in items:
			    handle(item)
			""", """
			for item in items:
			    if item.valid:
			        handle(item)
			    else:
			        skip(item)
			"""));
	}

	[Fact]
	public async Task Replace_CSharpCode_ClosingBrace_AlignsWithFile()
	{
		await TestReplaceAsync("""
			public class Service
			{
				public void Run()
				{
					if (a)
					{
						DoA();
					}
				}
			}
			""", """
			public class Service
			{
				public void Run()
				{
					if (a && b)
					{
						DoA();
						DoB();
					}
				}
			}
			""", Patch("""
			if (a)
			{
				DoA();
			}
			""", """
			if (a && b)
			{
				DoA();
				DoB();
			}
			"""));
	}

	[Fact]
	public async Task Replace_CSharpCode_BlankLineInsideMatch_RemovedWithBlock()
	{
		await TestReplaceAsync("""
			public sealed record FetchResult(
				string Html,

				int? HttpStatus,
				IReadOnlyDictionary<string, string> Headers
			);
			""", """
			public sealed record FetchResult(
				string Html,
				IReadOnlyDictionary<string, string> Headers
			);
			""", Patch("""
			string Html,

			int? HttpStatus,
			""", """
			string Html,
			"""));
	}

	[Fact]
	public async Task Replace_PythonCode_MultilineBlock_ReplacedBySingleLine_NoDoubledIndent()
	{
		await TestReplaceAsync("""
			def foo():
			    if x:
			        do_something()
			    else:
			        do_other()
			""", """
			def foo():
			    pass
			""", Patch("""
			if x:
			    do_something()
			else:
			    do_other()
			""", """
			pass
			"""));
	}

	[Fact]
	public async Task Replace_CSharpCode_TrailingCommentAfterClosingBrace_Preserved()
	{
		await TestReplaceAsync("""
			private void Run()
			{
				if (x)
				{
					DoIt();
				} // end if
			}
			""", """
			private void Run()
			{
				if (x)
				{
					DoIt();
					Cleanup();
				} // end if
			}
			""", Patch("""
			if (x)
			{
				DoIt();
			}
			""", """
			if (x)
			{
				DoIt();
				Cleanup();
			}
			"""));
	}

	[Fact]
	public async Task Replace_CSharpCode_TrailingCommentInsideBlock_Preserved()
	{
		await TestReplaceAsync("""
			private void Run()
			{
				if (x)
				{
					DoIt(); // dangerous
				}
			}
			""", """
			private void Run()
			{
				if (x)
				{
					DoIt(); // dangerous
					Cleanup();
				}
			}
			""", Patch("""
			if (x)
			{
				DoIt();
			}
			""", """
			if (x)
			{
				DoIt();
				Cleanup();
			}
			"""));
	}

	[Fact]
	public async Task Replace_PythonCode_MultipleOccurrences_EachReIndentedToOwnLevel()
	{
		await TestReplaceAsync("""
			def a():
			    foo()
			    bar()

			def b():
			    if x:
			        foo()
			        bar()
			""", """
			def a():
			    foo()
			    bar()
			    baz()

			def b():
			    if x:
			        foo()
			        bar()
			        baz()
			""", Patch("""
			foo()
			bar()
			""", """
			foo()
			bar()
			baz()
			"""));
	}

	[Fact]
	public async Task Replace_Markdown_MixedInnerIndentation_SavedCorrectly()
	{
		await TestReplaceAsync("""
			## 🧾 Formatting & Scoping Rules

			- Leading indentation is **normalized during parsing**: every line loses up to `depth × TabSize` leading
			  whitespace, where `depth` is the block nesting level. You can write templates with comfortable
			  indentation - the common base indent is stripped and the output stays clean
			- Inner whitespace, line breaks and blank lines are preserved as written
			- Leading/trailing blank lines of blocks are trimmed; indentation before complex statements
			  (`@if`, `@foreach`, `@while`) is removed
			- `TabSize` (default `4`) on `LLTParser` controls how many columns one indent level takes during refinement
			- `@let` variables are **lexically scoped**
			- Loop variables do **not leak outside** their block
			- Nested `@if` and `@foreach` blocks behave predictably, matching C#‑like logical semantics
			- `TabSize` (default `4`) on `LLTParser` controls indentation handling during refinement
			""", """
			## 🧾 Formatting & Scoping Rules

			- Leading indentation is **normalized during parsing**: every line loses up to `depth × TabSize` leading
			  whitespace, where `depth` is the block nesting level. You can write templates with comfortable
			  indentation - the common base indent is stripped and the output stays clean
			- Inner whitespace, line breaks and blank lines are preserved as written
			- Leading/trailing blank lines of blocks are trimmed; indentation before complex statements
			  (`@if`, `@foreach`, `@while`) is removed
			- Lines containing only non-rendering constructs - `@/` and `@* *@` comments, `@let` declarations,
			  variable assignments - are removed entirely: the surrounding line breaks are trimmed so they
			  leave no blank lines in the output
			- `TabSize` (default `4`) on `LLTParser` controls how many columns one indent level takes during refinement
			- `@let` variables are **lexically scoped**
			- Loop variables do **not leak outside** their block
			- Nested `@if` and `@foreach` blocks behave predictably, matching C#‑like logical semantics
			- `TabSize` (default `4`) on `LLTParser` controls indentation handling during refinement
			""", Patch("""
			- Leading/trailing blank lines of blocks are trimmed; indentation before complex statements
			  (`@if`, `@foreach`, `@while`) is removed
			""", """
			- Leading/trailing blank lines of blocks are trimmed; indentation before complex statements
			  (`@if`, `@foreach`, `@while`) is removed
			- Lines containing only non-rendering constructs - `@/` and `@* *@` comments, `@let` declarations,
			  variable assignments - are removed entirely: the surrounding line breaks are trimmed so they
			  leave no blank lines in the output
			"""));
	}

	[Fact]
	public async Task Replace_CSharpCode_TextBeforeMatchOnFirstLine_Preserved()
	{
		await TestReplaceAsync("""
			class Program
			{
				static void Main()
				{
					var options = new Options
					{
						Mode = Mode.Fast,
						Retries = 3;
					};
					Run(options);
				}
			}
			""", """
			class Program
			{
				static void Main()
				{
					var options = new Options
					{
						Mode = Mode.Fast,
						Retries = 5,
						Timeout = TimeSpan.FromSeconds(30)
					};
					Run(options);
				}
			}
			""", Patch("""
			= new Options
			{
				Mode = Mode.Fast,
				Retries = 3;
			};
			""", """
			= new Options
			{
				Mode = Mode.Fast,
				Retries = 5,
				Timeout = TimeSpan.FromSeconds(30)
			};
			"""));
	}

	[Fact]
	public async Task Replace_CSharpCode_Record_BlankLineInside_ClosingParenAligned()
	{
		await TestReplaceAsync("""
			namespace App
			{
				public sealed record Result(
					string Name,
					int Count,

					string? Note
				);
			}
			""", """
			namespace App
			{
				public sealed record Result(
					string Name,
					int Count,
					string? Note,
					string? Extra
				);
			}
			""", Patch("""
			public sealed record Result(
				string Name,
				int Count,

				string? Note
			);
			""", """
			public sealed record Result(
				string Name,
				int Count,
				string? Note,
				string? Extra
			);
			"""));
	}

	[Fact]
	public async Task Replace_PythonCode_BlankLinesInReplacement_StayEmpty()
	{
		await TestReplaceAsync("""
			def foo():
			    if x:
			        a()
			        b()
			""", """
			def foo():
			    if x:
			        a()

			        c()
			""", Patch("""
			if x:
			    a()
			    b()
			""", """
			if x:
			    a()

			    c()
			"""));
	}

	[Fact]
	public async Task Replace_RealWorldPatch_FirstLineWithoutIndent_IndentationPreserved()
	{
		await TestReplaceAsync("""
						// The block's base indentation in the file: the leading whitespace of the
						// first non-empty prefix (the prefix may also contain text before the match)
						var fileBasePrefix = effectivePrefixes.FirstOrDefault(p => p.Length > 0) ?? "";
						var fileBase = fileBasePrefix[..fileBasePrefix.TakeWhile(c => c is ' ' or '\t').Count()];
						// The replacement's own base indentation: the common leading whitespace of
						// all but the last line, since the last line is aligned with the file's
""", """
						// The block's base indentation in the file: the leading whitespace of the first
						// matched line's prefix (the prefix may also contain text before the match)
						var fileBasePrefix = effectivePrefixes.Length > 0 ? effectivePrefixes[0] : "";
						var fileBase = fileBasePrefix[..fileBasePrefix.TakeWhile(c => c is ' ' or '\t').Count()];
						// The replacement's own base indentation: the common leading whitespace of
						// all but the last line, since the last line is aligned with the file's
""", Patch("""
// The block's base indentation in the file: the leading whitespace of the
						// first non-empty prefix (the prefix may also contain text before the match)
						var fileBasePrefix = effectivePrefixes.FirstOrDefault(p => p.Length > 0) ?? "";
""", """
// The block's base indentation in the file: the leading whitespace of the first
						// matched line's prefix (the prefix may also contain text before the match)
						var fileBasePrefix = effectivePrefixes.Length > 0 ? effectivePrefixes[0] : "";
"""));
	}

	[Fact]
	public async Task Replace_RealWorldPatch_InsertBranch_FirstLineWithoutIndent_IndentationPreserved()
	{
		await TestReplaceAsync("""
								var isClosingLine = ownIndent.Length <= replacementBase.Length
									|| !ownIndent.StartsWith(replacementBase);
								line = isClosingLine
									? effectivePrefixes[matchLines.Count - 1] + trimmedContent
									: fileBase + ownIndent[replacementBase.Length..] + trimmedContent;
								}
								else
								{
									// Keep the replacement's own relative indentation but
									// replace its base indentation with the file's
									var ownIndent = replacementLines[j][..(replacementLines[j].Length - trimmedContent.Length)];
									var relativeIndent = ownIndent.StartsWith(replacementBase)
										? ownIndent[replacementBase.Length..]
										: ownIndent;
									line = fileBase + relativeIndent + trimmedContent;
								}
""", """
								var isClosingLine = ownIndent.Length <= replacementBase.Length
									|| !ownIndent.StartsWith(replacementBase);
								line = isClosingLine
									? effectivePrefixes[matchLines.Count - 1] + trimmedContent
									: fileBase + ownIndent[replacementBase.Length..] + trimmedContent;
								}
								else if (j < matchLines.Count)
								{
									// Align the replacement line with the corresponding matched line:
									// the file's indentation plus the difference between the replacement's
									// and the match's own indentation. This tolerates patches whose first
									// line has no indentation while the rest do (a common LLM style)
									var ownIndent = replacementLines[j][..(replacementLines[j].Length - trimmedContent.Length)];
									var matchIndent = matchIndents[j];
									var relativeIndent = ownIndent.StartsWith(matchIndent)
										? ownIndent[matchIndent.Length..]
										: ownIndent;
									var fileIndent = effectivePrefixes[j][..effectivePrefixes[j].TakeWhile(c => c is ' ' or '\t').Count()];
									line = fileIndent + relativeIndent + trimmedContent;
								}
								else
								{
									// Keep the replacement's own relative indentation but
									// replace its base indentation with the file's
									var ownIndent = replacementLines[j][..(replacementLines[j].Length - trimmedContent.Length)];
									var relativeIndent = ownIndent.StartsWith(replacementBase)
										? ownIndent[replacementBase.Length..]
										: ownIndent;
									line = fileBase + relativeIndent + trimmedContent;
								}
""", Patch("""
								line = isClosingLine
									? effectivePrefixes[matchLines.Count - 1] + trimmedContent
									: fileBase + ownIndent[replacementBase.Length..] + trimmedContent;
								}
								else
								{
									// Keep the replacement's own relative indentation but
									// replace its base indentation with the file's
""", """
								line = isClosingLine
									? effectivePrefixes[matchLines.Count - 1] + trimmedContent
									: fileBase + ownIndent[replacementBase.Length..] + trimmedContent;
								}
								else if (j < matchLines.Count)
								{
									// Align the replacement line with the corresponding matched line:
									// the file's indentation plus the difference between the replacement's
									// and the match's own indentation. This tolerates patches whose first
									// line has no indentation while the rest do (a common LLM style)
									var ownIndent = replacementLines[j][..(replacementLines[j].Length - trimmedContent.Length)];
									var matchIndent = matchIndents[j];
									var relativeIndent = ownIndent.StartsWith(matchIndent)
										? ownIndent[matchIndent.Length..]
										: ownIndent;
									var fileIndent = effectivePrefixes[j][..effectivePrefixes[j].TakeWhile(c => c is ' ' or '\t').Count()];
									line = fileIndent + relativeIndent + trimmedContent;
								}
								else
								{
									// Keep the replacement's own relative indentation but
									// replace its base indentation with the file's
"""));
	}

	[Fact]
	public async Task Replace_RealWorldPatch_1()
	{
		await TestReplaceAsync("""
using LLMDesktopAssistant.Localization;
using LLMDesktopAssistant.MCP;
using LLMDesktopAssistant.MVVM.Debug;
using LLMDesktopAssistant.Prompting;
using LLMDesktopAssistant.Services;

namespace LLMDesktopAssistant.MVVM
{
	public class MainViewModel : ViewModelBase
	{
		public ChatManagerViewModel ChatManager { get; }
		public MCPManagerViewModel MCPManager { get; }
		public PromptManagerViewModel PromptManager { get; }
		public AgentTaskDispatcherViewModel AgentTaskDispatcher { get; }
		public HelpViewModel Help { get; }
		public ApplicationSettingsViewModel ApplicationSettings { get; }

		{
			Help = new HelpViewModel(ServiceRegistry.Provider.GetRequiredService<HelpDocumentStore>());
			BottomSidebarItems.Add(new MainViewModelSidebarItemViewModel
			{
				Icon = MaterialIconKind.BookOpen,
				Title = Locale.GetKey("tab.title.help"),
				Content = Help
			});

			ApplicationSettings = new ApplicationSettingsViewModel();
			BottomSidebarItems.Add(new MainViewModelSidebarItemViewModel
			{
				Icon = MaterialIconKind.Cog,
				Title = Locale.GetKey("tab.title.settings"),
				Content = ApplicationSettings
			});
			
			SelectedTopSidebarItem = TopSidebarItems[0];
		}
	}
}

""", """
using LLMDesktopAssistant.Localization;
using LLMDesktopAssistant.MCP;
using LLMDesktopAssistant.MVVM.Debug;
using LLMDesktopAssistant.Prompting;
using LLMDesktopAssistant.Services;

namespace LLMDesktopAssistant.MVVM
{
	public class MainViewModel : ViewModelBase
	{
		public ChatManagerViewModel ChatManager { get; }
		public MCPManagerViewModel MCPManager { get; }
		public PromptManagerViewModel PromptManager { get; }
		public AgentTaskDispatcherViewModel AgentTaskDispatcher { get; }
		public HelpViewModel Help { get; }
		public ApplicationSettingsViewModel ApplicationSettings { get; }

#if DEBUG
		public DebugPagesViewModel Debug { get; }
#endif

		{
			Help = new HelpViewModel(ServiceRegistry.Provider.GetRequiredService<HelpDocumentStore>());
			BottomSidebarItems.Add(new MainViewModelSidebarItemViewModel
			{
				Icon = MaterialIconKind.BookOpen,
				Title = Locale.GetKey("tab.title.help"),
				Content = Help
			});

			ApplicationSettings = new ApplicationSettingsViewModel();
			BottomSidebarItems.Add(new MainViewModelSidebarItemViewModel
			{
				Icon = MaterialIconKind.Cog,
				Title = Locale.GetKey("tab.title.settings"),
				Content = ApplicationSettings
			});

#if DEBUG
			Debug = new DebugPagesViewModel();
			BottomSidebarItems.Add(new MainViewModelSidebarItemViewModel
			{
				Icon = MaterialIconKind.Bug,
				Title = Locale.GetKey("tab.title.debug"),
				Content = Debug
			});
#endif

			SelectedTopSidebarItem = TopSidebarItems[0];
		}
	}
}

""", Patch("""
using LLMDesktopAssistant.MCP;
using LLMDesktopAssistant.Prompting;
""", """
using LLMDesktopAssistant.MCP;
using LLMDesktopAssistant.MVVM.Debug;
using LLMDesktopAssistant.Prompting;
"""), Patch("""
		public HelpViewModel Help { get; }
		public ApplicationSettingsViewModel ApplicationSettings { get; }
""", """
		public HelpViewModel Help { get; }
		public ApplicationSettingsViewModel ApplicationSettings { get; }

#if DEBUG
		public DebugPagesViewModel Debug { get; }
#endif
"""), Patch("""
			ApplicationSettings = new ApplicationSettingsViewModel();
			BottomSidebarItems.Add(new MainViewModelSidebarItemViewModel
			{
				Icon = MaterialIconKind.Cog,
				Title = Locale.GetKey("tab.title.settings"),
				Content = ApplicationSettings
			});
			
			SelectedTopSidebarItem = TopSidebarItems[0];
""", """
			ApplicationSettings = new ApplicationSettingsViewModel();
			BottomSidebarItems.Add(new MainViewModelSidebarItemViewModel
			{
				Icon = MaterialIconKind.Cog,
				Title = Locale.GetKey("tab.title.settings"),
				Content = ApplicationSettings
			});

#if DEBUG
			Debug = new DebugPagesViewModel();
			BottomSidebarItems.Add(new MainViewModelSidebarItemViewModel
			{
				Icon = MaterialIconKind.Bug,
				Title = Locale.GetKey("tab.title.debug"),
				Content = Debug
			});
#endif

			SelectedTopSidebarItem = TopSidebarItems[0];
"""));
	}

	[Fact]
	public async Task Replace_RealWorldPatch_2()
	{
		await TestReplaceAsync("""
using LLMDesktopAssistant.Localization;
using LLMDesktopAssistant.MCP;
using LLMDesktopAssistant.MVVM.Debug;
using LLMDesktopAssistant.Prompting;

namespace LLMDesktopAssistant.Tests
{
	public class SomeTests
	{
		private static TrackedContext CreateTracked() => new();
	
		[Fact]
		public void ReplaceValue_UntracksOldAndTracksNew()
		{
			var ctx = new TrackedItemContext();
			var oldItem = new ObservableItem();
			var newItem = new ObservableItem();
			ctx.Dict.Add("item", oldItem);
			ctx.Dict["item"] = newItem; // Replace
			ctx.Changed = 0;
		
			oldItem.Value = 1;
			Assert.Equal(0, ctx.Changed);
		
			newItem.Value = 2;
			Assert.Equal(1, ctx.Changed);
		}
		
		// ========== Known limitation ==========
	
		[Fact]
		public void DeepChanges_InsideValues_AreNotTracked()
		{
			// ChangeTracker tracks collection items as reference-typed objects, but dictionary
			// items are KeyValuePair<TKey, TValue> value types, so objects stored as values
			// are not tracked deeply. Collection-level changes are still tracked.
			var dict = new ObservableDictionary<string, ObservableItem>();
			var item = new ObservableItem();
			dict.Add("item", item);
			var changed = 0;
			using var tracker = new ChangeTracker(dict, () => changed++);
		
			item.Value = 42;
		
			Assert.Equal(0, changed);
		}
	}
}

""", """
using LLMDesktopAssistant.Localization;
using LLMDesktopAssistant.MCP;
using LLMDesktopAssistant.MVVM.Debug;
using LLMDesktopAssistant.Prompting;

namespace LLMDesktopAssistant.Tests
{
	public class SomeTests
	{
		private static TrackedContext CreateTracked() => new();

		private sealed class TrackedItemContext
		{
			public ObservableDictionary<string, ObservableItem> Dict { get; } = new();

			public ChangeTracker Tracker { get; }

			public int Changed;

			public TrackedItemContext()
			{
				Tracker = new ChangeTracker(Dict, () => Changed++);
			}
		}
	
		[Fact]
		public void ReplaceValue_UntracksOldAndTracksNew()
		{
			var ctx = new TrackedItemContext();
			var oldItem = new ObservableItem();
			var newItem = new ObservableItem();
			ctx.Dict.Add("item", oldItem);
			ctx.Dict["item"] = newItem; // Replace
			ctx.Changed = 0;
		
			oldItem.Value = 1;
			Assert.Equal(0, ctx.Changed);
		
			newItem.Value = 2;
			Assert.Equal(1, ctx.Changed);
		}
		
		// ========== Deep tracking of values ==========

		[Fact]
		public void DeepChanges_InsideValues_AreTracked()
		{
			var ctx = new TrackedItemContext();
			var item = new ObservableItem();
			ctx.Dict.Add("item", item);
			ctx.Changed = 0;

			item.Value = 42;

			Assert.Equal(1, ctx.Changed);
		}

		[Fact]
		public void ReplaceValue_UntracksOldAndTracksNew()
		{
			var ctx = new TrackedItemContext();
			var oldItem = new ObservableItem();
			var newItem = new ObservableItem();
			ctx.Dict.Add("item", oldItem);
			ctx.Dict["item"] = newItem; // Replace
			ctx.Changed = 0;

			oldItem.Value = 1;
			Assert.Equal(0, ctx.Changed);

			newItem.Value = 2;
			Assert.Equal(1, ctx.Changed);
		}

		[Fact]
		public void Remove_UntracksValue()
		{
			var ctx = new TrackedItemContext();
			var item = new ObservableItem();
			ctx.Dict.Add("item", item);
			ctx.Dict.Remove("item");
			ctx.Changed = 0;

			item.Value = 1;

			Assert.Equal(0, ctx.Changed);
		}
	}
}

""", Patch("""
	private static TrackedContext CreateTracked() => new();
""", """
	private static TrackedContext CreateTracked() => new();
	
	private sealed class TrackedItemContext
	{
		public ObservableDictionary<string, ObservableItem> Dict { get; } = new();
	
		public ChangeTracker Tracker { get; }
	
		public int Changed;
	
		public TrackedItemContext()
		{
			Tracker = new ChangeTracker(Dict, () => Changed++);
		}
	}
"""), Patch("""
	// ========== Known limitation ==========
	
	[Fact]
	public void DeepChanges_InsideValues_AreNotTracked()
	{
		// ChangeTracker tracks collection items as reference-typed objects, but dictionary
		// items are KeyValuePair<TKey, TValue> value types, so objects stored as values
		// are not tracked deeply. Collection-level changes are still tracked.
		var dict = new ObservableDictionary<string, ObservableItem>();
		var item = new ObservableItem();
		dict.Add("item", item);
		var changed = 0;
		using var tracker = new ChangeTracker(dict, () => changed++);
	
		item.Value = 42;
	
		Assert.Equal(0, changed);
	}
""", """
	// ========== Deep tracking of values ==========
	
	[Fact]
	public void DeepChanges_InsideValues_AreTracked()
	{
		var ctx = new TrackedItemContext();
		var item = new ObservableItem();
		ctx.Dict.Add("item", item);
		ctx.Changed = 0;
	
		item.Value = 42;
	
		Assert.Equal(1, ctx.Changed);
	}
	
	[Fact]
	public void ReplaceValue_UntracksOldAndTracksNew()
	{
		var ctx = new TrackedItemContext();
		var oldItem = new ObservableItem();
		var newItem = new ObservableItem();
		ctx.Dict.Add("item", oldItem);
		ctx.Dict["item"] = newItem; // Replace
		ctx.Changed = 0;
	
		oldItem.Value = 1;
		Assert.Equal(0, ctx.Changed);
	
		newItem.Value = 2;
		Assert.Equal(1, ctx.Changed);
	}
	
	[Fact]
	public void Remove_UntracksValue()
	{
		var ctx = new TrackedItemContext();
		var item = new ObservableItem();
		ctx.Dict.Add("item", item);
		ctx.Dict.Remove("item");
		ctx.Changed = 0;
	
		item.Value = 1;
	
		Assert.Equal(0, ctx.Changed);
	}
"""));
	}

}
