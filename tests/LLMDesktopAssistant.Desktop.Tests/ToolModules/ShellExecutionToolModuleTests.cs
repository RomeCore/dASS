using System.Text.Json.Nodes;
using LLMDesktopAssistant.Addons;
using LLMDesktopAssistant.Addons.Loading;
using LLMDesktopAssistant.Addons.Management;
using LLMDesktopAssistant.Desktop.Execution;
using LLMDesktopAssistant.Desktop.ToolModules;
using LLMDesktopAssistant.LLM.Services;
using LLMDesktopAssistant.LLM.Services.Prompting;
using LLMDesktopAssistant.LLM.Settings;
using LLMDesktopAssistant.Prompting.Skills;
using LLMDesktopAssistant.Tools;
using LLMDesktopAssistant.Tools.Specifiers;
using LLMDesktopAssistant.Utils;

namespace LLMDesktopAssistant.Desktop.Tests.ToolModules;

public class ShellExecutionToolModuleTests
{
	private sealed class StubProcessLauncher : IProcessLauncher
	{
		public ProcessDescriptor Launch(ProcessLaunchParameters parameters, CancellationToken cancellationToken = default)
			=> throw new NotSupportedException();
	}

	private sealed class StubChatSettingsService : IChatSettingsService
	{
		public ChatSettings Settings { get; private set; } = new();

		public event EventHandler? SettingsChanged;

		public void LoadFromProfile(string profileName = "-default")
		{
		}

		public void SetSettings(ChatSettings settings)
		{
			Settings = settings;
			SettingsChanged?.Invoke(this, EventArgs.Empty);
		}
	}

	private sealed class StubSkillAccessor : IAddonAccessor<SkillInfo>
	{
		public ReadOnlyObservableCollection<SkillInfo> Addons => new(new List<SkillInfo>());
	}

	private sealed class StubAddonInvalidator : IAddonManagerInvalidator
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

	private static ShellExecutionToolModule CreateModule()
	{
		var wdAccess = new WorkingDirectoryAccessService(new StubChatSettingsService(), new StubSkillAccessor());
		return new ShellExecutionToolModule(wdAccess, new StubProcessLauncher(), new StubAddonInvalidator());
	}

	private static string MainArgumentName(string toolName) => toolName switch
	{
		"shell-bash" => "bash",
		"shell-batch" => "batch",
		"shell-powershell" => "powershell",
		_ => throw new ArgumentOutOfRangeException(nameof(toolName))
	};

	private static ToolInfo GetTool(ShellExecutionToolModule module, string toolName)
	{
		return module.GetTools().Single(t => t.Name == toolName);
	}

	private static SpecifierMatchResult Analyze(
		ShellExecutionToolModule module, string toolName, string specifierPattern, string command,
		bool runTerminal = false, bool wait = true)
	{
		var tool = GetTool(module, toolName);
		var specifier = SpecifierParser.Parse(specifierPattern, tool.SpecifierParameters);
		var args = new JsonObject
		{
			[MainArgumentName(toolName)] = command,
			["runTerminal"] = runTerminal,
			["wait"] = wait
		};
		var context = ToolExecutionContext.CreateDummy(tool, null, null);
		return tool.SpecifierAnalyzer!(specifier, args, context);
	}

	// ──────────────────────────── registration ────────────────────────────

	[Theory]
	[InlineData("shell-bash")]
	[InlineData("shell-batch")]
	[InlineData("shell-powershell")]
	public void Module_RegistersShellTools_WithSpecifierAnalyzerAndParameters(string toolName)
	{
		var module = CreateModule();

		var tool = GetTool(module, toolName);

		Assert.NotNull(tool.SpecifierAnalyzer);
		Assert.Equal(["runTerminal", "wait"], tool.SpecifierParameters);
	}

	// ──────────────────────────── matching basics ────────────────────────────

	[Theory]
	[InlineData("shell-bash")]
	[InlineData("shell-batch")]
	[InlineData("shell-powershell")]
	public void Analyze_SingleCommandFullMatch_AllShells(string toolName)
	{
		var result = Analyze(CreateModule(), toolName, "git status:*", "git status --short");

		Assert.Equal(SpecifierMatchResult.FullMatch, result);
	}

	[Theory]
	[InlineData("shell-bash")]
	[InlineData("shell-batch")]
	[InlineData("shell-powershell")]
	public void Analyze_NoMatch_DifferentCommand(string toolName)
	{
		var result = Analyze(CreateModule(), toolName, "npm *", "git status");

		Assert.Equal(SpecifierMatchResult.NoMatch, result);
	}

	// ──────────────────────────── compound commands ────────────────────────────

	[Fact]
	public void Analyze_PowerShellCompoundCommands_OrPatternFullMatch()
	{
		var result = Analyze(CreateModule(), "shell-powershell",
			"git status:* || npm install:*", "git status; npm install");

		Assert.Equal(SpecifierMatchResult.FullMatch, result);
	}

	[Fact]
	public void Analyze_PowerShellCompoundCommands_SinglePatternPartialMatch()
	{
		var result = Analyze(CreateModule(), "shell-powershell", "git status:*", "git status; npm install");

		Assert.Equal(SpecifierMatchResult.PartialMatch, result);
	}

