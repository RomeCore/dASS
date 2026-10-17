using System.Text.Json.Nodes;
using LLMDesktopAssistant.Tests.Tools;
using LLMDesktopAssistant.Tools;
using LLMDesktopAssistant.Tools.Execution.Domain;
using LLMDesktopAssistant.Tools.Specifiers;

namespace LLMDesktopAssistant.Tests.Specifiers;

public class SpecifierEngineTests
{
	private static IToolExecutionContext CreateContext()
	{
		var tool = ToolInfo.Create(new ToolInitializationInfo
		{
			Name = "test",
			Description = "test",
			Executor = (JsonNode? _, IToolExecutionContext _, CancellationToken _) =>
				Task.FromResult(ToolExecutionResult.Succeeded)
		});
		return ToolTestContext.Create(tool).Context;
	}

	private static ToolSpecifierRule Change(string pattern, SpecifierDecision decision) => new()
	{
		Pattern = pattern,
		Decision = decision
	};

	private static IToolSpecifierAnalyzer Analyzer(params string[] parts)
	{
		return new AnalyzerStub((specifier, _, _) => SpecifierMatcher.Match(specifier, parts, []));
	}

	private static IToolSpecifierAnalyzer ParameterAnalyzer(
		string[] parts, params (string Name, string Value)[] parameters)
	{
		return new AnalyzerStub((specifier, _, _) => SpecifierMatcher.Match(specifier, parts,
			parameters.Select(p => new KeyValuePair<string, string>(p.Name, p.Value))));
	}

	private sealed class AnalyzerStub(
		Func<Specifier, JsonNode?, IToolExecutionContext, SpecifierMatchResult> match) : IToolSpecifierAnalyzer
	{
		public SpecifierMatchResult Analyze(Specifier specifier, JsonNode? args, IToolExecutionContext ctx) =>
			match(specifier, args, ctx);
	}

	// ──────────────────────────── Evaluate: basics ────────────────────────────

	[Fact]
	public void Evaluate_NoSpecifiers_None()
	{
		var result = SpecifierEngine.Evaluate([], Analyzer("git status"), null, CreateContext(), [], SpecifierAggregationMode.Sequential);

		Assert.Equal(SpecifierVerdict.None, result.Verdict);
	}

	[Fact]
	public void Evaluate_NoMatch_None()
	{
		var result = SpecifierEngine.Evaluate([Change("git status:*", SpecifierDecision.Allow)],
			Analyzer("npm install"), null, CreateContext(), [], SpecifierAggregationMode.Sequential);

		Assert.Equal(SpecifierVerdict.None, result.Verdict);
	}

	[Fact]
	public void Evaluate_AllowWithFullMatch_Allow()
	{
		var result = SpecifierEngine.Evaluate([Change("git status:*", SpecifierDecision.Allow)],
			Analyzer("git status --short"), null, CreateContext(), [], SpecifierAggregationMode.Sequential);

		Assert.Equal(SpecifierVerdict.Allow, result.Verdict);
		Assert.Contains("git status:*", result.Message);
	}

	[Fact]
	public void Evaluate_AllowWithPartialMatch_None()
	{
		var result = SpecifierEngine.Evaluate([Change("git diff *", SpecifierDecision.Allow)],
			Analyzer("git diff --stat", "git status"), null, CreateContext(), [], SpecifierAggregationMode.Sequential);

		Assert.Equal(SpecifierVerdict.None, result.Verdict);
	}

	[Fact]
	public void Evaluate_AskWithPartialMatch_Ask()
	{
		var result = SpecifierEngine.Evaluate([Change("git diff *", SpecifierDecision.Ask)],
			Analyzer("git diff --stat", "git status"), null, CreateContext(), [], SpecifierAggregationMode.Sequential);

		Assert.Equal(SpecifierVerdict.Ask, result.Verdict);
	}

	[Fact]
	public void Evaluate_DenyWithPartialMatch_Deny()
	{
		var result = SpecifierEngine.Evaluate([Change("git diff *", SpecifierDecision.Deny)],
			Analyzer("git diff --stat", "git status"), null, CreateContext(), [], SpecifierAggregationMode.Sequential);

		Assert.Equal(SpecifierVerdict.Deny, result.Verdict);
	}

	[Fact]
	public void Evaluate_EmptyPattern_Skipped()
	{
		var result = SpecifierEngine.Evaluate([Change("", SpecifierDecision.Deny), Change("git status:*", SpecifierDecision.Allow)],
			Analyzer("git status"), null, CreateContext(), [], SpecifierAggregationMode.Sequential);

		Assert.Equal(SpecifierVerdict.Allow, result.Verdict);
	}

