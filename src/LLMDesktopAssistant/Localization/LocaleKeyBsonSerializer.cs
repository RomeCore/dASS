using System.Collections.Immutable;
using LiteDB;

namespace LLMDesktopAssistant.Localization
{
	/// <summary>
	/// Registers the LiteDB/BSON representation of <see cref="LocaleKeyBase"/>: a discriminated object so a
	/// persisted key survives a restart — <c>{ "type": "default" | "const" | "formatted", "key": "…", "args": […] }</c>
	/// (<c>args</c> only for <see cref="LocaleFormattedKey"/>). Kept in lock-step with the JSON representation in
	/// <c>JsonLocaleKeyConverter</c>.
	/// </summary>
	public static class LocaleKeyBsonSerializer
	{
		/// <summary>Registers the serializer on the given mapper.</summary>
		public static void Register(BsonMapper mapper)
		{
			mapper.RegisterType<LocaleKeyBase>(key =>
			{
				var doc = new BsonDocument();
				switch (key)
				{
					case ConstLocaleKey:
						doc["type"] = "const";
						break;
					case LocaleFormattedKey formatted:
						doc["type"] = "formatted";
						doc["args"] = new BsonArray(formatted.FormatArgs.Select(arg =>
							arg is null ? BsonValue.Null : new BsonValue(arg)));
						break;
					case LocaleKey:
					default:
						doc["type"] = "default";
						break;
				}
				doc["key"] = key.Key;
				return doc;
			}, bson =>
			{
				if (bson.IsNull)
					return null!;
				var doc = bson.AsDocument;
				var type = doc["type"].AsString;
				var key = doc["key"].AsString;
				switch (type)
				{
					case "const":
						return new ConstLocaleKey(key);
					case "formatted":
						var args = doc["args"].AsArray
							.Select(arg => arg.IsNull ? null : arg.AsString)
							.ToImmutableArray();
						return new LocaleFormattedKey(key, args);
					default:
						return LocaleKey.GetOrCreate(key);
				}
			});
		}
	}
}
