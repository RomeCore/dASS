using System.Text.Json.Nodes;
using LLMDesktopAssistant.LLM.Domain;
using LLMDesktopAssistant.LLM.MVVM.Additional;
using LLMDesktopAssistant.Tests.Tools;
using LLMDesktopAssistant.Tools;
using LLMDesktopAssistant.Tools.Consents;
using LLMDesktopAssistant.Tools.Execution.Domain;
using LLMDesktopAssistant.Tools.Execution.Services;

namespace LLMDesktopAssistant.Tests.Tools.Execution
{
	/// <summary>
	/// The confirmation stage: an unhandled decision is served by the invocation's surface, a handled one
	/// is delegated to the tool's approval provider, and the user's notes land on the call as an invisible
	/// content part.
	/// </summary>
	public class ToolConfirmationServiceTests
	{
		[Fact]
		public async Task AnUnhandledDisallow_WritesTheReasonAndStops()
		{
			var session = ToolTestContext.Create(ToolTestContext.CreateTool());
			session.Context.PolicyDecision = ToolPolicyDecision.Disallow;
			session.Context.PolicyReason = new ToolPolicyReason(ToolDecisionSource.ApprovalLevel, "policy says no");

			var approved = await new ToolConfirmationService().ConfirmAsync(session.Context, CancellationToken.None);

			Assert.False(approved);
			Assert.Equal(ToolStatus.Error, session.Call.Status);
			Assert.Equal("policy says no", session.Call.ResultContent);
		}

		[Fact]
		public async Task AnUnhandledAsk_UsesTheSinkAndWritesTheCancellation()
		{
			var sink = new FixedConfirmationSink(ToolApprovalResult.DeniedOnce("too risky"));
			var session = ToolTestContext.Create(ToolTestContext.CreateTool(), confirmation: sink);
			session.Context.PolicyDecision = ToolPolicyDecision.Ask;

			var approved = await new ToolConfirmationService().ConfirmAsync(session.Context, CancellationToken.None);

			Assert.False(approved);
			Assert.Equal(1, sink.Calls);
			Assert.Equal(ToolStatus.Cancelled, session.Call.Status);
			Assert.Contains("too risky", session.Call.ResultContent ?? string.Empty);
		}

		[Fact]
		public async Task AnApprovedAsk_AddsTheNotesAsAnInvisibleContentPart()
		{
			var sink = new FixedConfirmationSink(ToolApprovalResult.ApprovedOnce("looks fine"));
			var session = ToolTestContext.Create(ToolTestContext.CreateTool(), confirmation: sink);
			session.Context.PolicyDecision = ToolPolicyDecision.Ask;

			var approved = await new ToolConfirmationService().ConfirmAsync(session.Context, CancellationToken.None);

			Assert.True(approved);
			var part = Assert.Single(session.Call.AdditionalData.OfType<AdditionalMessageContentPart>());
			Assert.Equal("Additional notes from user: looks fine", part.Content);
			Assert.False(part.IsVisible);
			Assert.False(part.IsRestorable);
		}

		[Fact]
		public async Task AHandledAsk_UsesTheProviderInsteadOfTheSink()
		{
			var sink = new FixedConfirmationSink(ToolApprovalResult.DeniedOnce());
			var providerCalls = 0;
			Func<IToolExecutionContext, CancellationToken, Task<ToolApprovalResult>> provider =
				(_, _) =>
				{
					providerCalls++;
					return Task.FromResult(ToolApprovalResult.ApprovedOnce());
				};
			var tool = ToolTestContext.CreateTool("self-confirming",
				info => info.HandledDecisions = ToolPolicyDecision.Ask, approval: provider);
			var session = ToolTestContext.Create(tool, confirmation: sink);
			session.Context.PolicyDecision = ToolPolicyDecision.Ask;

			var approved = await new ToolConfirmationService().ConfirmAsync(session.Context, CancellationToken.None);

			Assert.True(approved);
			Assert.Equal(1, providerCalls);
			Assert.Equal(0, sink.Calls);
		}

