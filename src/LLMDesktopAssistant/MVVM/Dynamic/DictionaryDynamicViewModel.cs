namespace LLMDesktopAssistant.MVVM.Dynamic
{
	/// <summary>
	/// Default <see cref="DynamicViewModel"/> implementation backed by an in-memory dictionary.
	/// Reading a member that has not been set returns <see langword="null"/>.
	/// </summary>
	/// <remarks>
	/// Access to the store is guarded by a lock so that Lua-side writes and UI-side (two-way
	/// binding) writes can be interleaved safely; change notifications are raised outside the lock.
	/// </remarks>
	public class DictionaryDynamicViewModel : DynamicViewModel
	{
		private readonly Dictionary<string, object?> _store = [];
		private readonly Lock _lock = new();

		/// <inheritdoc/>
		public override object? GetDynamicMember(string name)
		{
			lock (_lock)
				return _store.GetValueOrDefault(name);
		}

		/// <inheritdoc/>
		public override void SetDynamicMember(string name, object? value)
		{
			lock (_lock)
				_store[name] = value;

			RaisePropertyChangedFor(name);
		}
	}
}
