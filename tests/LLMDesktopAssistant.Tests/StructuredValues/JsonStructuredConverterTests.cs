using System.Collections.Immutable;
using System.Text.Json.Nodes;
using LLMDesktopAssistant.StructuredValues.Const;
using LLMDesktopAssistant.StructuredValues.Converters;

namespace LLMDesktopAssistant.Tests.StructuredValues
{
	public class JsonStructuredConverterTests
	{
		[Fact]
		public void FiniteNumbers_StayNumbers()
		{
			var node = new ConstNodeNumberValue { Value = 42 }.ToJsonNode();

			Assert.Equal("42", node!.ToJsonString());
			Assert.Equal(42, Assert.IsType<ConstNodeNumberValue>(node.ToConstNodeValue()).Value);
		}

		[Theory]
		[InlineData(double.NaN, "NaN")]
		[InlineData(double.PositiveInfinity, "Infinity")]
		[InlineData(double.NegativeInfinity, "-Infinity")]
		public void NonFiniteNumbers_TravelAsStrings(double value, string spelling)
		{
			var node = new ConstNodeNumberValue { Value = value }.ToJsonNode();

			Assert.Equal($"\"{spelling}\"", node!.ToJsonString());
		}

		[Theory]
		[InlineData("NaN")]
		[InlineData("Infinity")]
		[InlineData("-Infinity")]
		public void NonFiniteSpellings_ReadBackAsNumbers(string spelling)
		{
			var value = JsonNode.Parse($"\"{spelling}\"").ToConstNodeValue();

			Assert.True(double.IsNaN(Assert.IsType<ConstNodeNumberValue>(value).Value)
				|| double.IsInfinity(Assert.IsType<ConstNodeNumberValue>(value).Value));
		}

		[Fact]
		public void NonFiniteNumbers_RoundTripThroughAnObject()
		{
			var source = new ConstNodeDictionaryValue
			{
				Items = new Dictionary<string, ConstNodeValue>
				{
					["a"] = new ConstNodeNumberValue { Value = double.NaN },
					["b"] = new ConstNodeNumberValue { Value = 1.5 },
				}.ToImmutableDictionary()
			};

			var json = source.ToJsonNode()!.ToJsonString();
			var restored = Assert.IsType<ConstNodeDictionaryValue>(JsonNode.Parse(json).ToConstNodeValue());

			Assert.True(double.IsNaN(Assert.IsType<ConstNodeNumberValue>(restored.Items["a"]).Value));
			Assert.Equal(1.5, Assert.IsType<ConstNodeNumberValue>(restored.Items["b"]).Value);
		}
	}
}