	[Fact]
	public void Evaluate_InvalidPattern_Skipped()
	{
		var result = SpecifierEngine.Evaluate([Change("git ||", SpecifierDecision.Deny), Change("git status:*", SpecifierDecision.Allow)],
			Analyzer("git status"), null, CreateContext(), [], SpecifierAggregationMode.Sequential);

		Assert.Equal(SpecifierVerdict.Allow, result.Verdict);
	}

	[Fact]
	public void Evaluate_AnalyzerThrows_Skipped()
	{
		var result = SpecifierEngine.Evaluate([Change("git *", SpecifierDecision.Deny), Change("npm *", SpecifierDecision.Allow)],
			new AnalyzerStub((specifier, _, _) => specifier.Parts.Count == 1 && specifier.Parts[0] is SpecifierLiteralPart { Value: "git *" }
				? throw new InvalidOperationException("boom")
				: SpecifierMatcher.Match(specifier, ["npm install"], [])),
			null, CreateContext(), [], SpecifierAggregationMode.Sequential);

		Assert.Equal(SpecifierVerdict.Allow, result.Verdict);
	}

	// ──────────────────────────── Evaluate: aggregation modes ────────────────────────────

	[Fact]
	public void Evaluate_Sequential_LastMatchWins()
	{
		var result = SpecifierEngine.Evaluate(
			[Change("git status:*", SpecifierDecision.Allow), Change("git *", SpecifierDecision.Ask)],
			Analyzer("git status --short"), null, CreateContext(), [],
			SpecifierAggregationMode.Sequential);

		Assert.Equal(SpecifierVerdict.Ask, result.Verdict);
	}

	[Fact]
	public void Evaluate_Sequential_NonMatchingBetweenMatches_LastMatchingWins()
	{
		var result = SpecifierEngine.Evaluate(
			[Change("git status:*", SpecifierDecision.Allow), Change("npm *", SpecifierDecision.Deny), Change("git *", SpecifierDecision.Ask)],
			Analyzer("git status --short"), null, CreateContext(), [],
			SpecifierAggregationMode.Sequential);

		Assert.Equal(SpecifierVerdict.Ask, result.Verdict);
	}

	[Fact]
	public void Evaluate_Prioritized_DenyWinsOverAllowRegardlessOfOrder()
	{
		var result = SpecifierEngine.Evaluate(
			[Change("git *", SpecifierDecision.Allow), Change("git status:*", SpecifierDecision.Deny)],
			Analyzer("git status --short"), null, CreateContext(), [],
			SpecifierAggregationMode.Prioritized);

		Assert.Equal(SpecifierVerdict.Deny, result.Verdict);
	}

	[Fact]
	public void Evaluate_Prioritized_AskWinsOverAllow()
	{
		var result = SpecifierEngine.Evaluate(
			[Change("git status:*", SpecifierDecision.Ask), Change("git *", SpecifierDecision.Allow)],
			Analyzer("git status --short"), null, CreateContext(), [],
			SpecifierAggregationMode.Prioritized);

		Assert.Equal(SpecifierVerdict.Ask, result.Verdict);
	}

	[Fact]
	public void Evaluate_Prioritized_AllowWhenNoStricterMatch()
	{
		var result = SpecifierEngine.Evaluate(
			[Change("git status:*", SpecifierDecision.Allow), Change("npm *", SpecifierDecision.Ask)],
			Analyzer("git status --short"), null, CreateContext(), [],
			SpecifierAggregationMode.Prioritized);

		Assert.Equal(SpecifierVerdict.Allow, result.Verdict);
	}

	[Fact]
	public void Evaluate_AllowPartialMatchDoesNotOverride_Sequential()
	{
		// A partially matched Allow produces no verdict, so the previous verdict stays.
		var result = SpecifierEngine.Evaluate(
			[Change("git diff *", SpecifierDecision.Ask), Change("git status:*", SpecifierDecision.Allow)],
			Analyzer("git diff --stat", "git status"), null, CreateContext(), [],
			SpecifierAggregationMode.Sequential);

		Assert.Equal(SpecifierVerdict.Ask, result.Verdict);
	}

	// ──────────────────────────── Evaluate: specifier parameters ────────────────────────────