	[Fact]
	public void Analyze_PowerShellDoubleAmpersand_OrPatternFullMatch()
	{
		var result = Analyze(CreateModule(), "shell-powershell",
			"git add .:* || git commit *", "git add . && git commit -m \"fix\"");

		Assert.Equal(SpecifierMatchResult.FullMatch, result);
	}

	[Fact]
	public void Analyze_PowerShellSeparatorInsideQuotes_NotSplit()
	{
		var result = Analyze(CreateModule(), "shell-powershell",
			"git commit * || git push:*", "git commit -m \"fix; done\" && git push");

		Assert.Equal(SpecifierMatchResult.FullMatch, result);
	}

	[Fact]
	public void Analyze_PowerShellPipe_NotASeparator()
	{
		var result = Analyze(CreateModule(), "shell-powershell", "git log | grep fix:*", "git log | grep fix");

		Assert.Equal(SpecifierMatchResult.FullMatch, result);
	}

	[Fact]
	public void Analyze_BatchSingleAmpersand_SplitsCommands()
	{
		var result = Analyze(CreateModule(), "shell-batch", "echo b:*", "echo a & echo b");

		Assert.Equal(SpecifierMatchResult.PartialMatch, result);
	}

	[Fact]
	public void Analyze_PowerShellSingleAmpersand_NotSplit()
	{
		var result = Analyze(CreateModule(), "shell-powershell", "echo b:*", "echo a & echo b");

		Assert.Equal(SpecifierMatchResult.NoMatch, result);
	}

	// ──────────────────────────── specifier parameters ────────────────────────────

	[Fact]
	public void Analyze_ParameterMatch_RunTerminalTrue_FullMatch()
	{
		var result = Analyze(CreateModule(), "shell-powershell",
			"git status:* && runTerminal:true", "git status", runTerminal: true);

		Assert.Equal(SpecifierMatchResult.FullMatch, result);
	}

	[Fact]
	public void Analyze_ParameterMismatch_RunTerminalFalse_NoMatch()
	{
		var result = Analyze(CreateModule(), "shell-powershell",
			"git status:* && runTerminal:true", "git status", runTerminal: false);

		Assert.Equal(SpecifierMatchResult.NoMatch, result);
	}

	[Fact]
	public void Analyze_ParameterMatch_WaitTrue_FullMatch()
	{
		var result = Analyze(CreateModule(), "shell-powershell",
			"git status:* && wait:true", "git status", wait: true);

		Assert.Equal(SpecifierMatchResult.FullMatch, result);
	}

	[Fact]
	public void Analyze_UnknownParameterName_TreatedAsLiteral()
	{
		var result = Analyze(CreateModule(), "shell-powershell",
			"runTerminal:true:*", "git status");

		Assert.Equal(SpecifierMatchResult.NoMatch, result);
	}

	// ──────────────────────────── engine integration ────────────────────────────

	[Fact]
	public void Evaluate_AllowRuleFullMatch_AllowVerdict()
	{
		var module = CreateModule();
		var tool = GetTool(module, "shell-powershell");
		var rule = new ToolSpecifierRule { Pattern = "git status:* || git log:*", Decision = SpecifierDecision.Allow };
		var args = new JsonObject { ["powershell"] = "git status; git log --oneline", ["runTerminal"] = false, ["wait"] = true };
		var context = ToolExecutionContext.CreateDummy(tool, null, null);

		var result = SpecifierEngine.Evaluate([rule], tool.SpecifierAnalyzer!, args, context,
			tool.SpecifierParameters, SpecifierAggregationMode.Sequential);

		Assert.Equal(SpecifierVerdict.Allow, result.Verdict);
	}

	[Fact]
	public void Evaluate_DenyRulePartialMatch_DenyVerdict()
	{
		var module = CreateModule();
		var tool = GetTool(module, "shell-powershell");
		var rule = new ToolSpecifierRule { Pattern = "rm *", Decision = SpecifierDecision.Deny };
		var args = new JsonObject { ["powershell"] = "rm -rf node_modules; git status", ["runTerminal"] = false, ["wait"] = true };
		var context = ToolExecutionContext.CreateDummy(tool, null, null);

		var result = SpecifierEngine.Evaluate([rule], tool.SpecifierAnalyzer!, args, context,
			tool.SpecifierParameters, SpecifierAggregationMode.Sequential);

		Assert.Equal(SpecifierVerdict.Deny, result.Verdict);
	}

	[Fact]
	public void Evaluate_AllowRulePartialMatch_NoneVerdict()
	{
		var module = CreateModule();
		var tool = GetTool(module, "shell-powershell");
		var rule = new ToolSpecifierRule { Pattern = "git status:*", Decision = SpecifierDecision.Allow };
		var args = new JsonObject { ["powershell"] = "git status; npm install", ["runTerminal"] = false, ["wait"] = true };
		var context = ToolExecutionContext.CreateDummy(tool, null, null);

		var result = SpecifierEngine.Evaluate([rule], tool.SpecifierAnalyzer!, args, context,
			tool.SpecifierParameters, SpecifierAggregationMode.Sequential);

		Assert.Equal(SpecifierVerdict.None, result.Verdict);
	}
}
