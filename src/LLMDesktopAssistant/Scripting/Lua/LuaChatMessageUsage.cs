using LLMDesktopAssistant.LLM.MVVM.Additional.Context;

namespace LLMDesktopAssistant.Scripting.Lua
{
	public class LuaChatMessageUsage
	{
		private readonly TokenCostViewModel _tokenCostViewModel;

		public string ModelName => _tokenCostViewModel.ModelName;

		public int? InputTokens => _tokenCostViewModel.InputTokens;

		public int? CacheCreateTokens => _tokenCostViewModel.CacheCreateTokens;

		public int? InputCacheHitTokens => _tokenCostViewModel.InputCacheHitTokens;

		public int? InputCacheMissTokens => _tokenCostViewModel.InputCacheMissTokens;

		public int? OutputTokens => _tokenCostViewModel.OutputTokens;

		public double TimeToFirstToken => _tokenCostViewModel.TTFT;

		public double GenerationTime => _tokenCostViewModel.GenerationTime;

		public LuaChatMessageUsage(TokenCostViewModel tokenCostViewModel)
		{
			_tokenCostViewModel = tokenCostViewModel;
		}
	}
}
