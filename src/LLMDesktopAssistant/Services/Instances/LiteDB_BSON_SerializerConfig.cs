using LiteDB;
using LLMDesktopAssistant.Controls.Icons;
using LLMDesktopAssistant.Localization;

namespace LLMDesktopAssistant.Services.Instances
{
	[Service]
	public class LiteDB_BSON_SerializerConfig
	{
		public LiteDB_BSON_SerializerConfig() => Register(BsonMapper.Global);

		/// <summary>
		/// Registers the app's custom BSON serializers on the given mapper. Called once for
		/// <see cref="BsonMapper.Global"/>; extracted so tests can build a private mapper without touching global state.
		/// </summary>
		public static void Register(BsonMapper mapper)
		{
			// Вот блять...
			// Вот нахуя эти ебанаты делают так, чтобы сериализация была нестабильной?
			// Из-за вас, бляди, я проебал 500+ рублей на НЕКЕШИРОВАННЫЕ входные токены
			// ибо ВЫ, БЛЯТЬ, ОБРЕЗАЕТЕ ВСЕ СТРОКИ НАХУЙ
			// ПИСЬКИ ЛУЧШЕ СЕБЕ ПООБРЕЗАЙТЕ!!!
			mapper.TrimWhitespace = false;
			mapper.EmptyStringToNull = false;

			LocaleKeyBsonSerializer.Register(mapper);
			VisualIconKindBsonSerializer.Register(mapper);
		}
	}
}
