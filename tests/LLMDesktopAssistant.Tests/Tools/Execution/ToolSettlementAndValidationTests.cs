using System.Text.Json.Nodes;
using LLMDesktopAssistant.LLM.Domain;
using LLMDesktopAssistant.Tests.Tools;
using LLMDesktopAssistant.Tools.Execution.Domain;
using LLMDesktopAssistant.Tools.Execution.Services;

namespace LLMDesktopAssistant.Tests.Tools.Execution
{
	/// <summary>
	/// The settlement and validation stages: settlement owns the terminal state (including a thrown
	/// exception), validation materializes the effective arguments and throws on invalid ones.
	/// </summary>
	public class ToolSettlementAndValidationTests
	{
		[Theory]
		[InlineData(ToolStatus.Success, "Tool successfully returned no result.")]
		[InlineData(ToolStatus.Error, "Tool failed with no result.")]
		[InlineData(ToolStatus.Cancelled, "Tool execution was cancelled.")]
		public void Finalize_FillsTheDefaultContentByStatus(ToolStatus status, string expected)
		{
			var session = ToolTestContext.Create(ToolTestContext.CreateTool());
			session.Call.Status = status;
			session.Call.ResultContent = null;

			new ToolSettlementService().Finalize(session.Context, null);

			Assert.Equal(expected, session.Call.ResultContent);
		}

		[Fact]
		public void Finalize_KeepsTheContentTheToolProduced()
		{
			var session = ToolTestContext.Create(ToolTestContext.CreateTool());
			session.Call.Status = ToolStatus.Success;
			session.Call.ResultContent = "the tool said this";

			new ToolSettlementService().Finalize(session.Context, null);

			Assert.Equal("the tool said this", session.Call.ResultContent);
		}

		[Fact]
		public void Finalize_MarksCancellationAsCancelled()
		{
			var session = ToolTestContext.Create(ToolTestContext.CreateTool());
			session.Call.ResultContent = null;

			new ToolSettlementService().Finalize(session.Context, new OperationCanceledException());

			Assert.Equal(ToolStatus.Cancelled, session.Call.Status);
			Assert.Equal("Tool execution was interrupted.", session.Call.ResultContent);
		}

		[Fact]
		public void Finalize_MarksAnUnexpectedFailureAsError()
		{
			var session = ToolTestContext.Create(ToolTestContext.CreateTool());
			session.Call.ResultContent = null;

			new ToolSettlementService().Finalize(session.Context, new InvalidOperationException("boom"));

			Assert.Equal(ToolStatus.Error, session.Call.Status);
			Assert.Contains("boom", session.Call.ResultContent);
		}

		[Fact]
		public void Finalize_WritesAModelFacingMessageVerbatim()
		{
			var session = ToolTestContext.Create(ToolTestContext.CreateTool());
			session.Call.ResultContent = null;

			new ToolSettlementService()
				.Finalize(session.Context, new ToolArgumentsInvalidException("The tool arguments are invalid."));

			Assert.Equal(ToolStatus.Error, session.Call.Status);
			Assert.Equal("The tool arguments are invalid.", session.Call.ResultContent);
		}

		[Fact]
		public void Validate_MaterializesTheEffectiveArgumentsAndAcceptsThem()
		{
			var session = ToolTestContext.Create(ToolTestContext.CreateTool(), JsonNode.Parse("""{"a":1}"""));

			new ToolValidationService().Validate(session.Context);

			Assert.Equal("1", session.Context.Arguments.Effective?["a"]?.ToJsonString());
		}
	}
}
