using LLMDesktopAssistant.Localization;
using LLMDesktopAssistant.Services;
using LLMDesktopAssistant.Tools.Execution.Domain;
using LLMDesktopAssistant.Tools.Execution.Support;

namespace LLMDesktopAssistant.Tools.Execution.Services
{
	/// <summary>
	/// The keyed scope. Keys are typed, hierarchical and carry an access: a held
	/// <see cref="ToolSyncKeyKind.Directory"/> conflicts with a <see cref="ToolSyncKeyKind.File"/> beneath
	/// it, a <see cref="ToolSyncAccess.Write"/> claim conflicts with everything, and two
	/// <see cref="ToolSyncAccess.Read"/> claims of the same key coexist.
	/// </summary>
	/// <remarks>
	/// <para>
	/// Acquisition is a queue over the intersection of key sets, taken from the head: an invocation is
	/// granted when no held key conflicts with its own and no conflicting invocation is already waiting
	/// ahead of it. Both checks happen under one mutex, so a request never merely observes a free key
	/// set - it claims it. Nothing holds one key while waiting for another, so no lock order exists and
	/// no deadlock is possible.
	/// </para>
	/// <para>
	/// The user may cancel the wait through <see cref="IToolCall.SynchronizationWait"/>: the invocation
	/// then runs without claiming anything, and the queue is re-examined so the skipped entry does not
	/// keep blocking the ones behind it.
	/// </para>
	/// <para>
	/// Path keys are compared OS-aware (case-insensitive on Windows);
	/// <see cref="ToolSyncKeyKind.Custom"/> keys are compared ordinally. The service canonicalizes before
	/// acquiring: trailing separators are trimmed (roots preserved), duplicates are dropped, a read and
	/// a write of one key widen to the write, and a key covered by a requested directory collapses.
	/// Absolutization belongs to the analyzer, never to this service.
	/// </para>
	/// </remarks>
	[Service(typeof(IToolSynchronizationService))]
	public sealed class ToolSynchronizationService : IToolSynchronizationService
	{
		private static readonly StringComparer PathComparer =
			OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

		private static readonly StringComparison PathComparison =
			OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

		private readonly SemaphoreSlim _mutex = new(1, 1);
		private readonly List<HeldKey> _held = [];
		private readonly List<Waiter> _queue = [];
		private TaskCompletionSource _changed = CreateSignal();

		/// <inheritdoc/>
		public async Task RunAsync(IToolExecutionContext ctx, Func<Task> inner, CancellationToken ct)
		{
			var keys = Collect(ctx);
			if (keys.Count == 0)
			{
				ctx.Call.SynchronizationWait = null;
				await inner();
				return;
			}

			var wait = new CancellationTokenSource();
			if (!await AcquireAsync(ctx, keys, wait, ct))
			{
				// The user released the invocation from the wait: it runs unguarded, and the cancelled
				// source stays on the call so the card can tell that this run was not synchronized.
				ct.ThrowIfCancellationRequested();
				await inner();
				return;
			}

			try
			{
				ct.ThrowIfCancellationRequested();
				await inner();
			}
			finally
			{
				await ReleaseAsync(keys);
			}
		}

		/// <summary>
		/// Collects and canonicalizes the keys of one invocation: the descriptor's static keys plus the
		/// synchronization analyzer's per-invocation keys.
		/// </summary>
		private static List<ToolSyncKey> Collect(IToolExecutionContext ctx)
		{
			var keys = new List<ToolSyncKey>(ctx.Descriptor.StaticSyncKeys);
			if (ctx.Descriptor.SyncAnalyzer is { } analyzer)
				keys.AddRange(analyzer.AnalyzeSyncKeys(ctx.Arguments.Effective, ctx));

			return Canonicalize(keys);
		}

