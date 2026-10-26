using System.Numerics;
using LLMDesktopAssistant.Calculation;
using LLMDesktopAssistant.Calculation.Ast;

namespace LLMDesktopAssistant.Tests.Calculation
{
	/// <summary>
	/// AR-0010: the folds seeded themselves with <see cref="Complex.Zero"/>, so the smallest possible
	/// magnitude sat in the compared set as if it were an argument and `minmag` always answered zero.
	/// </summary>
	public class MagnitudeTests
	{
		[Theory]
		[InlineData("minmag(3+4i, 1+1i)", 1.0, 1.0)]
		[InlineData("minmag(10+10i, 0.5+0.5i)", 0.5, 0.5)]
		[InlineData("minmag(5, 3, 4)", 3.0, 0.0)]
		[InlineData("minmag(-2, 4)", -2.0, 0.0)]
		[InlineData("minmag(3+4i)", 3.0, 4.0)]
		public void Minmag_ReturnsTheArgumentOfSmallestMagnitude(string expression, double real, double imaginary)
		{
			Complex actual = Evaluate(expression);

			Assert.Equal(real, actual.Real, 12);
			Assert.Equal(imaginary, actual.Imaginary, 12);
		}

		[Theory]
		[InlineData("maxmag(3+4i, 1+1i)", 3.0, 4.0)]
		[InlineData("maxmag(10+10i, 0.5+0.5i)", 10.0, 10.0)]
		[InlineData("maxmag(5, 3, 4)", 5.0, 0.0)]
		[InlineData("maxmag(-5, 3)", -5.0, 0.0)]
		[InlineData("maxmag(3+4i)", 3.0, 4.0)]
		public void Maxmag_ReturnsTheArgumentOfLargestMagnitude(string expression, double real, double imaginary)
		{
			Complex actual = Evaluate(expression);

			Assert.Equal(real, actual.Real, 12);
			Assert.Equal(imaginary, actual.Imaginary, 12);
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
