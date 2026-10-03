using LLMDesktopAssistant.LLM.Services;
using LLMDesktopAssistant.Tools.Consents;
using LLMDesktopAssistant.Tools.MVVM.Diff;
using LLMDesktopAssistant.Utils.Files;

namespace LLMDesktopAssistant.Tools.Implementations.Filesystem
{
	public abstract class FileSystemEditBaseToolModule : ToolModule
	{
		protected const string SyncGroup = "fs-edit";

		protected class DiffPostProcessResult
		{
			public required HunkGroups Diff { get; init; }

			public required HunkGroups AppliedDiff { get; init; }

			public required HunkGroups RejectedDiff { get; init; }

			public required string NewContent { get; init; }

			public required string? UserNotes { get; init; }
		}

		protected async Task<DiffPostProcessResult> PostProcessDiffAsync(string filename, string oldContent, string newContent,
			ToolExecutionContext ctx, ReactiveToolResult result, CancellationToken cancellationToken)
		{
			var diff = UnifiedDiff.Compute(oldContent, newContent, contextLines: 3);

			if (!ctx.RunningInUI)
				return new DiffPostProcessResult
				{
					Diff = diff,
					AppliedDiff = diff,
					RejectedDiff = new HunkGroups { Groups = [] },
					NewContent = newContent,
					UserNotes = null
				};

			if (ctx.PolicyDecision == ToolPolicyDecision.Ask)
			{
				var diffVM = new TextDiffAdditionalViewModel
				{
					Title = filename,
					IsReadOnly = false,
					ConsentContext = ctx.ConsentContext
				};
				diffVM.LoadFromHunkGroups(diff);
				result.AdditionalData.Add(diffVM);

				using var reg = cancellationToken.Register(() =>
				{
					diffVM.Decline(null);
				});

				var chatStatusService = ctx.Chat.Services.GetService<IChatExecutionStatusService>();

				MemorizedDecision decision;
				using (var askStatus = chatStatusService?.WithConfirmation())
					decision = await diffVM.ConfirmationTask;
				
				if (decision.Approved)
				{
					return new DiffPostProcessResult
					{
						Diff = diff,
						AppliedDiff = diffVM.BuildEnabledHunkGroups(),
						RejectedDiff = diffVM.BuildDisabledHunkGroups(),
						NewContent = diffVM.ApplyToText(oldContent),
						UserNotes = decision.Notes
					};
				}

				return new DiffPostProcessResult
				{
					Diff = diff,
					AppliedDiff = new HunkGroups { Groups = [] },
					RejectedDiff = diff,
					NewContent = diffVM.ApplyToText(oldContent),
					UserNotes = decision.Notes
				};
			}
			else
			{
				var diffVM = new TextDiffAdditionalViewModel
				{
					IsReadOnly = true
				};
				diffVM.LoadFromHunkGroups(diff);
				result.AdditionalData.Add(diffVM);
				return new DiffPostProcessResult
				{
					Diff = diff,
					AppliedDiff = diff,
					RejectedDiff = new HunkGroups { Groups = [] },
					NewContent = newContent,
					UserNotes = null
				};
			}
		}
	}
}
