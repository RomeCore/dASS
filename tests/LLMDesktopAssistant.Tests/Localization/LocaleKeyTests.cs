using LLMDesktopAssistant.Localization;

namespace LLMDesktopAssistant.Tests.Localization;

public class LocaleKeyTests
{
	private sealed class TestLocalizationManager : LocalizationManager
	{
		private readonly Dictionary<string, string> _values = new(StringComparer.Ordinal)
		{
			["test.hello"] = "Hello",
			["test.count"] = "Count: {0}"
		};

		public override string? TryLocalize(string key) => _values.GetValueOrDefault(key);

		public override IEnumerable<string> GetAvailableLanguages() => ["en-US", "ru-RU"];

		protected override bool TryChangeLanguage(string language) => true;
	}

	private static readonly TestLocalizationManager Manager = new();

	static LocaleKeyTests()
	{
		LocalizationManager.SetOverrideManager(Manager);
	}

	[Fact]
	public void GetOrCreate_ReturnsCachedInstanceForSameKey()
	{
		var first = Locale.GetKey("test.hello");
		var second = Locale.GetKey("test.hello");

		Assert.Same(first, second);
	}

	[Fact]
	public void GetOrCreate_ReturnsDifferentInstancesForDifferentKeys()
	{
		var first = Locale.GetKey("test.hello");
		var second = Locale.GetKey("test.count");

		Assert.NotSame(first, second);
	}

	[Fact]
	public void Value_ReturnsLocalizedString()
	{
		var key = Locale.GetKey("test.hello");

		Assert.Equal("Hello", key.Value);
	}

	[Fact]
	public void Value_ReturnsKeyWhenNotLocalized()
	{
		var key = Locale.GetKey("test.missing");

		Assert.Equal("test.missing", key.Value);
	}

	[Fact]
	public void Format_FormatsLocalizedValue()
	{
		var key = Locale.GetKey("test.count");

		Assert.Equal("Count: 42", key.Format(42));
	}

	[Fact]
	public void ToString_ReturnsLocalizedValue()
	{
		var key = Locale.GetKey("test.hello");

		Assert.Equal("Hello", key.ToString());
	}

	[Fact]
	public void Equals_ComparesByKey()
	{
		var first = Locale.GetKey("test.hello");
		var second = Locale.GetKey("test.hello");

		Assert.Equal(first, second);
		Assert.Equal(first.GetHashCode(), second.GetHashCode());
	}

	[Fact]
	public void LanguageChange_RaisesPropertyChangedForValue()
	{
		var key = Locale.GetKey("test.hello");
		var raised = false;

		key.PropertyChanged += (_, e) =>
		{
			if (e.PropertyName == nameof(LocaleKey.Value))
				raised = true;
		};

		Manager.CurrentLanguage = string.Empty;
		Manager.CurrentLanguage = "ru-RU";

		Assert.True(raised);
	}

	[Fact]
	public void FormattedKey_ValueFormatsTemplateWithArgs()
	{
		var key = Locale.GetFormattedKey("test.count", "42");

		Assert.Equal("Count: 42", key.Value);
	}

	[Fact]
	public void FormattedKey_IsNotCachedByFacade_ButComparesByKeyAndArgs()
	{
		var first = Locale.GetFormattedKey("test.count", "1");
		var second = Locale.GetFormattedKey("test.count", "1");

		Assert.NotSame(first, second);
		Assert.Equal(first, second);
		Assert.Equal(first.GetHashCode(), second.GetHashCode());
	}

	[Fact]
	public void FormattedKey_EqualsComparesKeyAndArgs()
	{
		Assert.NotEqual(Locale.GetFormattedKey("test.count", "1"), Locale.GetFormattedKey("test.count", "2"));
		Assert.NotEqual(Locale.GetFormattedKey("test.count", "1"), Locale.GetFormattedKey("test.missing", "1"));
		Assert.NotEqual(Locale.GetFormattedKey("test.count", "1", null), Locale.GetFormattedKey("test.count", "1"));
	}

	[Fact]
	public void FormattedKey_MissingKey_FallsBackToKey()
	{
		var key = Locale.GetFormattedKey("test.missing", "x");

		Assert.Equal("test.missing", key.Value);
	}

	[Fact]
	public void FormattedKey_LanguageChange_RaisesPropertyChangedForValue()
	{
		var key = Locale.GetFormattedKey("test.count", "7");
		var raised = false;

		key.PropertyChanged += (_, e) =>
		{
			if (e.PropertyName == nameof(LocaleKey.Value))
				raised = true;
		};

		Manager.CurrentLanguage = Manager.CurrentLanguage == "ru-RU" ? "en-US" : "ru-RU";

		Assert.True(raised);
	}
}
