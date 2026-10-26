using System.ComponentModel;
using System.Globalization;
using System.Numerics;
using LLMDesktopAssistant.Calculation;
using LLMDesktopAssistant.Calculation.Ast;
using LLMDesktopAssistant.Localization;
using LLMDesktopAssistant.Tools.Execution.Domain;
using LLMDesktopAssistant.Tools.Execution.Support;
using RCParsing;

namespace LLMDesktopAssistant.Tools.Implementations
{
	[ToolModule(chatScoped: false)]
	public class MathematicsToolModule : ToolModule
	{
		public MathematicsToolModule()
		{
			AddTool(new ToolInitializationInfo
			{
				Executor = Calculate,
				Name = "math-calculate",
				Description = """
					Evaluate a mathematical expression. Examples:

					1 + 2 * -(3^2 / e)
					sin(pi/2) + cos(pi/3)
					integral(x^2 + 1, x, 0, 10)
					derivative(x^2 + 1, x, 5)
					sin(9+2i)

					All supported constants:

					NaN, pi, inf, eps, phi, tau, g, e, c, gamma

					All supported functions:

					Normal:

					asinh, acosh, tan, atanh, atan2, cbrt, sign, floor, ceil, round, trunc, mod,
					integral, derivative

					Complex:

					mag, conjugate, minmag, maxmag

					Normal and complex:

					sin, sinh, asin, cos, cosh, acos, tanh, atan,
					sind, cosd, tand, asind, acosd, atand,
					ln, log, log2, log10, logb, exp, pow, sqrt,
					abs, min, max, gamma, factorial
					""",
				NameKey = Locale.GetKey("tool.name.math-calculate"),
				DescriptionKey = Locale.GetKey("tool.description.math-calculate"),
				CategoryKey = Locale.GetKey("tool.category.mathematics")
			});

			AddTool(new ToolInitializationInfo
			{
				Executor = Solve,
				Name = "math-solve",
				Description = """
					Solve an equation numerically for a given variable using the bisection method.
					Finds real roots of f(x) = 0 within a search range.

					Examples:

					solve(x^2 - 4 = 0, x)
					solve(sin(x) - 0.5, x, -10, 10)
					solve(x^3 - 2*x - 5 = 0, x)

					The equation can be written as 'expression = 0' or just 'expression'.
					If no range is specified, it scans from -100 to 100 by default.

					For complex roots, use 'math-solve_complex'.
					""",
				NameKey = Locale.GetKey("tool.name.math-solve"),
				DescriptionKey = Locale.GetKey("tool.description.math-solve"),
				CategoryKey = Locale.GetKey("tool.category.mathematics")
			});

			AddTool(new ToolInitializationInfo
			{
				Executor = SolveComplex,
				Name = "math-solve_complex",
				Description = """
					Solve an equation numerically for complex roots using Newton's method.
					Scans a rectangular region of the complex plane and refines roots.

					Examples:

					solve_complex(x^2 + 1 = 0, z)
					solve_complex(x^3 - 1 = 0, x, -5, 5, -5, 5, 0.5)
					solve_complex(x^2 + x + 1 = 0, x)

					The equation can be written as 'expression = 0' or 'expression'.
					Scans the region [-reRange, reRange] x [-imRange, imRange] with given grid step.
					""",
				NameKey = Locale.GetKey("tool.name.math-solve_complex"),
				DescriptionKey = Locale.GetKey("tool.description.math-solve_complex"),
				CategoryKey = Locale.GetKey("tool.category.mathematics")
			});
		}

		private ToolExecutionResult Calculate(IToolExecutionContext ctx,
			[Description("Expression to evaluate")] string expression)
		{
			try
			{
				var expressionEntity = MathExpressionParser.Parse(expression);
				var evalContext = new MathEvaluationContext();
				var resultEntity = expressionEntity.Evaluate(evalContext);
				var result = resultEntity.ToComplexOrThrow();
				var formatted = FormatComplex(result);

				ctx.Call.SetResult(formatted);
				ctx.Call.SetStatus(Material.Icons.MaterialIconKind.Abacus, $"`{expression}` = `{formatted}`");
				return ToolExecutionResult.Succeeded;
			}
			catch (MathEvaluationException mex)
			{
				ctx.Call.SetResult("Error evaluating expression: " + mex.Message);
				ctx.Call.SetStatus(Material.Icons.MaterialIconKind.Abacus, $"`{expression}`");
				return ToolExecutionResult.Failed;
			}
			catch (ParsingException pex)
			{
				ctx.Call.SetResult("Error parsing expression: " + pex.Message);
				ctx.Call.SetStatus(Material.Icons.MaterialIconKind.Abacus, $"`{expression}`");
				return ToolExecutionResult.Failed;
			}
		}

