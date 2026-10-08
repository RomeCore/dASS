using System.Text.Json;
using LiteDB;
using LLMDesktopAssistant.Localization;
using LLMDesktopAssistant.Utils.Json;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace LLMDesktopAssistant.Tests.Localization;

public class LocaleKeySerializationTests
{
	private static JsonSerializerOptions JsonOptions() => new() { Converters = { new JsonLocaleKeyConverter() } };

	private static BsonMapper BsonMapper()
	{
		var mapper = new BsonMapper();
		LocaleKeyBsonSerializer.Register(mapper);
		return mapper;
	}

	[Fact]
	public void Json_RoundTrips_DefaultKey()
	{
		var options = JsonOptions();
		var json = JsonSerializer.Serialize<LocaleKeyBase>(Locale.GetKey("test.hello"), options);

		var back = JsonSerializer.Deserialize<LocaleKeyBase>(json, options);

		Assert.IsType<LocaleKey>(back);
		Assert.Equal("test.hello", back!.Key);
	}

	[Fact]
	public void Json_RoundTrips_ConstKey()
	{
		var options = JsonOptions();
		var json = JsonSerializer.Serialize<LocaleKeyBase>(Locale.GetConstKey("raw text"), options);

		var back = JsonSerializer.Deserialize<LocaleKeyBase>(json, options);

		Assert.IsType<ConstLocaleKey>(back);
		Assert.Equal("raw text", back!.Key);
	}

	[Fact]
	public void Json_RoundTrips_FormattedKey_WithArgs()
	{
		var options = JsonOptions();
		var json = JsonSerializer.Serialize<LocaleKeyBase>(Locale.GetFormattedKey("test.count", "42", null), options);

		var back = Assert.IsType<LocaleFormattedKey>(JsonSerializer.Deserialize<LocaleKeyBase>(json, options));

		Assert.Equal("test.count", back.Key);
		Assert.Equal(new string?[] { "42", null }, back.FormatArgs.ToArray());
	}

	[Fact]
	public void Bson_RoundTrips_FormattedKey_WithArgs()
	{
		var mapper = BsonMapper();
		var bson = mapper.Serialize(typeof(LocaleKeyBase), Locale.GetFormattedKey("test.count", "42", null));

		var back = Assert.IsType<LocaleFormattedKey>(mapper.Deserialize(typeof(LocaleKeyBase), bson));

		Assert.Equal("test.count", back.Key);
		Assert.Equal(new string?[] { "42", null }, back.FormatArgs.ToArray());
	}

	[Fact]
	public void Bson_RoundTrips_ConstAndDefaultKeys()
	{
		var mapper = BsonMapper();

		var constBack = mapper.Deserialize(typeof(LocaleKeyBase),
			mapper.Serialize(typeof(LocaleKeyBase), Locale.GetConstKey("x")));
		var defaultBack = mapper.Deserialize(typeof(LocaleKeyBase),
			mapper.Serialize(typeof(LocaleKeyBase), Locale.GetKey("test.hello")));

		Assert.IsType<ConstLocaleKey>(constBack);
		Assert.IsType<LocaleKey>(defaultBack);
		Assert.Equal("test.hello", ((LocaleKeyBase)defaultBack!).Key);
	}
}
