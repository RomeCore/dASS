using System.Text.Json.Nodes;
using LLMDesktopAssistant.LLM.Services;
using LLMDesktopAssistant.LLM.Settings;
using LLMDesktopAssistant.Tools;
using LLMDesktopAssistant.Tools.Execution.Domain;
using LLMDesktopAssistant.Tools.Execution.Executors;
using LLMDesktopAssistant.Tools.Specifiers;
using Microsoft.Extensions.DependencyInjection;

namespace LLMDesktopAssistant.Tests.Tools.Execution;

/// <summary>
/// The generated analyzer is what makes a rule work on a tool with no hand-written analyzer: it reads
/// the arguments the descriptor describes and matches each in the dialect that argument declares.
/// </summary>
public class GeneratedSpecifierAnalyzerTests
{
	private static readonly ToolArgumentsDescriptor PathArguments =
		ToolMethodBinder.DescribeArguments((Func<string, bool, string?, string>)Edit);

	private static readonly ToolArgumentsDescriptor TextArguments =
		ToolMethodBinder.DescribeArguments((Func<string, bool, string>)Run);

	private static readonly IServiceProvider Services = new ServiceCollection()
		.AddSingleton<IWorkingDirectoryAccessService>(new StubWorkingDirectory("/work"))
		.BuildServiceProvider();

	private static string Edit(
		[MainArgument] [PathArgument(File = true, Directory = false)] string path,
		bool overwrite = false,
		string? note = null) => path;

	private static string Run([MainArgument] string command, bool runTerminal = false) => command;

	private static string Arrays(string[] items) => string.Join(',', items);

	/// <summary>
	/// Analyses a rule the way the policy stage does: parsed with the tool's specifier names, otherwise
	/// <c>runTerminal:true</c> would be a plain literal instead of a parameter reference.
	/// </summary>
	private static SpecifierMatchResult Analyze(ToolArgumentsDescriptor arguments, string pattern,
		JsonObject args) =>
		GeneratedSpecifierAnalyzer.Analyze(
			SpecifierParser.Parse(pattern, arguments.SpecifierParameters.Select(argument => argument.Name)),
			args, arguments, Services);

	[Fact]
	public void PathMainArgument_IsMatchedAsAPathPattern()
	{
		var args = new JsonObject { ["path"] = "src/Lib/File.cs" };

		Assert.Equal(SpecifierMatchResult.FullMatch, Analyze(PathArguments, "**/*.cs", args));
		Assert.Equal(SpecifierMatchResult.NoMatch, Analyze(PathArguments, "**/*.md", args));
	}

	[Fact]
	public void TextMainArgument_IsMatchedAsProse()
	{
		var args = new JsonObject { ["command"] = "git status --short" };

		Assert.Equal(SpecifierMatchResult.FullMatch, Analyze(TextArguments, "git status:*", args));
		Assert.Equal(SpecifierMatchResult.FullMatch, Analyze(TextArguments, "git *", args));
	}

	[Fact]
	public void NamedArgument_IsMatchedByName()
	{
		var args = new JsonObject { ["command"] = "git status", ["runTerminal"] = true };

		Assert.Equal(SpecifierMatchResult.FullMatch, Analyze(TextArguments, "git * && runTerminal:true", args));
		Assert.Equal(SpecifierMatchResult.NoMatch, Analyze(TextArguments, "npm * || runTerminal:false", args));
	}

	[Fact]
	public void OmittedArgument_FallsBackToTheAuthoredDefault()
	{
		// The tool receives 'false' when the model leaves the flag out; the rule sees what the tool sees.
		var args = new JsonObject { ["command"] = "git status" };

		Assert.Equal(SpecifierMatchResult.FullMatch, Analyze(TextArguments, "git * && runTerminal:false", args));
		Assert.Equal(SpecifierMatchResult.NoMatch, Analyze(TextArguments, "git * && runTerminal:true", args));
	}

	[Fact]
	public void ArgumentsWithNoSpecifierTargets_HaveNoAnalyzer()
	{
		var arguments = ToolMethodBinder.DescribeArguments((Func<string[], string>)Arrays);

		Assert.False(arguments.HasSpecifierTargets);
		Assert.True(TextArguments.HasSpecifierTargets);
	}

	/// <summary>
	/// A working directory for the path targets: the analyzer asks the services, as it does in production.
	/// </summary>
	private sealed class StubWorkingDirectory(string root) : IWorkingDirectoryAccessService
	{
		public string AccessPath(string path, DirectoryAccessMode mode) => Path.Combine(root, path);

		public string CheckedAccessPath(string path, DirectoryAccessMode mode, out bool isAccessed)
		{
			isAccessed = true;
			return Path.Combine(root, path);
		}

		public string GetWorkingDirectory() => root;

		public string? TryAccessPath(string path, DirectoryAccessMode mode) => Path.Combine(root, path);
	}
}
