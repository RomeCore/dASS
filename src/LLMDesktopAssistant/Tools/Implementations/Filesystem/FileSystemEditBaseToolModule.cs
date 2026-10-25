using LLMDesktopAssistant.Addons;
using LLMDesktopAssistant.Tools.Execution.Domain;
using LLMDesktopAssistant.Tools.MVVM.Diff;
using LLMDesktopAssistant.Utils.Files;

namespace LLMDesktopAssistant.Tools.Implementations.Filesystem
{
	/// <summary>
	/// The shared machinery of the filesystem edit family: the preview-stage diff card, the approval
	/// provider that turns it interactive and awaits the user, and the result shape the main executor
	/// reports.
	/// </summary>
	/// <remarks>
	/// The family does not decide anything inside <c>Main</c>. Preview computes the full new content and
	/// attaches a read-only diff; the approval provider (running in the confirmation stage) makes the same
	/// card interactive and records the decision in the shared context; <c>Main</c> only writes the content
	/// the context carries. Without an approval payload (policy auto-approve, a restart, or a test that
	/// calls the executor directly) the previewed content is written whole.
	/// </remarks>
	public abstract class FileSystemEditBaseToolModule : ToolModule
	{
		/// <summary>
		/// The shared state of one filesystem edit.
		/// </summary>
		protected abstract class DiffSharedContext
		{
			public required string Path { get; init; }

			public required AddonKind AddonKind { get; init; }

			/// <summary>
			/// The file as preview read it, so the write can put the same encoding, byte order mark and
			/// line ending back instead of letting .NET pick UTF-8 without a preamble.
			/// </summary>
			public TextFileContent? Source { get; init; }

			public required string OriginalContent { get; init; }

			public required string PreviewedContent { get; init; }

			public required HunkGroups FullDiff { get; init; }

			/// <summary>
			/// The read-only diff card attached by preview, or <see langword="null"/> when there is no UI.
			/// </summary>
			public TextDiffAdditionalViewModel? DiffView { get; init; }

			public bool? Approved { get; set; }

			public HunkGroups? AppliedDiff { get; set; }

			public HunkGroups? RejectedDiff { get; set; }

			public string? UserNotes { get; set; }

			/// <summary>
			/// The content the main executor writes: the approved subset when the user narrowed it, the
			/// previewed content otherwise.
			/// </summary>
			public string? ApprovedContent { get; set; }

			public string EffectiveContent => ApprovedContent ?? PreviewedContent;
		}

		/// <summary>
		/// The outcome of one edit, as the main executor reports it.
		/// </summary>
		protected sealed class DiffResult
		{
			public required HunkGroups Diff { get; init; }

			public required HunkGroups AppliedDiff { get; init; }

			public required HunkGroups RejectedDiff { get; init; }

			public required string NewContent { get; init; }
		}

		/// <summary>
		/// Computes the diff and, when a user interface exists, attaches a read-only card to the call.
		/// The approval provider later flips the same card to interactive mode.
		/// </summary>
		protected static (TextDiffAdditionalViewModel? View, HunkGroups Diff) PrepareDiff(
			IToolExecutionContext ctx, string path, string originalContent, string newContent)
		{
			var diff = UnifiedDiff.Compute(originalContent, newContent, contextLines: 3);

			if (!ctx.RunningInUi)
				return (null, diff);

			var view = new TextDiffAdditionalViewModel
			{
				Title = path,
				IsReadOnly = true
			};
			view.LoadFromHunkGroups(diff);
			ctx.Call.AddAttachment(view);

			return (view, diff);
		}

		/// <summary>
		/// Awaits the user's decision through the diff card. Without an interactive surface the previewed
		/// change is approved whole; the preview normally removes the <c>Ask</c> bit in that case.
		/// </summary>
		protected static async Task<ToolApprovalResult> AwaitDiffConsentAsync(
			IToolExecutionContext ctx, DiffSharedContext? shared, CancellationToken ct)
		{
			if (shared is null)
				return ToolApprovalResult.DeniedOnce(
					"The diff preview is unavailable, so the edit was not applied.");

			var view = shared.DiffView;
			if (!ctx.RunningInUi)
			{
				shared.Approved = true;
				shared.AppliedDiff = shared.FullDiff;
				shared.RejectedDiff = EmptyDiff();
				return ToolApprovalResult.ApprovedOnce();
			}

			if (view is null)
				return ToolApprovalResult.DeniedOnce(
					"The diff preview is unavailable, so the edit was not applied.");

			view.IsReadOnly = false;

			using var registration = ct.Register(() => view.Decline(null));
			var consent = await view.ConsentResultTask.WaitAsync(ct);

			shared.UserNotes = consent.Notes;
			shared.Approved = consent.Approved;

			if (consent.Approved)
			{
				shared.AppliedDiff = view.BuildEnabledHunkGroups();
				shared.RejectedDiff = view.BuildDisabledHunkGroups();
				shared.ApprovedContent = view.ApplyToText(shared.OriginalContent);
			}
			else
			{
				shared.AppliedDiff = EmptyDiff();
				shared.RejectedDiff = view.BuildEnabledHunkGroups();
			}

			return consent;
		}

		/// <summary>
		/// Turns the shared state into the report shape. An invocation without an approval payload (policy
		/// auto-approve, restart, direct executor call) reports the whole previewed change as applied.
		/// </summary>
		protected static DiffResult BuildDiffResult(DiffSharedContext shared)
		{
			if (shared.Approved is null)
			{
				return new DiffResult
				{
					Diff = shared.FullDiff,
					AppliedDiff = shared.FullDiff,
					RejectedDiff = EmptyDiff(),
					NewContent = shared.PreviewedContent
				};
			}

			return new DiffResult
			{
				Diff = shared.FullDiff,
				AppliedDiff = shared.AppliedDiff ?? EmptyDiff(),
				RejectedDiff = shared.RejectedDiff ?? EmptyDiff(),
				NewContent = shared.Approved == true ? shared.EffectiveContent : shared.OriginalContent
			};
		}

		/// <summary>
		/// Builds the report for a direct executor call that never ran preview.
		/// </summary>
		protected static DiffResult BuildUnpreviewedResult(string originalContent, string newContent)
		{
			var diff = UnifiedDiff.Compute(originalContent, newContent, contextLines: 3);

			return new DiffResult
			{
				Diff = diff,
				AppliedDiff = diff,
				RejectedDiff = EmptyDiff(),
				NewContent = newContent
			};
		}

		protected static HunkGroups EmptyDiff() => new() { Groups = [] };
	}
}
