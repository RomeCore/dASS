using System.Globalization;
using LiteDB;
using LLMDesktopAssistant.Localization;

namespace LLMDesktopAssistant.LLM.MVVM.Additional.Context
{
	[ViewModelFor(typeof(TokenCostView))]
	public class TokenCostViewModel : AdditionalChatData
	{
		public override int Order => 10;

		public required string ModelName { get; init; }

		public required int? InputTokens { get; init; }

		public required int? InputCacheHitTokens { get; init; }

		public required int? InputCacheMissTokens { get; init; }

		public required int? OutputTokens { get; init; }

		public required double TTFT { get; init; }

		public required double GenerationTime { get; init; }

		/// <summary>
		/// Localized label: "Model".
		/// </summary>
		[BsonIgnore]
		public string ModelLabel => LocalizationManager.LocalizeStatic("message.token_cost.model");

		/// <summary>
		/// Localized label: "Input tokens".
		/// </summary>
		[BsonIgnore]
		public string InputTokensLabel => LocalizationManager.LocalizeStatic("message.token_cost.input");

		/// <summary>
		/// Localized label: "Cache hit".
		/// </summary>
		[BsonIgnore]
		public string CacheHitLabel => LocalizationManager.LocalizeStatic("message.token_cost.cache_hit");

		/// <summary>
		/// Localized label: "Cache miss".
		/// </summary>
		[BsonIgnore]
		public string CacheMissLabel => LocalizationManager.LocalizeStatic("message.token_cost.cache_miss");

		/// <summary>
		/// Localized label: "Output tokens".
		/// </summary>
		[BsonIgnore]
		public string OutputTokensLabel => LocalizationManager.LocalizeStatic("message.token_cost.output");

		/// <summary>
		/// Localized label: "TTFT" (Time to First Token).
		/// </summary>
		[BsonIgnore]
		public string TTFTLabel => LocalizationManager.LocalizeStatic("message.token_cost.ttft");

		/// <summary>
		/// Localized label: "Generation time".
		/// </summary>
		[BsonIgnore]
		public string GenerationTimeLabel => LocalizationManager.LocalizeStatic("message.token_cost.generation_time");

		/// <summary>
		/// Formatted input tokens with thousands separator.
		/// </summary>
		[BsonIgnore]
		public string InputTokensFormatted => InputTokens?.ToString("N0", CultureInfo.CurrentCulture) ?? "-";

		/// <summary>
		/// Formatted cache hit tokens (or "-" if unavailable).
		/// </summary>
		[BsonIgnore]
		public string CacheHitFormatted => InputCacheHitTokens?.ToString("N0", CultureInfo.CurrentCulture) ?? "-";

		/// <summary>
		/// Formatted cache miss tokens (or "-" if unavailable).
		/// </summary>
		[BsonIgnore]
		public string CacheMissFormatted => InputCacheMissTokens?.ToString("N0", CultureInfo.CurrentCulture) ?? "-";

		/// <summary>
		/// Formatted output tokens with thousands separator.
		/// </summary>
		[BsonIgnore]
		public string OutputTokensFormatted => OutputTokens?.ToString("N0", CultureInfo.CurrentCulture) ?? "-";

		/// <summary>
		/// Formatted TTFT in seconds (e.g. "1.23s").
		/// </summary>
		[BsonIgnore]
		public string TTFTFormatted => $"{TTFT:F2}s";

		/// <summary>
		/// Formatted generation time in seconds (e.g. "4.56s").
		/// </summary>
		[BsonIgnore]
		public string GenerationTimeFormatted => $"{GenerationTime:F2}s";
	}
}
