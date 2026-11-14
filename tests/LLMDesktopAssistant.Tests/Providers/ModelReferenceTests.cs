using LLMDesktopAssistant.Providers;

namespace LLMDesktopAssistant.Tests.Providers;

public class ModelReferenceTests
{
	[Fact]
	public void Parse_WithoutModifier_ReturnsProviderAndModel()
	{
		var reference = ModelReference.Parse("DeepSeek$deepseek-v4-flash");

		Assert.Equal("DeepSeek", reference.Provider);
		Assert.Equal("deepseek-v4-flash", reference.ModelId);
		Assert.Empty(reference.Modifiers!);
	}

	[Fact]
	public void Parse_WithModifier_ReturnsModifier()
	{
		var reference = ModelReference.Parse("DeepSeek$deepseek-v4-flash$Max");

		Assert.Equal("DeepSeek", reference.Provider);
		Assert.Equal("deepseek-v4-flash", reference.ModelId);
		Assert.Equal("Max", Assert.Single(reference.Modifiers!));
	}

	[Fact]
	public void Parse_WithSeveralModifiers_ReturnsAllOfThem()
	{
		var reference = ModelReference.Parse("DeepSeek$deepseek-v4-flash$Max,Fast");

		Assert.Equal(["Max", "Fast"], reference.Modifiers!);
	}

	[Fact]
	public void ToString_WithoutModifier_ReturnsProviderModel()
	{
		var reference = new ModelReference("DeepSeek", "deepseek-v4-flash", null);

		Assert.Equal("DeepSeek$deepseek-v4-flash", reference.ToString());
	}

	[Fact]
	public void ToString_WithModifier_ReturnsProviderModelModifier()
	{
		var reference = new ModelReference("DeepSeek", "deepseek-v4-flash", ["Max"]);

		Assert.Equal("DeepSeek$deepseek-v4-flash$Max", reference.ToString());
	}

	[Fact]
	public void ToString_WithSeveralModifiers_JoinsThemWithCommas()
	{
		var reference = new ModelReference("DeepSeek", "deepseek-v4-flash", ["Max", "Fast"]);

		Assert.Equal("DeepSeek$deepseek-v4-flash$Max,Fast", reference.ToString());
	}

	[Theory]
	[InlineData("")]
	[InlineData("   ")]
	[InlineData(null)]
	[InlineData("DeepSeek")]
	[InlineData("DeepSeek$")]
	[InlineData("$deepseek-v4-flash")]
	[InlineData("DeepSeek$deepseek-v4-flash$")]
	[InlineData("DeepSeek$model$Max$Extra")]
	public void TryParse_InvalidInput_ReturnsFalse(string? fullName)
	{
		var result = ModelReference.TryParse(fullName!, out _);

		Assert.False(result);
	}

	[Theory]
	[InlineData("DeepSeek$deepseek-v4-flash", false)]
	[InlineData("DeepSeek$deepseek-v4-flash$Max", true)]
	[InlineData("DeepSeek$deepseek-v4-flash$Max,Fast", true)]
	[InlineData("OpenAI$gpt-4o$Fast", true)]
	public void TryParse_ValidInput_ReturnsTrue(string fullName, bool hasModifier)
	{
		var result = ModelReference.TryParse(fullName, out var reference);

		Assert.True(result);
		Assert.Equal(hasModifier, reference.Modifiers is { Count: > 0 });
	}

	[Fact]
	public void Parse_InvalidInput_Throws()
	{
		Assert.Throws<ArgumentException>(() => ModelReference.Parse("invalid"));
	}

	[Fact]
	public void Parse_ToString_RoundTrip()
	{
		var original = new ModelReference("DeepSeek", "deepseek-v4-flash", ["Max", "Fast"]);

		var parsed = ModelReference.Parse(original.ToString());

		// The modifier list compares by reference inside the record struct, so the parts are compared one by one.
		Assert.Equal(original.Provider, parsed.Provider);
		Assert.Equal(original.ModelId, parsed.ModelId);
		Assert.Equal(original.Modifiers, parsed.Modifiers);
	}
}
