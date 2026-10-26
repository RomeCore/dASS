using System.Numerics;
using LLMDesktopAssistant.Calculation;
using LLMDesktopAssistant.Calculation.Ast;

namespace LLMDesktopAssistant.Tests.Calculation
{
	/// <summary>
	/// AR-0011: the factorial was an integer-only product, so every non-integer argument was
	/// truncated toward zero and the caller got a plausible - but wrong - number.
	/// </summary>
	public class FactorialTests
	{
		[Theory]
		[InlineData("factorial(0)", 1.0)]
		[InlineData("factorial(1)", 1.0)]
		[InlineData("factorial(5)", 120.0)]
		[InlineData("factorial(10)", 3628800.0)]
		[InlineData("0!", 1.0)]
		[InlineData("5!", 120.0)]
		[InlineData("(2+3)!", 120.0)]
		public void IntegerArgument_ReturnsExactProduct(string expression, double expected)
		{
			Assert.Equal(expected, Evaluate(expression).Real, 12);
		}

		[Theory]
		[InlineData("factorial(0.5)", 0.8862269254527580)]
		[InlineData("0.5!", 0.8862269254527580)]
		[InlineData("factorial(1.5)", 1.3293403881791370)]
		[InlineData("factorial(2.5)", 3.3233509704478426)]
		[InlineData("factorial(2.9)", 5.2993297338097065)]
		public void FractionalArgument_IsGammaOfArgumentPlusOne(string expression, double expected)
		{
			Assert.Equal(expected, Evaluate(expression).Real, 12);
		}

		[Theory]
		[InlineData("factorial(-0.5)", 1.7724538509055159)]
		[InlineData("(-0.5)!", 1.7724538509055159)]
		[InlineData("factorial(-1.5)", -3.5449077018110320)]
		[InlineData("factorial(-2.5)", 2.3632718012073550)]
		[InlineData("factorial(-3.5)", -0.9453087204829419)]
		public void NegativeFractionalArgument_UsesTheReflectionFormula(string expression, double expected)
		{
			Assert.Equal(expected, Evaluate(expression).Real, 11);
		}

		[Theory]
		[InlineData("factorial(-1)")]
		[InlineData("factorial(-2)")]
		[InlineData("(-1)!")]
		[InlineData("factorial(0.5 - 1.5)")]
		public void NegativeIntegerArgument_IsUndefined(string expression)
		{
			Assert.True(double.IsNaN(Evaluate(expression).Real));
		}

		[Fact]
		public void BeyondTheDoubleRange_IsInfinity()
		{
			double largest = Evaluate("factorial(170)").Real;
			double beyond = Evaluate("factorial(171)").Real;

			Assert.Equal(7.2574156153079989e306, largest, largest * 1e-12);
			Assert.True(double.IsPositiveInfinity(beyond));
		}

		[Fact]
		public void GammaOfPositiveInfinity_IsInfinity()
		{
			Assert.True(double.IsPositiveInfinity(Evaluate("gamma(inf)").Real));
			Assert.True(double.IsPositiveInfinity(Evaluate("factorial(inf)").Real));
		}

		[Fact]
		public void ComplexArgument_IsGammaOfComplexArgumentPlusOne()
		{
			Complex actual = Evaluate("factorial(1+i)");

			Assert.Equal(0.6529654964201642, actual.Real, 11);
			Assert.Equal(0.3430658398165443, actual.Imaginary, 11);
		}

		[Fact]
		public void BothEntryPoints_AgreeOnACommonArgument()
		{
			// The function call and the postfix operator used to carry two copies of the product.
			Assert.Equal(Evaluate("factorial(2.9)").Real, Evaluate("2.9!").Real, 15);
		}

		[Theory]
		[InlineData("gamma(5)", 24.0)]
		[InlineData("gamma(0.5)", 1.7724538509055159)]
		[InlineData("gamma(-0.5)", -3.5449077018110320)]
		public void Gamma_IsDefinedBetweenTheNegativePolesToo(string expression, double expected)
		{
			Assert.Equal(expected, Evaluate(expression).Real, 10);
		}

		[Fact]
		public void Gamma_KeepsRelativeAccuracyForLargeArguments()
		{
			// Absolute decimals are meaningless at this magnitude; the Lanczos set is good to ~1e-15
			// relative, so the tolerance has to be relative too.
			const double expected = 1133278.3889487856;

			Assert.Equal(expected, Evaluate("gamma(10.5)").Real, expected * 1e-13);
		}

		[Fact]
		public void Gamma_ReflectsOnTheRealPartNotTheMagnitude()
		{
			// |0.4 + 0.3i| is below one half, yet the Lanczos branch is the correct one here.
			Complex actual = Evaluate("gamma(0.4+0.3i)");

			Assert.Equal(1.3419819788320806, actual.Real, 11);
			Assert.Equal(-1.0362830129822300, actual.Imaginary, 11);
		}

		[Fact]
		public void Gamma_OfAComplexArgument_IsNotItsRealPart()
		{
			// gamma used to project onto the real axis and answer Gamma(2) here.
			Complex actual = Evaluate("gamma(2+i)");

			Assert.Equal(0.6529654964201642, actual.Real, 11);
			Assert.Equal(0.3430658398165443, actual.Imaginary, 11);
		}

		[Fact]
		public void Gamma_OfARealArgument_StaysReal()
		{
			Complex actual = Evaluate("gamma(5)");

			Assert.Equal(0, actual.Imaginary);
			Assert.Equal(24.0, actual.Real, 12);
		}

		[Fact]
		public void Compgamma_NoLongerResolves()
		{
			Assert.Throws<MathEvaluationException>(() => Evaluate("compgamma(0.5)"));
		}

		/// <summary>
		/// Parses and evaluates one expression the same way <c>math-calculate</c> does.
		/// </summary>
		/// <param name="expression">The expression to evaluate.</param>
		/// <returns>The resulting complex number.</returns>
		private static Complex Evaluate(string expression)
		{
			MathEntity parsed = MathExpressionParser.Parse(expression);

			return parsed.Evaluate(new MathEvaluationContext()).ToComplexOrThrow();
		}
	}
}