	[Fact]
	public void Evaluate_ParameterReference_MatchWhenParameterAllowed()
	{
		var result = SpecifierEngine.Evaluate(
			[Change("git status * && runTerminal:true", SpecifierDecision.Allow)],
			ParameterAnalyzer(["git status --short"], ("runTerminal", "true")),
			null, CreateContext(), ["runTerminal"], SpecifierAggregationMode.Sequential);

		Assert.Equal(SpecifierVerdict.Allow, result.Verdict);
	}

	[Fact]
	public void Evaluate_ParameterReference_NotInAllowedList_TreatedAsLiteral()
	{
		// Without the allowed parameter names the reference is a plain literal,
		// which does not match the main argument parts.
		var result = SpecifierEngine.Evaluate(
			[Change("git status * && runTerminal:true", SpecifierDecision.Allow)],
			ParameterAnalyzer(["git status --short"], ("runTerminal", "true")),
			null, CreateContext(), [], SpecifierAggregationMode.Sequential);

		Assert.Equal(SpecifierVerdict.None, result.Verdict);
	}

	[Fact]
	public void Evaluate_ParameterReference_ValueMismatch_NoVerdict()
	{
		var result = SpecifierEngine.Evaluate(
			[Change("runTerminal:true", SpecifierDecision.Allow)],
			ParameterAnalyzer(["git status"], ("runTerminal", "false")),
			null, CreateContext(), ["runTerminal"], SpecifierAggregationMode.Sequential);

		Assert.Equal(SpecifierVerdict.None, result.Verdict);
	}

	[Fact]
	public void Evaluate_ParameterReference_MissingParameter_NoVerdict()
	{
		var result = SpecifierEngine.Evaluate(
			[Change("runTerminal:true", SpecifierDecision.Allow)],
			ParameterAnalyzer(["git status"]),
			null, CreateContext(), ["runTerminal"], SpecifierAggregationMode.Sequential);

		Assert.Equal(SpecifierVerdict.None, result.Verdict);
	}

	[Fact]
	public void Evaluate_ParameterReference_AskWithPartialMatch_Ask()
	{
		// The main part matches partially while the parameter value does not match:
		// Ask and Deny verdicts require only a partial match.
		var result = SpecifierEngine.Evaluate(
			[Change("git status:* || runTerminal:true", SpecifierDecision.Ask)],
			ParameterAnalyzer(["git status --short", "npm install"], ("runTerminal", "false")),
			null, CreateContext(), ["runTerminal"], SpecifierAggregationMode.Sequential);

		Assert.Equal(SpecifierVerdict.Ask, result.Verdict);
	}

	[Fact]
	public void Evaluate_ParameterReference_DenyWithPartialMatch_Deny()
	{
		var result = SpecifierEngine.Evaluate(
			[Change("git status:* || runTerminal:true", SpecifierDecision.Deny)],
			ParameterAnalyzer(["git status --short", "npm install"], ("runTerminal", "false")),
			null, CreateContext(), ["runTerminal"], SpecifierAggregationMode.Sequential);

		Assert.Equal(SpecifierVerdict.Deny, result.Verdict);
	}

	// ──────────────────────────── Combine: Disabled ────────────────────────────

	[Theory]
	[InlineData(SpecifierVerdict.Allow, ToolPolicyDecision.Approve, ToolPolicyDecision.Approve)]
	[InlineData(SpecifierVerdict.Allow, ToolPolicyDecision.Ask, ToolPolicyDecision.Ask)]
	[InlineData(SpecifierVerdict.Deny, ToolPolicyDecision.Ask, ToolPolicyDecision.Ask)]
	[InlineData(SpecifierVerdict.None, ToolPolicyDecision.Disallow, ToolPolicyDecision.Disallow)]
	public void Combine_Disabled_ReturnsPolicy(SpecifierVerdict specifier, ToolPolicyDecision policy, ToolPolicyDecision expected)
	{
		Assert.Equal(expected, SpecifierEngine.Combine(specifier, policy, SpecifierBehaviourUnionMode.Disabled));
	}

	// ──────────────────────────── Combine: IgnoreNonSpecifierBehaviours ────────────────────────────

