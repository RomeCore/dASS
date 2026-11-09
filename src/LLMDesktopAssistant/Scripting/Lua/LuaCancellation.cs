using AsyncLua.Values;

namespace LLMDesktopAssistant.Scripting.Lua
{
	public class LuaCancellation : IDisposable
	{
		private readonly CancellationTokenSource _cts;
		private readonly Lock _lock = new();
		private bool _disposed = false;

		[LuaHidden]
		public CancellationTokenSource Cts => _cts;

		public bool Cancellable => !_cts.IsCancellationRequested;

		public bool CancellationRequested => _cts.IsCancellationRequested;

		public LuaCancellation(CancellationTokenSource cts)
		{
			_cts = cts;
		}

		[LuaHidden]
		public void Dispose()
		{
			lock (_lock)
			{
				if (_disposed)
					return;
				_disposed = true;
				try
				{
					_cts.Cancel();
				}
				catch { }
				_cts.Dispose();
			}
		}
		
		/// <summary>
		/// Fires the handle now.
		/// </summary>
		/// <returns>
		/// <see langword="true"/> when this call fired it; <see langword="false"/> when it had already
		/// fired or was closed.
		/// </returns>
		public bool Cancel()
		{
			lock (_lock)
			{
				if (_disposed || _cts.IsCancellationRequested)
					return false;

				try
				{
					_cts.Cancel();
					return true;
				}
				catch
				{
					return false;
				}
			}
		}

		public void CancelAfter(int millisecondsDelay)
		{
			try
			{
				_cts.CancelAfter(millisecondsDelay);
			}
			catch { }
		}

		public void Close()
		{
			Dispose();
		}

		/// <summary>
		/// Wraps the handle into an <c>cancellation</c> userdata.
		/// </summary>
		/// <param name="cancellation">The cancellation handle to wrap.</param>
		[LuaHidden]
		public static LuaUserData Wrap(LuaCancellation cancellation)
			=> new(cancellation, "cancellation")
			{
				Metatable = UserDataMetatableGenerator.GetOrCreate(typeof(LuaCancellation))
			};
	}
}
