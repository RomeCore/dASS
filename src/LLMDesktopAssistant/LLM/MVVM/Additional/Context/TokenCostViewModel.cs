using System.Globalization;
using Avalonia.Controls;
using LiteDB;
using LLMDesktopAssistant.Localization;
using LLMDesktopAssistant.Providers;

namespace LLMDesktopAssistant.LLM.MVVM.Additional.Context
{
	[ViewModelFor(typeof(TokenCostView))]
	public class TokenCostViewModel : AdditionalChatData
	{
		private const double cacheGoodPercent = 70;
		private const double cacheWarningPercent = 30;

		public override int Order => 10;

		public required string ModelName { get; init; }
		public required int? InputTokens { get; init; }
		public required int? CacheCreateTokens { get; init; }
		public required int? InputCacheHitTokens { get; init; }
		public required int? InputCacheMissTokens { get; init; }
		public required int? OutputTokens { get; init; }
		public required double TTFT { get; init; }
		public required double GenerationTime { get; init; }

		[BsonIgnore]
		public string ProviderName => ModelParts.Provider;
		[BsonIgnore]
		public bool HasProvider => !string.IsNullOrWhiteSpace(ProviderName);
		[BsonIgnore]
		public string ModelDisplayName => ModelParts.ModelId;
		[BsonIgnore]
		public IReadOnlyList<string> Modifiers => ModelParts.Modifiers ?? [];
		[BsonIgnore]
		public bool HasModifiers => Modifiers.Count > 0;
		[BsonIgnore]
		public string FirstModifier => HasModifiers ? Modifiers[0] : string.Empty;
		[BsonIgnore]
		public bool HasMoreModifiers => Modifiers.Count > 1;
		[BsonIgnore]
		public string MoreModifiersText => $"+{Modifiers.Count - 1}";

		private ModelReference? _modelNameParts;
		[BsonIgnore]
		public ModelReference ModelParts
		{
			get
			{
				if (_modelNameParts.HasValue)
					return _modelNameParts.Value;

				try
				{
					_modelNameParts = ModelReference.Parse(ModelName);
				}
				catch
				{
					_modelNameParts = new ModelReference(string.Empty, ModelName, []);
				}
				return _modelNameParts.Value;
			}
		}

		[BsonIgnore]
		public bool HasInputTokens => InputTokens > 0;
		[BsonIgnore]
		public bool HasOutputTokens => OutputTokens > 0;
		[BsonIgnore]
		public bool HasTokenCounts => HasInputTokens || HasOutputTokens;
		[BsonIgnore]
		public bool HasBothTokenCounts => HasInputTokens && HasOutputTokens;

		[BsonIgnore]
		public bool HasCacheHit => InputCacheHitTokens > 0;
		[BsonIgnore]
		public bool HasCacheMiss => InputCacheMissTokens > 0;
		[BsonIgnore]
		public bool HasCacheCreate => CacheCreateTokens > 0;
		[BsonIgnore]
		public bool HasCacheBlock => HasCacheHit || HasCacheMiss || HasCacheCreate;
		[BsonIgnore]
		public bool HasCachePercent => HasCacheHit || HasCacheMiss;
		[BsonIgnore]
		public bool IsCacheJustCreated => !HasCachePercent && HasCacheCreate;

		[BsonIgnore]
		public double CacheHitPercent => CacheShare(InputCacheHitTokens) * 100;
		[BsonIgnore]
		public string CacheHitPercentText => $"{CacheHitPercent.ToString("F2", CultureInfo.CurrentCulture)}%";

		[BsonIgnore]
		public GridLength CacheHitLength => new(CacheShare(InputCacheHitTokens), GridUnitType.Star);
		[BsonIgnore]
		public GridLength CacheMissLength => new(CacheShare(InputCacheMissTokens), GridUnitType.Star);
		[BsonIgnore]
		public GridLength CacheCreateLength => new(CacheShare(CacheCreateTokens), GridUnitType.Star);

		[BsonIgnore]
		public bool IsCacheGood => CacheHitPercent >= cacheGoodPercent;
		[BsonIgnore]
		public bool IsCacheWarning => CacheHitPercent is >= cacheWarningPercent and < cacheGoodPercent;
		[BsonIgnore]
		public bool IsCacheBad => HasCachePercent && CacheHitPercent < cacheWarningPercent;

		private int CacheTotal => (InputCacheHitTokens ?? 0) + (InputCacheMissTokens ?? 0) + (CacheCreateTokens ?? 0);
		private double CacheShare(int? tokens) => CacheTotal > 0 ? (tokens ?? 0) / (double)CacheTotal : 0;

		[BsonIgnore]
		public string InputTokensFormatted => FormatTokens(InputTokens);
		[BsonIgnore]
		public string CacheCreateFormatted => FormatTokens(CacheCreateTokens);
		[BsonIgnore]
		public string CacheHitFormatted => FormatTokens(InputCacheHitTokens);
		[BsonIgnore]
		public string CacheMissFormatted => FormatTokens(InputCacheMissTokens);
		[BsonIgnore]
		public string OutputTokensFormatted => FormatTokens(OutputTokens);
		[BsonIgnore]
		public string TTFTValue => TTFT.ToString("F2", CultureInfo.CurrentCulture);
		[BsonIgnore]
		public string GenerationTimeValue => GenerationTime.ToString("F2", CultureInfo.CurrentCulture);

		private static string FormatTokens(int? tokens) => tokens?.ToString("N0", CultureInfo.CurrentCulture) ?? "-";
	}
}