		private ToolExecutionResult Solve(IToolExecutionContext ctx,
			[Description("Equation to solve. Examples: 'x^2 - 4 = 0', 'sin(x) - 0.5', 'x^3 - 2*x - 5 = 0'")] string equation,
			[Description("Variable to solve for. Default: 'x'")] string variable = "x",
			[Description("Left bound of the search range. Default: -100")] double rangeStart = -100.0,
			[Description("Right bound of the search range. Default: 100")] double rangeEnd = 100.0,
			[Description("Step size for scanning. Smaller values find more roots but are slower. Default: 0.1")] double scanStep = 0.1)
		{
			try
			{
				// Parse the equation: support "expr = 0" or just "expr"
				string expressionStr = equation;
				if (equation.Contains('='))
				{
					var parts = equation.Split('=', 2);
					expressionStr = $"({parts[0].Trim()}) - ({parts[1].Trim()})";
				}

				var expression = MathExpressionParser.Parse(expressionStr);
				var roots = MathEquationSolver.FindRoots(expression, variable, rangeStart, rangeEnd, scanStep);

				if (roots.Length == 0)
				{
					ctx.Call.SetResult("No real roots found in the specified range. Try 'math-solve-complex' for complex roots.");
					ctx.Call.SetStatus(Material.Icons.MaterialIconKind.Abacus,
						LocalizationManager.LocalizeStaticFormat("tool.status.math-solve.no_roots", $"`{equation}`"));
					return ToolExecutionResult.Succeeded;
				}

				var formatted = string.Join(", ", roots.Select(r => r.ToString("G", CultureInfo.InvariantCulture)));
				ctx.Call.SetResult($"Roots found for '{variable}': {formatted}");
				ctx.Call.SetStatus(Material.Icons.MaterialIconKind.Abacus,
					LocalizationManager.LocalizeStaticFormat("tool.status.math-solve.status", $"`{equation}`", roots.Length));
				return ToolExecutionResult.Succeeded;
			}
			catch (MathEvaluationException mex)
			{
				ctx.Call.SetResult("Error evaluating equation: " + mex.Message);
				ctx.Call.SetStatus(Material.Icons.MaterialIconKind.Abacus, $"`{equation}`");
				return ToolExecutionResult.Failed;
			}
			catch (ParsingException pex)
			{
				ctx.Call.SetResult("Error parsing equation: " + pex.Message);
				ctx.Call.SetStatus(Material.Icons.MaterialIconKind.Abacus, $"`{equation}`");
				return ToolExecutionResult.Failed;
			}
		}

		private ToolExecutionResult SolveComplex(IToolExecutionContext ctx,
			[Description("Equation to solve. Examples: 'x^2 + 1 = 0', 'x^3 - 1 = 0', 'x^2 + x + 1 = 0'")] string equation,
			[Description("Variable to solve for. Default: 'z'")] string variable = "z",
			[Description("Half-range for real part. Scans [-reRange, reRange]. Default: 10")] double reRange = 10.0,
			[Description("Half-range for imaginary part. Scans [-imRange, imRange]. Default: 10")] double imRange = 10.0,
			[Description("Grid step for coarse scan. Smaller values find more roots but are slower. Default: 0.5")] double gridStep = 0.5)
		{
			try
			{
				// Parse the equation: support "expr = 0" or just "expr"
				string expressionStr = equation;
				if (equation.Contains('='))
				{
					var parts = equation.Split('=', 2);
					expressionStr = $"({parts[0].Trim()}) - ({parts[1].Trim()})";
				}

				var expression = MathExpressionParser.Parse(expressionStr);
				var roots = MathEquationSolver.FindComplexRoots(
					expression, variable, reRange, imRange, gridStep);

				if (roots.Length == 0)
				{
					ctx.Call.SetResult("No complex roots found in the specified region. Try increasing reRange/imRange or decreasing gridStep.");
					ctx.Call.SetStatus(Material.Icons.MaterialIconKind.Abacus, $"`{equation}`");
					return ToolExecutionResult.Succeeded;
				}

				var formatted = string.Join(", ", roots.Select(r => FormatComplex(r)));
				ctx.Call.SetResult($"Complex roots found for '{variable}': {formatted}");
				ctx.Call.SetStatus(Material.Icons.MaterialIconKind.Abacus,
					LocalizationManager.LocalizeStaticFormat("tool.status.math-solve.status", $"`{equation}`", roots.Length));
				return ToolExecutionResult.Succeeded;
			}
			catch (MathEvaluationException mex)
			{
				ctx.Call.SetResult("Error evaluating equation: " + mex.Message);
				ctx.Call.SetStatus(Material.Icons.MaterialIconKind.Abacus, $"`{equation}`");
				return ToolExecutionResult.Failed;
			}
			catch (ParsingException pex)
			{
				ctx.Call.SetResult("Error parsing equation: " + pex.Message);
				ctx.Call.SetStatus(Material.Icons.MaterialIconKind.Abacus, $"`{equation}`");
				return ToolExecutionResult.Failed;
			}
		}

		/// <summary>
		/// Renders a value the way the parser itself accepts it - point decimal, no localized words.
		/// </summary>
		private static string FormatComplex(Complex c)
		{
			if (double.IsNaN(c.Real) || double.IsNaN(c.Imaginary))
				return "NaN";
			if (c.Imaginary == 0)
				return c.Real.ToString("G", CultureInfo.InvariantCulture);
			if (c.Real == 0)
				return $"{c.Imaginary.ToString("G", CultureInfo.InvariantCulture)}i";

			string sign = c.Imaginary > 0 ? "+" : "-";
			string imaginary = Math.Abs(c.Imaginary).ToString("G", CultureInfo.InvariantCulture);
			return $"{c.Real.ToString("G", CultureInfo.InvariantCulture)} {sign} {imaginary}i";
		}
	}
}