		[Fact]
		public async Task AHandledDisallow_IsDelegatedToTheProvider()
		{
			var providerCalls = 0;
			Func<IToolExecutionContext, CancellationToken, Task<ToolApprovalResult>> provider =
				(_, _) =>
				{
					providerCalls++;
					return Task.FromResult(ToolApprovalResult.ApprovedOnce());
				};
			var tool = ToolTestContext.CreateTool("self-disallowing",
				info => info.HandledDecisions = ToolPolicyDecision.Disallow, approval: provider);
			var session = ToolTestContext.Create(tool);
			session.Context.PolicyDecision = ToolPolicyDecision.Disallow;

			var approved = await new ToolConfirmationService().ConfirmAsync(session.Context, CancellationToken.None);

			Assert.True(approved);
			Assert.Equal(1, providerCalls);
		}

		[Fact]
		public async Task AHandledDecisionWithoutProvider_FailsTheCall()
		{
			var tool = new ToolInfo
			{
				Name = "broken-handler",
				Description = "Declares a handled decision without a provider.",
				HandledDecisions = ToolPolicyDecision.Ask
			};
			var session = ToolTestContext.Create(tool);
			session.Context.PolicyDecision = ToolPolicyDecision.Ask;

			var approved = await new ToolConfirmationService().ConfirmAsync(session.Context, CancellationToken.None);

			Assert.False(approved);
			Assert.Equal(ToolStatus.Error, session.Call.Status);
			Assert.Contains("no approval provider", session.Call.ResultContent ?? string.Empty);
		}

		[Fact]
		public async Task TheInvocationMask_OverridesTheDescriptor()
		{
			var providerCalls = 0;
			Func<IToolExecutionContext, CancellationToken, Task<ToolApprovalResult>> provider =
				(_, _) =>
				{
					providerCalls++;
					return Task.FromResult(ToolApprovalResult.ApprovedOnce());
				};
			var tool = ToolTestContext.CreateTool("mask-override",
				info => info.HandledDecisions = ToolPolicyDecision.None, approval: provider);
			var session = ToolTestContext.Create(tool);
			session.Context.HandledDecisions = ToolPolicyDecision.Ask;
			session.Context.PolicyDecision = ToolPolicyDecision.Ask;

			var approved = await new ToolConfirmationService().ConfirmAsync(session.Context, CancellationToken.None);

			Assert.True(approved);
			Assert.Equal(1, providerCalls);
		}

		[Fact]
		public async Task ToolPolicyDecisionAll_HandlesEveryDecision()
		{
			var providerCalls = 0;
			Func<IToolExecutionContext, CancellationToken, Task<ToolApprovalResult>> provider =
				(_, _) =>
				{
					providerCalls++;
					return Task.FromResult(ToolApprovalResult.ApprovedOnce());
				};
			var tool = ToolTestContext.CreateTool("all-handler",
				info => info.HandledDecisions = ToolPolicyDecision.All, approval: provider);

			foreach (var decision in new[] { ToolPolicyDecision.Ask, ToolPolicyDecision.Disallow })
			{
				var session = ToolTestContext.Create(tool);
				session.Context.PolicyDecision = decision;

				Assert.True(await new ToolConfirmationService().ConfirmAsync(session.Context, CancellationToken.None));
			}

			Assert.Equal(2, providerCalls);
		}

		[Fact]
		public async Task EditedArguments_WithOverrideModelIntent_ReplaceThePersistedIntent()
		{
			var edited = JsonNode.Parse("""{"path":"new.txt"}""");
			var tool = ToolTestContext.CreateTool("editing",
				info => info.HandledDecisions = ToolPolicyDecision.Ask,
				approval: (Func<IToolExecutionContext, CancellationToken, Task<ToolApprovalResult>>)
					((_, _) => Task.FromResult(new ToolApprovalResult
					{
						Approved = true,
						Memorization = ToolApprovalMemorization.Once,
						EditedArguments = edited,
						OverrideModelIntent = true
					})));
			var session = ToolTestContext.Create(tool, JsonNode.Parse("""{"path":"old.txt"}"""));
			session.Context.PolicyDecision = ToolPolicyDecision.Ask;

			Assert.True(await new ToolConfirmationService().ConfirmAsync(session.Context, CancellationToken.None));

			Assert.Equal("new.txt", session.Context.Arguments.Effective?["path"]?.GetValue<string>());
			Assert.Equal("new.txt", JsonNode.Parse(session.Call.Arguments)?["path"]?.GetValue<string>());
			Assert.Null(session.Context.ArgumentsOverride);
		}

