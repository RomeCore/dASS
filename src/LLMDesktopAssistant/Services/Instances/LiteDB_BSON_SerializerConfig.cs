using LiteDB;
using LLMDesktopAssistant.Controls.Icons;
using LLMDesktopAssistant.Localization;

namespace LLMDesktopAssistant.Services.Instances
{
	[Service]
	public class LiteDB_BSON_SerializerConfig
	{
		public LiteDB_BSON_SerializerConfig()
		{
			// Вот блять...
			// Вот нахуя эти ебанаты делают так, чтобы сериализация была нестабильной?
			// Из-за вас, бляди, я проебал 500+ рублей на НЕКЕШИРОВАННЫЕ входные токены
			// ибо ВЫ, БЛЯТЬ, ОБРЕЗАЕТЕ ВСЕ СТРОКИ НАХУЙ
			// ПИСЬКИ ЛУЧШЕ СЕБЕ ПООБРЕЗАЙТЕ!!!
			BsonMapper.Global.TrimWhitespace = false;
			BsonMapper.Global.EmptyStringToNull = false;

			BsonMapper.Global.RegisterType<LocaleKeyBase>(key =>
			{
				var doc = new BsonDocument();
				switch (key)
				{
					case ConstLocaleKey:
						doc["type"] = "const";
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
					default:
						return LocaleKey.GetOrCreate(key);
				}
			});

			VisualIconKindBsonSerializer.Register(BsonMapper.Global);
		}
	}
}
