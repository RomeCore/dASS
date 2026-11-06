namespace LLMDesktopAssistant.Hooks
{
	public enum HookExecutionFlow
	{
		/// <summary>
		/// Hook runs in background asyncronously (in parallel with other hooks), and does not wait for completion.
		/// </summary>
		FireAndForget,

		/// <summary>
		/// Hook runs in parallel with other hooks, all parallel hooks are awaited by dispatcher before returning.
		/// Hook results are ignored, and hooks will not receive results from other hooks.
		/// </summary>
		Parallel,

		/// <summary>
		/// Hook runs sequentially, and waits for completion before moving on to the next hook.
		/// Hook can return a result, which is then applied to the next hook in the chain.
		/// </summary>
		Sequential
	}
}