		[Fact]
		public async Task EditedArguments_WithoutOverrideModelIntent_SetTheEphemeralChannel()
		{
			var edited = JsonNode.Parse("""{"path":"new.txt"}""");
			var tool = ToolTestContext.CreateTool("editing-ephemeral",
				info => info.HandledDecisions = ToolPolicyDecision.Ask,
				approval: (Func<IToolExecutionContext, CancellationToken, Task<ToolApprovalResult>>)
					((_, _) => Task.FromResult(new ToolApprovalResult
					{
						Approved = true,
						Memorization = ToolApprovalMemorization.Once,
						EditedArguments = edited
					})));
			var session = ToolTestContext.Create(tool, JsonNode.Parse("""{"path":"old.txt"}"""));
			session.Context.PolicyDecision = ToolPolicyDecision.Ask;

			Assert.True(await new ToolConfirmationService().ConfirmAsync(session.Context, CancellationToken.None));

			Assert.Equal("new.txt", session.Context.Arguments.Effective?["path"]?.GetValue<string>());
			Assert.Equal("old.txt", JsonNode.Parse(session.Call.Arguments)?["path"]?.GetValue<string>());
			Assert.Same(edited, session.Context.ArgumentsOverride);
		}

		[Fact]
		public async Task ApprovalData_ReachesTheContext()
		{
			var approvalData = JsonNode.Parse("""{"hunks":[1,2]}""");
			var tool = ToolTestContext.CreateTool("approval-data",
				info => info.HandledDecisions = ToolPolicyDecision.Ask,
				approval: (Func<IToolExecutionContext, CancellationToken, Task<ToolApprovalResult>>)
					((_, _) => Task.FromResult(new ToolApprovalResult
					{
						Approved = true,
						Memorization = ToolApprovalMemorization.Once,
						ApprovalData = approvalData
					})));
			var session = ToolTestContext.Create(tool);
			session.Context.PolicyDecision = ToolPolicyDecision.Ask;

			Assert.True(await new ToolConfirmationService().ConfirmAsync(session.Context, CancellationToken.None));

			Assert.Same(approvalData, session.Context.ApprovalData);
		}

		[Fact]
		public async Task AProviderDecision_IsMemorizedByTheService()
		{
			var memorization = new RecordingMemorization();
			Func<IToolExecutionContext, CancellationToken, Task<ToolApprovalResult>> provider =
				(_, _) => Task.FromResult(new ToolApprovalResult
				{
					Approved = true,
					Memorization = ToolApprovalMemorization.Session,
					Notes = "remember it"
				});
			var tool = ToolTestContext.CreateTool("memorizing",
				info => info.HandledDecisions = ToolPolicyDecision.Ask, approval: provider);
			var session = ToolTestContext.Create(tool, memorization: memorization);
			session.Context.PolicyDecision = ToolPolicyDecision.Ask;

			Assert.True(await new ToolConfirmationService().ConfirmAsync(session.Context, CancellationToken.None));

			var consent = Assert.Single(memorization.Consents);
			Assert.True(consent.IsApproved);
			Assert.Equal(ToolApprovalMemorization.Session, consent.Memorization);
			Assert.Equal("remember it", consent.Notes);
		}

		private sealed class FixedConfirmationSink(ToolApprovalResult result) : IConfirmationSink
		{
			public int Calls { get; private set; }

			public Task<ToolApprovalResult> RequestAsync(IToolExecutionContext ctx, CancellationToken ct)
			{
				Calls++;
				return Task.FromResult(result);
			}
		}

		private sealed class RecordingMemorization : IToolMemorizationService
		{
			public List<ToolConsentResult> Consents { get; } = [];

			public void PushTaskAsyncScope()
			{
			}

			public void MemorizeConsent(Chat? chat, string toolName, ToolConsentResult consentResult) =>
				Consents.Add(consentResult);

			public bool TryGetMemorizedDecision(Chat? chat, string toolName, out ToolPolicyDecision decision,
				out string? message)
			{
				decision = ToolPolicyDecision.None;
				message = null;
				return false;
			}

			public IEnumerable<MemorizedConsentInfo> GetMemorizedConsents(Chat? chat) => [];

			public bool ForgetConsent(Chat? chat, string toolName) => false;

			public void ClearConsents(Chat? chat)
			{
			}
		}
	}
}
