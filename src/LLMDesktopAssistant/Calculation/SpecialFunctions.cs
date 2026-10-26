using System.Numerics;

namespace LLMDesktopAssistant.Calculation
{
	/// <summary>
	/// Special functions behind the expression evaluator: the gamma function and the factorial it
	/// generalises to non-integer arguments.
	/// </summary>
	public static class SpecialFunctions
	{
		/// <summary>
		/// The shift the Lanczos coefficients below are fitted for; changing one without the other
		/// silently destroys the accuracy.
		/// </summary>
		private const double lanczosG = 7;

		/// <summary>
		/// Lanczos coefficients for <c>g = 7</c>, nine terms - the classical set that reaches about
		/// fifteen significant digits on the real axis.
		/// </summary>
		private static readonly double[] lanczosCoefficients =
		[
			0.99999999999980993, 676.5203681218851, -1259.1392167224028, 771.32342877765313,
			-176.61502916214059, 12.507343278686905, -0.13857109526572012, 9.9843695780195716e-6,
			1.5056327351493116e-7
		];

		/// <summary>
		/// The largest argument whose factorial still fits into a <see cref="double"/>: 171! overflows
		/// to infinity, so above it the honest answer is an infinity, not a product.
		/// </summary>
		private const int maxExactFactorial = 170;

		/// <summary>
		/// Evaluates the gamma function on the real axis, including the stretches between its poles
		/// on the negative half-line where it is perfectly finite.
		/// </summary>
		/// <param name="x">The argument.</param>
		/// <returns>
		/// Γ(x), or <see cref="double.NaN"/> at the poles - the non-positive integers - where the
		/// function is undefined.
		/// </returns>
		public static double Gamma(double x)
		{
			if (double.IsNaN(x) || double.IsNegativeInfinity(x))
				return double.NaN;
			if (double.IsPositiveInfinity(x))
				return double.PositiveInfinity;
			// The poles of gamma are exactly the non-positive integers; the explicit test keeps the
			// reflection branch from turning them into a huge finite value through sin(pi * x).
			if (x <= 0 && x == Math.Floor(x))
				return double.NaN;
			if (x < 0.5)
				return Math.PI / (Math.Sin(Math.PI * x) * Gamma(1 - x));

			return Lanczos(x - 1);
		}

		/// <summary>
		/// Evaluates the gamma function on the complex plane.
		/// </summary>
		/// <param name="z">The argument.</param>
		/// <returns>Γ(z), or a value whose parts are NaN at the poles - the non-positive integers.</returns>
		public static Complex Gamma(Complex z)
		{
			// A real argument takes the real path, so gamma(5) and gamma(5 + 0i) stay the same number
			// instead of drifting apart by the polar form of Complex.Pow.
			if (z.Imaginary == 0)
				return Gamma(z.Real);
			if (double.IsNaN(z.Real) || double.IsNaN(z.Imaginary))
				return new Complex(double.NaN, double.NaN);
			// Reflection keyed on the real part, not the magnitude: the branch cut of the stable
			// Lanczos region sits at Re z = 0.5, whatever the imaginary part is.
			if (z.Real < 0.5)
				return Math.PI / (Complex.Sin(Math.PI * z) * Gamma(1 - z));

			return Lanczos(z - 1);
		}

		/// <summary>
		/// Evaluates the factorial, extended to non-integers through <c>x! = Γ(x + 1)</c>.
		/// </summary>
		/// <param name="value">The argument.</param>
		/// <returns>
		/// x!, or a value whose parts are NaN where the extension has a pole - the negative integers.
		/// </returns>
		public static Complex Factorial(Complex value)
		{
			if (value.Imaginary != 0)
				return Gamma(value + 1);

			double x = value.Real;
			// Integers keep the exact product: the gamma route answers 5! with 119.99999999999997.
			if (x >= 0 && x == Math.Floor(x))
				return IntegerFactorial(x);

			return Gamma(x + 1);
		}

		/// <summary>
		/// Multiplies out a non-negative integer argument, stopping at the first overflow.
		/// </summary>
		/// <param name="x">The argument, already known to be a non-negative integer.</param>
		/// <returns>The product, or <see cref="double.PositiveInfinity"/> once it leaves the double range.</returns>
		private static double IntegerFactorial(double x)
		{
			if (x > maxExactFactorial)
				return double.PositiveInfinity;

			double result = 1;
			for (long i = 2; i <= (long)x; i++)
				result *= i;

			return result;
		}

		/// <summary>
		/// The Lanczos approximation itself, valid where the real part is at least one half.
		/// </summary>
		/// <param name="s">The argument already shifted by one, i.e. the <c>x - 1</c> of the formula.</param>
		/// <returns>Γ(s + 1) for a real <paramref name="s"/>.</returns>
		private static double Lanczos(double s)
		{
			double a = lanczosCoefficients[0];
			for (int i = 1; i < lanczosCoefficients.Length; i++)
				a += lanczosCoefficients[i] / (s + i);

			double t = s + lanczosG + 0.5;
			return Math.Sqrt(2 * Math.PI) * Math.Pow(t, s + 0.5) * Math.Exp(-t) * a;
		}

		/// <summary>
		/// The complex counterpart of <see cref="Lanczos(double)"/>, same coefficients and same domain.
		/// </summary>
		/// <param name="s">The argument already shifted by one.</param>
		/// <returns>Γ(s + 1) for a complex <paramref name="s"/>.</returns>
		private static Complex Lanczos(Complex s)
		{
			Complex a = lanczosCoefficients[0];
			for (int i = 1; i < lanczosCoefficients.Length; i++)
				a += lanczosCoefficients[i] / (s + i);

			Complex t = s + lanczosG + 0.5;
			return Complex.Sqrt(2 * Math.PI) * Complex.Pow(t, s + 0.5) * Complex.Exp(-t) * a;
		}
	}
}