	[Theory]
	[InlineData(SpecifierVerdict.Allow, ToolPolicyDecision.Disallow, ToolPolicyDecision.Approve)]
	[InlineData(SpecifierVerdict.Ask, ToolPolicyDecision.Approve, ToolPolicyDecision.Ask)]
	[InlineData(SpecifierVerdict.Deny, ToolPolicyDecision.Approve, ToolPolicyDecision.Disallow)]
	[InlineData(SpecifierVerdict.None, ToolPolicyDecision.Ask, ToolPolicyDecision.Ask)]
	public void Combine_IgnoreNonSpecifierBehaviours_UsesSpecifier(SpecifierVerdict specifier, ToolPolicyDecision policy, ToolPolicyDecision expected)
	{
		Assert.Equal(expected, SpecifierEngine.Combine(specifier, policy, SpecifierBehaviourUnionMode.IgnoreNonSpecifierBehaviours));
	}

	// ──────────────────────────── Combine: CombineHard ────────────────────────────

	[Theory]
	[InlineData(SpecifierVerdict.Allow, ToolPolicyDecision.Approve, ToolPolicyDecision.Approve)]
	[InlineData(SpecifierVerdict.Allow, ToolPolicyDecision.Ask, ToolPolicyDecision.Ask)]
	[InlineData(SpecifierVerdict.Allow, ToolPolicyDecision.Disallow, ToolPolicyDecision.Disallow)]
	[InlineData(SpecifierVerdict.Ask, ToolPolicyDecision.Approve, ToolPolicyDecision.Ask)]
	[InlineData(SpecifierVerdict.Ask, ToolPolicyDecision.Ask, ToolPolicyDecision.Ask)]
	[InlineData(SpecifierVerdict.Ask, ToolPolicyDecision.Disallow, ToolPolicyDecision.Disallow)]
	[InlineData(SpecifierVerdict.Deny, ToolPolicyDecision.Approve, ToolPolicyDecision.Disallow)]
	[InlineData(SpecifierVerdict.Deny, ToolPolicyDecision.Ask, ToolPolicyDecision.Disallow)]
	[InlineData(SpecifierVerdict.Deny, ToolPolicyDecision.Disallow, ToolPolicyDecision.Disallow)]
	[InlineData(SpecifierVerdict.None, ToolPolicyDecision.Ask, ToolPolicyDecision.Ask)]
	[InlineData(SpecifierVerdict.None, ToolPolicyDecision.Disallow, ToolPolicyDecision.Disallow)]
	public void Combine_CombineHard_StrictestWins(SpecifierVerdict specifier, ToolPolicyDecision policy, ToolPolicyDecision expected)
	{
		Assert.Equal(expected, SpecifierEngine.Combine(specifier, policy, SpecifierBehaviourUnionMode.CombineHard));
	}

	// ──────────────────────────── Combine: CombineSoft ────────────────────────────

	[Theory]
	[InlineData(SpecifierVerdict.Allow, ToolPolicyDecision.Approve, ToolPolicyDecision.Approve)]
	[InlineData(SpecifierVerdict.Allow, ToolPolicyDecision.Ask, ToolPolicyDecision.Approve)]
	[InlineData(SpecifierVerdict.Allow, ToolPolicyDecision.Disallow, ToolPolicyDecision.Disallow)]
	[InlineData(SpecifierVerdict.Ask, ToolPolicyDecision.Approve, ToolPolicyDecision.Ask)]
	[InlineData(SpecifierVerdict.Ask, ToolPolicyDecision.Ask, ToolPolicyDecision.Ask)]
	[InlineData(SpecifierVerdict.Ask, ToolPolicyDecision.Disallow, ToolPolicyDecision.Disallow)]
	[InlineData(SpecifierVerdict.Deny, ToolPolicyDecision.Approve, ToolPolicyDecision.Disallow)]
	[InlineData(SpecifierVerdict.Deny, ToolPolicyDecision.Ask, ToolPolicyDecision.Disallow)]
	[InlineData(SpecifierVerdict.Deny, ToolPolicyDecision.Disallow, ToolPolicyDecision.Disallow)]
	[InlineData(SpecifierVerdict.None, ToolPolicyDecision.Approve, ToolPolicyDecision.Approve)]
	[InlineData(SpecifierVerdict.None, ToolPolicyDecision.Ask, ToolPolicyDecision.Ask)]
	[InlineData(SpecifierVerdict.None, ToolPolicyDecision.Disallow, ToolPolicyDecision.Disallow)]
	public void Combine_CombineSoft_SpecifierSuppressesAskButNotPolicyDisallow(SpecifierVerdict specifier, ToolPolicyDecision policy, ToolPolicyDecision expected)
	{
		Assert.Equal(expected, SpecifierEngine.Combine(specifier, policy, SpecifierBehaviourUnionMode.CombineSoft));
	}
}
