namespace LLMDesktopAssistant.LLM.Services
{
	/// <inheritdoc cref="IChatExecutionTokenService"/>
	[ChatService(typeof(IChatExecutionTokenService))]
	public class ChatExecutionTokenService : Disposable, IChatExecutionTokenService
	{
		private readonly object _gate = new();
		private readonly SortedDictionary<ChatExecutionLevel, CancellationTokenSource> _levels = new();

		private CancellationTokenSource? _root;

		/// <inheritdoc/>
		public CancellationTokenSource? ExecutionCancellationToken => _root;

		/// <inheritdoc/>
		public event Action? ExecutionCancellationTokenChanged;

		/// <inheritdoc/>
		public IDisposable WithToken(ChatExecutionLevel level, CancellationToken inputCt, out CancellationToken cancellationToken)
		{
			if (level == ChatExecutionLevel.None)
				throw new ArgumentOutOfRangeException(nameof(level), "Cannot take the None level.");

			CancellationTokenSource source;
			bool rootCreated = false;

			lock (_gate)
			{
				// If everything was cancelled (e.g. the whole-levels token was cancelled), the current set is dead:
				// drop it so the new level gets a fresh, uncancelled token.
				ResetIfCancelled();

				// Taking a level replaces that level's token and cascades into every narrower level.
				CancelLevelsFrom(level);

				if (_root is null)
				{
					_root = new CancellationTokenSource();
					rootCreated = true;
				}

				var wider = FindNearestWider(level);
				source = wider is null
					? CancellationTokenSource.CreateLinkedTokenSource(inputCt, _root.Token)
					: CancellationTokenSource.CreateLinkedTokenSource(inputCt, _root.Token, wider.Token);

				_levels[level] = source;
			}

			cancellationToken = source.Token;

			if (rootCreated)
				RaiseChanged();

			return new LevelHandle(this, level, source);
		}

		/// <inheritdoc/>
		public bool TryCancel(ChatExecutionLevel level)
		{
			bool releasedRoot;

			lock (_gate)
			{
				if (!_levels.Remove(level, out var source))
					return false;

				CancelLevelsNarrowerThan(level);
				CancelAndDispose(source);

				releasedRoot = ReleaseRootIfIdle();
			}

			if (releasedRoot)
				RaiseChanged();

			return true;
		}

		private bool ReleaseLevel(ChatExecutionLevel level, CancellationTokenSource source)
		{
			bool releasedRoot;

			lock (_gate)
			{
				// Only act if this level still holds our token - it may have been replaced or cancelled already.
				if (_levels.TryGetValue(level, out var current) && ReferenceEquals(current, source))
				{
					_levels.Remove(level);
					CancelLevelsNarrowerThan(level);
				}

				releasedRoot = ReleaseRootIfIdle();
			}

			DisposeOnly(source);
			return releasedRoot;
		}

		private void ResetIfCancelled()
		{
			if (_root is not { IsCancellationRequested: true })
				return;

			var levels = _levels.Values.ToList();
			_levels.Clear();

			var root = _root;
			_root = null;

			foreach (var source in levels)
				DisposeOnly(source);

			DisposeOnly(root);
		}

		private CancellationTokenSource? FindNearestWider(ChatExecutionLevel level)
		{
			// Wider levels have a smaller enum value, so the nearest one is the largest key below `level`.
			CancellationTokenSource? result = null;

			foreach (var (key, source) in _levels)
			{
				if (key >= level)
					break;
				if (!source.IsCancellationRequested)
					result = source;
			}

			return result;
		}

		private void CancelLevelsFrom(ChatExecutionLevel level)
		{
			CancelLevels(key => key >= level);
		}

		private void CancelLevelsNarrowerThan(ChatExecutionLevel level)
		{
			CancelLevels(key => key > level);
		}

		private void CancelLevels(Func<ChatExecutionLevel, bool> predicate)
		{
			// Detach the victims first so that re-entrant cancellations observe a consistent level set.
			var victims = _levels.Where(kv => predicate(kv.Key)).Select(kv => kv.Key).ToList();

			var sources = new List<CancellationTokenSource>(victims.Count);
			foreach (var key in victims)
			{
				sources.Add(_levels[key]);
				_levels.Remove(key);
			}

			foreach (var source in sources)
				CancelAndDispose(source);
		}

		private bool ReleaseRootIfIdle()
		{
			if (_levels.Count > 0 || _root is null)
				return false;

			var root = _root;
			_root = null;
			DisposeOnly(root);
			return true;
		}

		private void RaiseChanged() => ExecutionCancellationTokenChanged?.Invoke();

		private static void CancelAndDispose(CancellationTokenSource source)
		{
			try
			{
				source.Cancel();
			}
			catch (ObjectDisposedException)
			{
			}

			DisposeOnly(source);
		}

		private static void DisposeOnly(CancellationTokenSource? source)
		{
			if (source is null)
				return;

			try
			{
				source.Dispose();
			}
			catch (ObjectDisposedException)
			{
			}
		}

		protected override void Dispose(bool disposing)
		{
			base.Dispose(disposing);

			if (disposing)
			{
				lock (_gate)
				{
					var sources = _levels.Values.ToList();
					_levels.Clear();

					var root = _root;
					_root = null;

					foreach (var source in sources)
						DisposeOnly(source);

					DisposeOnly(root);
				}
			}
		}

		private sealed class LevelHandle(ChatExecutionTokenService owner, ChatExecutionLevel level,
			CancellationTokenSource source) : IDisposable
		{
			private bool _disposed;

			public void Dispose()
			{
				if (_disposed)
					return;
				_disposed = true;

				if (owner.ReleaseLevel(level, source))
					owner.RaiseChanged();
			}
		}
	}
}
