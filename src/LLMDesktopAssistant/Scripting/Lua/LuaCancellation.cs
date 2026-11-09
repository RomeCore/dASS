using AsyncLua.Values;

namespace LLMDesktopAssistant.Scripting.Lua
{
	public class LuaCancellation : IDisposable
	{
		private readonly CancellationTokenSource _cts;
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
		
		public void Cancel()
		{
			try
			{
				_cts.Cancel();
			}
			catch { }
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
