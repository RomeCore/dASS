using System.Numerics;
using LLMDesktopAssistant.Calculation.Ast;

namespace LLMDesktopAssistant.Calculation
{
    /// <summary>
    /// Numerically solves equations of the form f(x) = 0 using various methods.
    /// Supports both real and complex root finding.
    /// </summary>
    public static class MathEquationSolver
    {
        /// <summary>
        /// Finds a root of the equation f(x) = 0 on the interval [a, b] using the bisection method.
        /// The function must change sign on the interval (f(a) * f(b) &lt; 0).
        /// </summary>
        /// <param name="expression">The expression f(x) as a MathEntity tree.</param>
        /// <param name="variable">The name of the variable to solve for.</param>
        /// <param name="a">Left bound of the search interval.</param>
        /// <param name="b">Right bound of the search interval.</param>
        /// <param name="tolerance">The desired precision. Default is 1e-12.</param>
        /// <param name="maxIterations">Maximum number of iterations. Default is 200.</param>
        /// <returns>The approximate root.</returns>
        public static double SolveBisection(
            MathEntity expression,
            string variable,
            double a,
            double b,
            double tolerance = 1e-12,
            int maxIterations = 200)
        {
            double fa = EvaluateAt(expression, variable, a);

            if (Math.Abs(fa) < tolerance)
            {
                return a;
            }

            double fb = EvaluateAt(expression, variable, b);

            if (Math.Abs(fb) < tolerance)
            {
                return b;
            }

            if (fa * fb > 0)
            {
                throw new MathEvaluationException(
                    $"Function does not change sign on the interval [{a}, {b}]. " +
                    $"f({a}) = {fa}, f({b}) = {fb}. " +
                    "Try a different interval or check if the equation has roots there.");
            }

            for (int iteration = 0; iteration < maxIterations; iteration++)
            {
                double c = (a + b) / 2.0;
                double fc = EvaluateAt(expression, variable, c);

                if (Math.Abs(fc) < tolerance || (b - a) / 2.0 < tolerance)
                {
                    return c;
                }

                if (fa * fc < 0)
                {
                    b = c;
                    fb = fc;
                }
                else
                {
                    a = c;
                    fa = fc;
                }
            }

            return (a + b) / 2.0;
        }

        /// <summary>
        /// Scans the range [rangeStart, rangeEnd] looking for intervals where the function changes sign,
        /// then applies bisection on each such interval.
        /// </summary>
        /// <param name="expression">The expression f(x) as a MathEntity tree.</param>
        /// <param name="variable">The name of the variable to solve for.</param>
        /// <param name="rangeStart">Start of the scan range.</param>
        /// <param name="rangeEnd">End of the scan range.</param>
        /// <param name="scanStep">Step size for scanning. Smaller values find more roots but are slower.</param>
        /// <param name="tolerance">The desired precision. Default is 1e-12.</param>
        /// <returns>An array of found roots (distinct).</returns>
        public static double[] FindRoots(
            MathEntity expression,
            string variable,
            double rangeStart = -100.0,
            double rangeEnd = 100.0,
            double scanStep = 0.1,
            double tolerance = 1e-12)
        {
            var roots = new List<double>();

            double prevX = rangeStart;
            double prevF = EvaluateAt(expression, variable, prevX);

            if (Math.Abs(prevF) < tolerance)
            {
                roots.Add(prevX);
            }

            for (double x = rangeStart + scanStep; x <= rangeEnd; x += scanStep)
            {
                double f = EvaluateAt(expression, variable, x);

                if (Math.Abs(f) < tolerance)
                {
                    // Direct hit
                    AddDistinctRoot(roots, x, tolerance);
                }
                else if (prevF * f < 0)
                {
                    // Sign change detected - root in [prevX, x]
                    double root;
                    try
                    {
                        root = SolveBisection(expression, variable, prevX, x, tolerance);
                    }
                    catch
                    {
                        prevX = x;
                        prevF = f;
                        continue;
                    }
                    AddDistinctRoot(roots, root, tolerance);
                }

                prevX = x;
                prevF = f;
            }

            return roots.ToArray();
        }

        /// <summary>
        /// Convenience method: parses an expression string into a MathEntity, then finds real roots.
        /// </summary>
        public static double[] Solve(string expressionString, string variable = "x")
        {
            var expression = MathExpressionParser.Parse(expressionString);
            return FindRoots(expression, variable);
        }

