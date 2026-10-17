using LLMDesktopAssistant.Services;
using LLMDesktopAssistant.Tools.Execution.Domain;

namespace LLMDesktopAssistant.Tools.Execution.Services
{
	/// <summary>
	/// The keyed scope. Keys are typed and hierarchical: a held <see cref="ToolSyncKeyKind.Directory"/>
	/// conflicts with a <see cref="ToolSyncKeyKind.File"/> beneath it, and
	/// <see cref="ToolSyncKeyKind.AllFiles"/> conflicts with every file key. Acquisition is
	/// all-or-nothing under one mutex, so no hold-and-wait and no lock-order deadlock is possible.
	/// </summary>
	/// <remarks>
	/// Path keys are compared OS-aware (case-insensitive on Windows); <see cref="ToolSyncKeyKind.Custom"/>
	/// keys are compared ordinally. The service canonicalizes before acquiring: trailing separators are
	/// trimmed (roots preserved), duplicates are dropped and a key covered by a requested directory is
	/// collapsed. Absolutization belongs to the analyzer, never to this service.
	/// </remarks>
	[Service(typeof(IToolSynchronizationService))]
	public sealed class ToolSynchronizationService : IToolSynchronizationService
	{
		private static readonly StringComparer PathComparer =
			OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

		private static readonly StringComparison PathComparison =
			OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;

		private readonly SemaphoreSlim _mutex = new(1, 1);
		private readonly List<ToolSyncKey> _held = [];
		private TaskCompletionSource _released = CreateSignal();

		/// <inheritdoc/>
		public async Task RunAsync(IToolExecutionContext ctx, Func<Task> inner, CancellationToken ct)
		{
			var keys = Collect(ctx);
			if (keys.Count == 0)
			{
				await inner();
				return;
			}

			await AcquireAsync(keys, ct);
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

				var canonical = new ToolSyncKey(key.Kind, value);
				if (!result.Contains(canonical, SyncKeyComparer.Instance))
					result.Add(canonical);
			}

			// A requested directory covers every key beneath it: holding the directory already covers them.
			result.RemoveAll(candidate =>
				candidate.Kind is ToolSyncKeyKind.File or ToolSyncKeyKind.Directory &&
				result.Any(directory =>
					directory.Kind == ToolSyncKeyKind.Directory &&
					!SyncKeyComparer.Instance.Equals(candidate, directory) &&
					IsBeneathOrEqual(candidate.Value, directory.Value)));

			return result;
		}

		private async Task AcquireAsync(IReadOnlyList<ToolSyncKey> keys, CancellationToken ct)
		{
			while (true)
			{
				Task released;

				await _mutex.WaitAsync(ct);
				try
				{
					if (!Conflicts(keys))
					{
						_held.AddRange(keys);
						return;
					}

					released = _released.Task;
				}
				finally
				{
					_mutex.Release();
				}

				// The signal is broadcast on every release, so a waiter never sleeps on a free key set.
				await released.WaitAsync(ct);
			}
		}

		private async Task ReleaseAsync(IReadOnlyList<ToolSyncKey> keys)
		{
			await _mutex.WaitAsync();
			try
			{
				_held.RemoveAll(held => keys.Contains(held, SyncKeyComparer.Instance));
				Pulse();
			}
			finally
			{
				_mutex.Release();
			}
		}

		private void Pulse()
		{
			var signal = _released;
			_released = CreateSignal();
			signal.TrySetResult();
		}

		private static TaskCompletionSource CreateSignal() =>
			new(TaskCreationOptions.RunContinuationsAsynchronously);

		private bool Conflicts(IReadOnlyList<ToolSyncKey> keys)
		{
			foreach (var held in _held)
				foreach (var requested in keys)
				{
					if (Conflicts(held, requested))
						return true;
				}

			return false;
		}

		private static bool Conflicts(ToolSyncKey held, ToolSyncKey requested)
		{
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

		private sealed class SyncKeyComparer : IEqualityComparer<ToolSyncKey>
		{
			public static readonly SyncKeyComparer Instance = new();

			public bool Equals(ToolSyncKey x, ToolSyncKey y)
			{
				if (x.Kind != y.Kind)
					return false;

				return x.Kind is ToolSyncKeyKind.File or ToolSyncKeyKind.Directory
					? PathComparer.Equals(x.Value, y.Value)
					: string.Equals(x.Value, y.Value, StringComparison.Ordinal);
			}

			public int GetHashCode(ToolSyncKey obj)
			{
				var value = obj.Value ?? string.Empty;

				return HashCode.Combine(obj.Kind, obj.Kind is ToolSyncKeyKind.File or ToolSyncKeyKind.Directory
					? PathComparer.GetHashCode(value)
					: StringComparer.Ordinal.GetHashCode(value));
			}
		}
	}
}