		/// <summary>
		/// Normalizes, deduplicates and collapses a key set. Exposed for the unit tests; the service
		/// itself is the only production caller.
		/// </summary>
		/// <param name="keys">The raw key set.</param>
		/// <returns>The canonical key set.</returns>
		public static List<ToolSyncKey> Canonicalize(IEnumerable<ToolSyncKey> keys)
		{
			var result = new List<ToolSyncKey>();

			foreach (var key in keys)
			{
				var value = key.Kind switch
				{
					ToolSyncKeyKind.AllFiles => string.Empty,
					ToolSyncKeyKind.Custom => key.Value?.Trim() ?? string.Empty,
					_ => Path.TrimEndingDirectorySeparator(key.Value?.Trim() ?? string.Empty)
				};

				if (key.Kind != ToolSyncKeyKind.AllFiles && string.IsNullOrEmpty(value))
					continue;

				var canonical = new ToolSyncKey(key.Kind, value, key.Access);
				var existing = result.FindIndex(candidate => SameTarget(candidate, canonical));

				// Asking for the same target twice is one claim, and a write claim subsumes a read one.
				if (existing < 0)
					result.Add(canonical);
				else if (result[existing].Access == ToolSyncAccess.Read && canonical.Access == ToolSyncAccess.Write)
					result[existing] = canonical;
			}

			// A requested write directory covers every key beneath it; a read directory covers only the
			// reads beneath it, so a write of a file inside it must survive as a separate claim.
			result.RemoveAll(candidate =>
				candidate.Kind is ToolSyncKeyKind.File or ToolSyncKeyKind.Directory &&
				result.Any(cover =>
					cover.Kind == ToolSyncKeyKind.Directory &&
					!SameTarget(candidate, cover) &&
					(cover.Access == ToolSyncAccess.Write || candidate.Access == ToolSyncAccess.Read) &&
					IsBeneathOrEqual(candidate.Value, cover.Value)));

			return result;
		}

		private async Task<bool> AcquireAsync(IToolExecutionContext ctx, IReadOnlyList<ToolSyncKey> keys,
			CancellationTokenSource wait, CancellationToken ct)
		{
			var waiter = new Waiter(keys, ctx.Call.ToolName);
			var previousTitle = ctx.Call.StatusTitle;
			ctx.Call.SynchronizationWait = wait;

			while (true)
			{
				Task changed;

				await _mutex.WaitAsync(ct);
				try
				{
					var blocker = FindBlocker(keys, waiter);
					if (blocker is null)
					{
						_queue.Remove(waiter);
						_held.AddRange(keys.Select(key => new HeldKey(key, waiter.Owner)));
						ctx.Call.SynchronizationWait = null;
						ctx.Call.SetStatusTitle(previousTitle);
						return true;
					}

					ctx.Call.SetStatusTitle(blocker);

					if (!_queue.Contains(waiter))
						_queue.Add(waiter);

					changed = _changed.Task;
				}
				finally
				{
					_mutex.Release();
				}

				// The wait ends on a state change, on the user's skip or on the caller's cancellation;
				// none of the three can be missed, because the queue is re-examined after every wake.
				var skip = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
				using var registration = wait.Token.Register(() => skip.TrySetResult());

				try
				{
					await Task.WhenAny(changed, skip.Task).WaitAsync(ct);
				}
				catch (OperationCanceledException)
				{
					await DropAsync(waiter);
					throw;
				}
				finally
				{
					ctx.Call.SynchronizationWait = null;
					ctx.Call.SetStatusTitle(previousTitle);
				}

				if (!wait.IsCancellationRequested)
					continue;

				await DropAsync(waiter);
				ctx.Call.SetStatusTitle(previousTitle);
				return false;
			}
		}

		private async Task ReleaseAsync(IReadOnlyList<ToolSyncKey> keys)
		{
			await _mutex.WaitAsync();
			try
			{
				_held.RemoveAll(held => keys.Contains(held.Key, SyncKeyComparer.Instance));
				Pulse();
			}
			finally
			{
				_mutex.Release();
			}
		}

		/// <summary>
		/// Removes a waiter that gave up and wakes the rest, so a cancelled or skipped invocation cannot
		/// keep blocking the ones queued behind it.
		/// </summary>
		private async Task DropAsync(Waiter waiter)
		{
			await _mutex.WaitAsync();
			try
			{
				if (_queue.Remove(waiter))
					Pulse();
			}
			finally
			{
				_mutex.Release();
			}
		}

		private void Pulse()
		{
			var signal = _changed;
			_changed = CreateSignal();
			signal.TrySetResult();
		}

		private static TaskCompletionSource CreateSignal() =>
			new(TaskCreationOptions.RunContinuationsAsynchronously);

		/// <summary>
		/// Returns the localized description of what stands in the way, or <see langword="null"/> when
		/// nothing does and the key set may be claimed.
		/// </summary>
		private string? FindBlocker(IReadOnlyList<ToolSyncKey> keys, Waiter waiter)
		{
			foreach (var held in _held)
				foreach (var requested in keys)
				{
					if (Conflicts(held.Key, requested))
						return Describe(held.Owner, held.Key);
				}

			// Fairness: an invocation never overtakes an earlier one it conflicts with, even when the
			// earlier one is not running yet.
			foreach (var earlier in _queue)
			{
				if (ReferenceEquals(earlier, waiter))
					break;

				foreach (var queued in earlier.Keys)
					foreach (var requested in keys)
					{
						if (Conflicts(queued, requested))
							return Describe(earlier.Owner, queued);
					}
			}

			return null;
		}