        /// <summary>
        /// Finds a single complex root of f(z) = 0 using the Newton-Raphson method.
        /// Works for any analytic function, not just polynomials.
        /// </summary>
        /// <param name="expression">The expression f(z) as a MathEntity tree.</param>
        /// <param name="variable">The name of the variable to solve for.</param>
        /// <param name="initialGuess">Starting point on the complex plane.</param>
        /// <param name="tolerance">Desired precision. Default is 1e-12.</param>
        /// <param name="maxIterations">Maximum iterations. Default is 50.</param>
        /// <returns>A complex root near the initial guess.</returns>
        public static Complex SolveNewtonComplex(
            MathEntity expression,
            string variable,
            Complex initialGuess,
            double tolerance = 1e-12,
            int maxIterations = 50)
        {
            const double h = 1e-8;
            Complex z = initialGuess;

            for (int iteration = 0; iteration < maxIterations; iteration++)
            {
                Complex fz = EvaluateAtComplex(expression, variable, z);

                if (fz.Magnitude < tolerance)
                    return z;

                // Numerical derivative via complex step differentiation
                Complex fzPlusH = EvaluateAtComplex(expression, variable, z + h);
                Complex derivative = (fzPlusH - fz) / h;

                if (derivative.Magnitude < 1e-20)
                    throw new MathEvaluationException(
                        $"Derivative near zero at z = {z}. Try a different initial guess.");

                Complex step = fz / derivative;
                z = z - step;

                // If step is tiny, we've converged
                if (step.Magnitude < tolerance)
                    return z;
            }

            return z;
        }

        /// <summary>
        /// Scans a rectangular region of the complex plane to find all roots
        /// using a grid search + Newton refinement.
        /// </summary>
        /// <param name="expression">The expression f(z) as a MathEntity tree.</param>
        /// <param name="variable">The variable name.</param>
        /// <param name="reRange">Half-range for real part (searched in [-reRange, reRange]).</param>
        /// <param name="imRange">Half-range for imaginary part (searched in [-imRange, imRange]).</param>
        /// <param name="gridStep">Step size for the coarse grid search.</param>
        /// <param name="tolerance">Precision for root refinement.</param>
        /// <returns>Array of distinct complex roots.</returns>
        public static Complex[] FindComplexRoots(
            MathEntity expression,
            string variable,
            double reRange = 10.0,
            double imRange = 10.0,
            double gridStep = 0.5,
            double tolerance = 1e-12)
        {
            var candidates = new List<Complex>();

            // Coarse grid scan
            for (double re = -reRange; re <= reRange; re += gridStep)
            {
                for (double im = -imRange; im <= imRange; im += gridStep)
                {
                    Complex z = new Complex(re, im);
                    double fMag = EvaluateAtComplex(expression, variable, z).Magnitude;

                    // If magnitude is small enough, try to refine
                    if (fMag < 1.0)
                    {
                        try
                        {
                            Complex root = SolveNewtonComplex(expression, variable, z, tolerance);

                            // Check if it's a genuine root
                            if (EvaluateAtComplex(expression, variable, root).Magnitude < tolerance)
                            {
                                AddDistinctComplexRoot(candidates, root, tolerance);
                            }
                        }
                        catch
                        {
                            // Ignore failed refinements
                        }
                    }
                }
            }

            return candidates.ToArray();
        }

        /// <summary>
        /// Convenience method: parses expression string and finds complex roots.
        /// </summary>
        public static Complex[] SolveComplex(string expressionString, string variable = "z")
        {
            var expression = MathExpressionParser.Parse(expressionString);
            return FindComplexRoots(expression, variable);
        }

        private static double EvaluateAt(MathEntity expression, string variable, double value)
        {
            var ctx = new MathEvaluationContext();
            ctx.PushScope(new Dictionary<string, MathEntity>
            {
                { variable, new ConstantEntity(new Complex(value, 0)) }
            });

            try
            {
                var result = expression.Evaluate(ctx);
                return result.ToComplexOrThrow().Real;
            }
            finally
            {
                ctx.PopScope();
            }
        }

        private static void AddDistinctRoot(List<double> roots, double candidate, double tolerance)
        {
            // Round to avoid duplicates from floating-point noise
            double rounded = Math.Round(candidate, 10);

            foreach (var existing in roots)
            {
                if (Math.Abs(existing - rounded) < tolerance * 10)
                {
                    return;
                }
            }

            roots.Add(candidate);
        }

        private static Complex EvaluateAtComplex(MathEntity expression, string variable, Complex value)
        {
            var ctx = new MathEvaluationContext();
            ctx.PushScope(new Dictionary<string, MathEntity>
            {
                { variable, new ConstantEntity(value) }
            });

            try
            {
                var result = expression.Evaluate(ctx);
                return result.ToComplexOrThrow();
            }
            finally
            {
                ctx.PopScope();
            }
        }

        private static void AddDistinctComplexRoot(List<Complex> roots, Complex candidate, double tolerance)
        {
            foreach (var existing in roots)
            {
                if ((existing - candidate).Magnitude < tolerance * 10)
                {
                    return;
                }
            }

            roots.Add(candidate);
        }
    }
}
