namespace LLMDesktopAssistant.Prompting.Context
{
	public abstract class PromptContextNativeProvider
	{
		private readonly List<PromptContextInfo> _contexts = [];

		protected void AddContext(PromptContextInfo context)
		{
			// A native section has no frontmatter to declare its kind: it is anchored by construction.
			if (context.Kind is PromptContextProviderKind.Unknown)
				context.Kind = PromptContextProviderKind.Anchored;

			context.OverrideOrder = 1;
			context.Freeze();
			_contexts.Add(context);
		}

		public virtual IEnumerable<PromptContextInfo> GetContexts()
		{
			return _contexts;
		}
	}
}