		private static string Describe(string owner, ToolSyncKey key)
		{
			var target = key.Kind == ToolSyncKeyKind.AllFiles
				? LocalizationManager.LocalizeStatic("tool.status.sync.all_files")
				: key.Value;

			return LocalizationManager.LocalizeStaticFormat("tool.status.sync.waiting", $"{owner} · {target}");
		}

		private static bool Conflicts(ToolSyncKey held, ToolSyncKey requested)
		{
			// Two readers never exclude each other; everything else does.
			if (held.Access == ToolSyncAccess.Read && requested.Access == ToolSyncAccess.Read)
				return false;

			if (held.Kind == ToolSyncKeyKind.AllFiles && requested.Kind != ToolSyncKeyKind.Custom)
				return true;

			if (requested.Kind == ToolSyncKeyKind.AllFiles && held.Kind != ToolSyncKeyKind.Custom)
				return true;

			return (held.Kind, requested.Kind) switch
			{
				(ToolSyncKeyKind.File, ToolSyncKeyKind.File) => PathComparer.Equals(held.Value, requested.Value),
				(ToolSyncKeyKind.Directory, ToolSyncKeyKind.Directory) => PathComparer.Equals(held.Value, requested.Value),
				(ToolSyncKeyKind.Directory, ToolSyncKeyKind.File) => IsBeneathOrEqual(requested.Value, held.Value),
				(ToolSyncKeyKind.File, ToolSyncKeyKind.Directory) => IsBeneathOrEqual(held.Value, requested.Value),
				(ToolSyncKeyKind.Custom, ToolSyncKeyKind.Custom) =>
					string.Equals(held.Value, requested.Value, StringComparison.Ordinal),
				_ => false
			};
		}

		private static bool IsBeneathOrEqual(string path, string directory)
		{
			if (string.IsNullOrEmpty(path) || string.IsNullOrEmpty(directory))
				return PathComparer.Equals(path, directory);

			if (PathComparer.Equals(path, directory))
				return true;

			var prefix = directory.EndsWith(Path.DirectorySeparatorChar) ||
				directory.EndsWith(Path.AltDirectorySeparatorChar)
				? directory
				: directory + Path.DirectorySeparatorChar;

			return path.StartsWith(prefix, PathComparison);
		}

		/// <summary>
		/// Whether two keys point at the same target, ignoring the access. The access decides whether they
		/// exclude each other, not whether they are the same key.
		/// </summary>
		private static bool SameTarget(ToolSyncKey x, ToolSyncKey y)
		{
			if (x.Kind != y.Kind)
				return false;

			return x.Kind is ToolSyncKeyKind.File or ToolSyncKeyKind.Directory
				? PathComparer.Equals(x.Value, y.Value)
				: string.Equals(x.Value, y.Value, StringComparison.Ordinal);
		}

		/// <summary>
		/// A granted key together with the tool that holds it, so a blocked invocation can name its
		/// blocker instead of waiting silently.
		/// </summary>
		private sealed record HeldKey(ToolSyncKey Key, string Owner);

		/// <summary>
		/// A queued invocation: the keys it wants and the tool that wants them.
		/// </summary>
		private sealed class Waiter(IReadOnlyList<ToolSyncKey> keys, string owner)
		{
			/// <summary>
			/// The keys the invocation claims.
			/// </summary>
			public IReadOnlyList<ToolSyncKey> Keys { get; } = keys;

			/// <summary>
			/// The name of the tool, shown to the invocation queued behind it.
			/// </summary>
			public string Owner { get; } = owner;
		}

		private sealed class SyncKeyComparer : IEqualityComparer<ToolSyncKey>
		{
			public static readonly SyncKeyComparer Instance = new();

			public bool Equals(ToolSyncKey x, ToolSyncKey y)
			{
				if (x.Kind != y.Kind || x.Access != y.Access)
					return false;

				return x.Kind is ToolSyncKeyKind.File or ToolSyncKeyKind.Directory
					? PathComparer.Equals(x.Value, y.Value)
					: string.Equals(x.Value, y.Value, StringComparison.Ordinal);
			}

			public int GetHashCode(ToolSyncKey obj)
			{
				var value = obj.Value ?? string.Empty;

				return HashCode.Combine(obj.Kind, obj.Access,
					obj.Kind is ToolSyncKeyKind.File or ToolSyncKeyKind.Directory
						? PathComparer.GetHashCode(value)
						: StringComparer.Ordinal.GetHashCode(value));
			}
		}
	}
}
