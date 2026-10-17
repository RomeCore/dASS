using System.Text.Json.Nodes;
using LLMDesktopAssistant.Tools.Specifiers;

namespace LLMDesktopAssistant.Tools.Execution.Domain
{
	/// <summary>
	/// Matches a parsed specifier against the tool arguments for the policy stage.
	/// </summary>
	public interface IToolSpecifierAnalyzer
	{
		/// <summary>
		/// Analyses one specifier rule.
		/// </summary>
		/// <param name="specifier">The parsed specifier to match.</param>
		/// <param name="args">The effective arguments.</param>
		/// <param name="ctx">The invocation context.</param>
		/// <returns>The match result the specifier engine combines with the other rules.</returns>
		SpecifierMatchResult Analyze(Specifier specifier, JsonNode? args, IToolExecutionContext ctx);
	}
}
