using System.Numerics;
using AsyncLua;
using AsyncLua.Values;
using LLMDesktopAssistant.Calculation;
using LLMDesktopAssistant.Calculation.Ast;

namespace LLMDesktopAssistant.Scripting.Lua.API
{
	/// <summary>
	/// Lua API extending the built-in math library: <c>math.*</c>.
	/// Adds expression evaluation, variable binding, complex numbers,
	/// numerical integration, differentiation, and equation solving.
	/// </summary>
	[LuaApi(chatScoped: false)]
	public class LuaApiMath : LuaApiBase
	{
		public override string? Namespace => "math";

		public override string? Manuals => """
			--- math - extended mathematics API

			Supplements the built-in Lua math library with expression parsing/evaluation,
			complex numbers, numerical integration/differentiation, and equation solving.

			NEW FUNCTIONS:

			--- math.parse(expression)
			  Parses a mathematical expression string into a callable function.
			  The returned function accepts an optional table of variable values.
			  This is more efficient than math.evaluate for repeated evaluation.
			  Parameters:
			    - expression: string - expression to parse (e.g. "x^2 + 2*x + 1")
			  Returns: function(vars) -> number|table
			  Example:
			    local f = math.parse("x^2 + 2*x + 1")
			    f({x = 0})  → 1
			    f({x = 1})  → 4
			    f({x = 2})  → 9

			--- math.evaluate(expression, [vars])
			  Evaluates a mathematical expression string with optional variable bindings.
			  Supports complex numbers, integrals, derivatives, all math functions.
			  Parameters:
			    - expression: string - expression to evaluate
			    - vars: table (optional) - variable bindings {x = 3, y = 5}
			  Returns: number (real) or table {real, imag} for complex results
			  Examples:
			    math.evaluate("sin(pi/4)^2 + cos(pi/4)^2")     → 1.0
			    math.evaluate("2*x + y", {x = 3, y = 5})       → 11.0
			    math.evaluate("(3+4i) * (2-5i")                → {real=26, imag=-7}
			    math.evaluate("integral(x^2, x, 0, 10)")       → 333.333...

			--- math.solve(equation, [variable], [rangeStart], [rangeEnd], [scanStep])
			  Solves f(x) = 0 numerically using the bisection method.
			  Parameters:
			    - equation: string - equation to solve (e.g. "x^2 - 5*x + 6 = 0")
			    - variable: string (optional, default "x")
			    - rangeStart: number (optional, default -100)
			    - rangeEnd: number (optional, default 100)
			    - scanStep: number (optional, default 0.1)
			  Returns: table - array of found roots (may be empty)
			  Examples:
			    math.solve("x^2 - 5*x + 6 = 0")  → {2.0, 3.0}
			    math.solve("cos(x) - x", "x", 0, 10, 0.01)  → {~0.739}

			--- math.integral(f, a, b, [steps])
			  Numerically integrates a Lua function over [a, b].
			  Parameters:
			    - f: function(x) - function to integrate
			    - a: number - lower bound
			    - b: number - upper bound
			    - steps: number (optional, default 1000) - Simpson steps
			  Returns: number
			  Example:
			    math.integral(function(x) return x^2 end, 0, 10)  → 333.333...

			--- math.derivative(f, x)
			  Numerically differentiates a Lua function at point x.
			  Parameters:
			    - f: function(x) - function to differentiate
			    - x: number - point to evaluate at
			  Returns: number
			  Example:
			    math.derivative(function(x) return x^3 end, 5)  → ~75.0

			--- math.complex(real, imag)
			  Creates a complex number as a table {real, imag}.
			--- math.mag(z)
			  Magnitude of a complex number (or absolute value of a real).
			--- math.conj(z)
			  Conjugate of a complex number.
			--- math.real(z)
			  Real part of a complex number (or the number itself).
			--- math.imag(z)
			  Imaginary part of a complex number.

			--- math.log2(x)       - logarithm base 2
			--- math.logb(x, base) - logarithm with arbitrary base
			--- math.cbrt(x)       - cube root
			--- math.gamma(x)      - Gamma function (Lanczos approximation)
			--- math.factorial(n)  - factorial (integer n >= 0)
			--- math.round(x)      - round to nearest integer
			--- math.trunc(x)      - truncate toward zero
			--- math.sind(x)       - sine (x in degrees)
			--- math.cosd(x)       - cosine (x in degrees)
			--- math.tand(x)       - tangent (x in degrees)

			CONSTANTS (via math.constants):

			--- math.constants.pi      - π = 3.141592653589793
			--- math.constants.e       - e = 2.718281828459045
			--- math.constants.phi     - φ = 1.618033988749895 (golden ratio)
			--- math.constants.tau     - τ = 6.283185307179586
			--- math.constants.gamma   - γ = 0.5772156649015328 (Euler-Mascheroni)
			--- math.constants.g       - g = 9.81 (Earth's gravitational acceleration)
			--- math.constants.c       - c = 299792458 (speed of light)
			--- math.constants.halfpi  - π/2 = 1.5707963267948966
			--- math.constants.sqrt2   - √2 = 1.4142135623730951
			--- math.constants.sqrt3   - √3 = 1.7320508075688772
			--- math.constants.ln2     - ln(2) = 0.6931471805599453

			EXAMPLES:

			  -- Parse once, evaluate many times
			  local f = math.parse("x^2 + 2*x + 1")
			  for i = 0, 5 do
			    print(f({x = i}))
			  end  → 1, 4, 9, 16, 25, 36

			  -- Evaluate with variables
			  math.evaluate("a * b + c", {a = 2, b = 3, c = 1})  → 7

			  -- Complex arithmetic
			  local z = math.evaluate("(1+2i) / (3+4i)")
			  print(z.real, z.imag)  → 0.44, 0.08

			  -- Solve equations
			  local roots = math.solve("x^3 - 6*x^2 + 11*x - 6 = 0")
			  for _, r in ipairs(roots) do print(r) end  → 1, 2, 3

			  -- Numerical methods with Lua functions
			  local area = math.integral(function(x) return math.sin(x) end, 0, math.pi)
			  print(area)  → ~2.0

			  local slope = math.derivative(function(x) return x^3 end, 5)
			  print(slope)  → ~75.0

			  -- Constants
			  print(math.constants.phi)   → 1.61803...
			  print(math.constants.sqrt2) → 1.41421...
			""";

		public override Action? Populate(LuaTable globals, LuaTable ns, LuaService luaService)
		{
			// Core
			ns["parse"] = new LuaCallbackFunction(Parse);
			ns["evaluate"] = new LuaCallbackFunction(Evaluate);
			ns["solve"] = new LuaCallbackFunction(Solve);

			// Numerical methods
			ns["integral"] = new LuaCallbackFunction(Integral);
			ns["derivative"] = new LuaCallbackFunction(Derivative);

			// Complex number support
			ns["complex"] = new LuaCallbackFunction(MakeComplex);
			ns["mag"] = new LuaCallbackFunction(Mag);
			ns["conj"] = new LuaCallbackFunction(Conj);
			ns["real"] = new LuaCallbackFunction(Real);
			ns["imag"] = new LuaCallbackFunction(Imag);

			// Extra math functions
			ns["log2"] = new LuaCallbackFunction(Log2);
			ns["logb"] = new LuaCallbackFunction(LogB);
			ns["cbrt"] = new LuaCallbackFunction(Cbrt);
			ns["gamma"] = new LuaCallbackFunction(Gamma);
			ns["factorial"] = new LuaCallbackFunction(Factorial);
			ns["round"] = new LuaCallbackFunction(Round);
			ns["trunc"] = new LuaCallbackFunction(Trunc);
			ns["sind"] = new LuaCallbackFunction(Sind);
			ns["cosd"] = new LuaCallbackFunction(Cosd);
			ns["tand"] = new LuaCallbackFunction(Tand);

			// Constants table
			var constants = new LuaTable();
			constants["pi"] = new LuaNumber(Math.PI);
			constants["e"] = new LuaNumber(double.E);
			constants["phi"] = new LuaNumber(1.618033988749895);
			constants["tau"] = new LuaNumber(6.283185307179586);
			constants["gamma"] = new LuaNumber(0.5772156649015328);
			constants["g"] = new LuaNumber(9.81);
			constants["c"] = new LuaNumber(299792458.0);
			constants["inf"] = new LuaNumber(double.PositiveInfinity);
			constants["nan"] = new LuaNumber(double.NaN);
			constants["halfpi"] = new LuaNumber(Math.PI / 2);
			constants["sqrt2"] = new LuaNumber(1.4142135623730951);
			constants["sqrt3"] = new LuaNumber(1.7320508075688772);
			constants["ln2"] = new LuaNumber(0.6931471805599453);
			ns["constants"] = constants;

			return null;
		}

		// ============================================================
		//  math.parse(expression) → function(vars)
		// ============================================================

		private LuaTuple Parse(LuaCallingContext ctx, LuaValue[] args)
		{
			if (args.Length < 1)
				throw new LuaRuntimeException("math.parse(expression, [vars]): at least 1 argument expected.");

			if (args[0] is not LuaString exprVal)
				throw new LuaRuntimeException("math.parse(expression): expression must be a string.");

			MathEntity parsed;
			try
			{
				parsed = MathExpressionParser.Parse(exprVal.Value);
			}
			catch (Exception ex)
			{
				throw new LuaRuntimeException($"math.parse() error: {ex.Message}");
			}

			var capturedEntity = parsed;

			var closure = new LuaCallbackFunction((c, a) =>
			{
				var evalCtx = new MathEvaluationContext();

				if (a.Length >= 1 && a[0] is LuaTable vars)
				{
					var scope = new Dictionary<string, MathEntity>();
					foreach (var key in vars.Keys)
					{
						if (key is not LuaString keyStr) continue;

						var val = vars.Get(key);
						if (val is LuaNumber num)
						{
							scope[keyStr.Value] = new ConstantEntity(new Complex(num.Value, 0));
							continue;
						}

						if (val is LuaTable t)
						{
							double re = t.Get("real") is LuaNumber reNum ? reNum.Value : 0;
							double im = t.Get("imag") is LuaNumber imNum ? imNum.Value : 0;
							scope[keyStr.Value] = new ConstantEntity(new Complex(re, im));
							continue;
						}
					}
					evalCtx.PushScope(scope);
				}

				try
				{
					var result = capturedEntity.Evaluate(evalCtx);
					var complex = result.ToComplexOrThrow();

					if (complex.Imaginary == 0)
						return new LuaTuple(new LuaNumber(complex.Real));

					var tbl = new LuaTable();
					tbl["real"] = new LuaNumber(complex.Real);
					tbl["imag"] = new LuaNumber(complex.Imaginary);
					return new LuaTuple(tbl);
				}
				catch (Exception ex)
				{
					throw new LuaRuntimeException($"math.parse() evaluation error: {ex.Message}");
				}
				finally
				{
					if (a.Length >= 1 && a[0] is LuaTable)
						evalCtx.PopScope();
				}
			});

			return new LuaTuple(closure);
		}

		// ============================================================
		//  math.evaluate(expression, [vars])
		// ============================================================

		private LuaTuple Evaluate(LuaCallingContext ctx, LuaValue[] args)
		{
			if (args.Length < 1)
				throw new LuaRuntimeException("math.evaluate(expression, [vars]): at least 1 argument expected.");

			if (args[0] is not LuaString exprVal)
				throw new LuaRuntimeException("math.evaluate(expression, [vars]): expression must be a string.");

			try
			{
				var parsed = MathExpressionParser.Parse(exprVal.Value);
				var evalCtx = new MathEvaluationContext();

				if (args.Length >= 2 && args[1] is LuaTable vars)
				{
					var scope = new Dictionary<string, MathEntity>();
					foreach (var key in vars.Keys)
					{
						if (key is not LuaString keyStr) continue;

						var val = vars.Get(key);
						if (val is LuaNumber num)
						{
							scope[keyStr.Value] = new ConstantEntity(new Complex(num.Value, 0));
							continue;
						}

						if (val is LuaTable t)
						{
							double re = t.Get("real") is LuaNumber reNum ? reNum.Value : 0;
							double im = t.Get("imag") is LuaNumber imNum ? imNum.Value : 0;
							scope[keyStr.Value] = new ConstantEntity(new Complex(re, im));
							continue;
						}
					}
					evalCtx.PushScope(scope);
				}

				MathEntity result;
				try
				{
					result = parsed.Evaluate(evalCtx);
				}
				finally
				{
					if (args.Length >= 2 && args[1] is LuaTable)
						evalCtx.PopScope();
				}

				var complexNumber = result.ToComplexOrThrow();

				if (complexNumber.Imaginary == 0)
					return new LuaTuple(new LuaNumber(complexNumber.Real));

				var tbl = new LuaTable();
				tbl["real"] = new LuaNumber(complexNumber.Real);
				tbl["imag"] = new LuaNumber(complexNumber.Imaginary);
				return new LuaTuple(tbl);
			}
			catch (Exception ex)
			{
				throw new LuaRuntimeException($"math.evaluate() error: {ex.Message}");
			}
		}

		// ============================================================
		//  math.solve(equation, [variable], [rangeStart], [rangeEnd], [scanStep])
		// ============================================================

		private LuaTuple Solve(LuaCallingContext ctx, LuaValue[] args)
		{
			if (args.Length < 1)
				throw new LuaRuntimeException("math.solve(equation, ...): at least 1 argument expected.");

			if (args[0] is not LuaString equationVal)
				throw new LuaRuntimeException("math.solve(equation, ...): equation must be a string.");

			string variable = "x";
			double rangeStart = -100.0;
			double rangeEnd = 100.0;
			double scanStep = 0.1;

			if (args.Length >= 2 && args[1] is not LuaNil)
				variable = args[1] is LuaString varStr ? varStr.Value : "x";
			if (args.Length >= 3 && args[2] is not LuaNil)
				rangeStart = args[2] is LuaNumber rs ? rs.Value : -100.0;
			if (args.Length >= 4 && args[3] is not LuaNil)
				rangeEnd = args[3] is LuaNumber re ? re.Value : 100.0;
			if (args.Length >= 5 && args[4] is not LuaNil)
				scanStep = args[4] is LuaNumber ss ? ss.Value : 0.1;

			try
			{
				string expressionStr = equationVal.Value;
				if (expressionStr.Contains('='))
				{
					var parts = expressionStr.Split('=', 2);
					expressionStr = $"({parts[0].Trim()}) - ({parts[1].Trim()})";
				}

				var parsed = MathExpressionParser.Parse(expressionStr);
				var roots = MathEquationSolver.FindRoots(parsed, variable, rangeStart, rangeEnd, scanStep);

				var resultTable = new LuaTable();
				for (int i = 0; i < roots.Length; i++)
					resultTable[i + 1] = new LuaNumber(roots[i]);

				return new LuaTuple(resultTable);
			}
			catch (Exception ex)
			{
				throw new LuaRuntimeException($"math.solve() error: {ex.Message}");
			}
		}

		// ============================================================
		//  math.integral(f, a, b, [steps])
		// ============================================================

		private LuaTuple Integral(LuaCallingContext ctx, LuaValue[] args)
		{
			if (args.Length < 3)
				throw new LuaRuntimeException("math.integral(f, a, b, [steps]): at least 3 arguments expected.");

			if (args[0] is not LuaFunction func)
				throw new LuaRuntimeException("math.integral(f, a, b, [steps]): first argument must be a function.");

			if (args[1] is not LuaNumber aVal)
				throw new LuaRuntimeException("math.integral(f, a, b, [steps]): a must be a number.");
			if (args[2] is not LuaNumber bVal)
				throw new LuaRuntimeException("math.integral(f, a, b, [steps]): b must be a number.");

			double a = aVal.Value;
			double b = bVal.Value;
			int steps = NumericalIntegration.DefaultSteps;
			if (args.Length >= 4 && args[3] is not LuaNil)
				steps = (int)(args[3] is LuaNumber s ? s.Value : NumericalIntegration.DefaultSteps);

			double h = (b - a) / steps;
			double sum = 0;

			for (int i = 0; i < steps; i++)
			{
				double x0 = a + i * h;
				double x1 = x0 + h;
				double xm = (x0 + x1) / 2;

				double f0 = CallFunction(func, ctx, x0);
				double fm = CallFunction(func, ctx, xm);
				double f1 = CallFunction(func, ctx, x1);

				sum += h / 6 * (f0 + 4 * fm + f1);
			}

			return new LuaTuple(new LuaNumber(sum));
		}

		// ============================================================
		//  math.derivative(f, x)
		// ============================================================

		private LuaTuple Derivative(LuaCallingContext ctx, LuaValue[] args)
		{
			if (args.Length < 2)
				throw new LuaRuntimeException("math.derivative(f, x): at least 2 arguments expected.");

			if (args[0] is not LuaFunction func)
				throw new LuaRuntimeException("math.derivative(f, x): first argument must be a function.");

			if (args[1] is not LuaNumber xVal)
				throw new LuaRuntimeException("math.derivative(f, x): x must be a number.");

			double x = xVal.Value;
			const double h = NumericalDifferentiation.DefaultStepSize;
			double fp = CallFunction(func, ctx, x + h);
			double fm = CallFunction(func, ctx, x - h);

			return new LuaTuple(new LuaNumber((fp - fm) / (2 * h)));
		}

		// ============================================================
		//  Complex number utilities
		// ============================================================

		private LuaTuple MakeComplex(LuaCallingContext ctx, LuaValue[] args)
		{
			if (args.Length < 2)
				throw new LuaRuntimeException("math.complex(real, imag): 2 arguments expected.");

			if (args[0] is not LuaNumber realVal)
				throw new LuaRuntimeException("math.complex(): real must be a number.");
			if (args[1] is not LuaNumber imagVal)
				throw new LuaRuntimeException("math.complex(): imag must be a number.");

			var tbl = new LuaTable();
			tbl["real"] = new LuaNumber(realVal.Value);
			tbl["imag"] = new LuaNumber(imagVal.Value);
			return new LuaTuple(tbl);
		}

		private LuaTuple Mag(LuaCallingContext ctx, LuaValue[] args)
		{
			var (re, im) = GetComplex(args, "math.mag");
			return new LuaTuple(new LuaNumber(Math.Sqrt(re * re + im * im)));
		}

		private LuaTuple Conj(LuaCallingContext ctx, LuaValue[] args)
		{
			var (re, im) = GetComplex(args, "math.conj");
			var tbl = new LuaTable();
			tbl["real"] = new LuaNumber(re);
			tbl["imag"] = new LuaNumber(-im);
			return new LuaTuple(tbl);
		}

		private LuaTuple Real(LuaCallingContext ctx, LuaValue[] args)
		{
			var (re, _) = GetComplex(args, "math.real");
			return new LuaTuple(new LuaNumber(re));
		}

		private LuaTuple Imag(LuaCallingContext ctx, LuaValue[] args)
		{
			var (_, im) = GetComplex(args, "math.imag");
			return new LuaTuple(new LuaNumber(im));
		}

		// ============================================================
		//  Extra math functions
		// ============================================================

		private LuaTuple Log2(LuaCallingContext ctx, LuaValue[] args)
		{
			if (args.Length < 1 || args[0] is not LuaNumber xVal)
				throw new LuaRuntimeException("math.log2(x): 1 argument expected (number).");
			return new LuaTuple(new LuaNumber(Math.Log(xVal.Value, 2)));
		}

		private LuaTuple LogB(LuaCallingContext ctx, LuaValue[] args)
		{
			if (args.Length < 2 || args[0] is not LuaNumber xVal || args[1] is not LuaNumber baseVal)
				throw new LuaRuntimeException("math.logb(x, base): 2 arguments expected (numbers).");
			return new LuaTuple(new LuaNumber(Math.Log(xVal.Value, baseVal.Value)));
		}

		private LuaTuple Cbrt(LuaCallingContext ctx, LuaValue[] args)
		{
			if (args.Length < 1 || args[0] is not LuaNumber xVal)
				throw new LuaRuntimeException("math.cbrt(x): 1 argument expected (number).");
			return new LuaTuple(new LuaNumber(Math.Cbrt(xVal.Value)));
		}

		private LuaTuple Gamma(LuaCallingContext ctx, LuaValue[] args)
		{
			if (args.Length < 1 || args[0] is not LuaNumber xVal)
				throw new LuaRuntimeException("math.gamma(x): 1 argument expected (number).");
			return new LuaTuple(new LuaNumber(ComputeGamma(xVal.Value)));
		}

		private LuaTuple Factorial(LuaCallingContext ctx, LuaValue[] args)
		{
			if (args.Length < 1 || args[0] is not LuaNumber nVal)
				throw new LuaRuntimeException("math.factorial(n): 1 argument expected (number).");
			return new LuaTuple(new LuaNumber(ComputeFactorial((int)nVal.Value)));
		}

		private LuaTuple Round(LuaCallingContext ctx, LuaValue[] args)
		{
			if (args.Length < 1 || args[0] is not LuaNumber xVal)
				throw new LuaRuntimeException("math.round(x): 1 argument expected (number).");
			return new LuaTuple(new LuaNumber(Math.Round(xVal.Value)));
		}

		private LuaTuple Trunc(LuaCallingContext ctx, LuaValue[] args)
		{
			if (args.Length < 1 || args[0] is not LuaNumber xVal)
				throw new LuaRuntimeException("math.trunc(x): 1 argument expected (number).");
			return new LuaTuple(new LuaNumber(Math.Truncate(xVal.Value)));
		}

		private LuaTuple Sind(LuaCallingContext ctx, LuaValue[] args)
		{
			if (args.Length < 1 || args[0] is not LuaNumber xVal)
				throw new LuaRuntimeException("math.sind(x): 1 argument expected (number).");
			return new LuaTuple(new LuaNumber(Math.Sin(xVal.Value * Math.PI / 180.0)));
		}

		private LuaTuple Cosd(LuaCallingContext ctx, LuaValue[] args)
		{
			if (args.Length < 1 || args[0] is not LuaNumber xVal)
				throw new LuaRuntimeException("math.cosd(x): 1 argument expected (number).");
			return new LuaTuple(new LuaNumber(Math.Cos(xVal.Value * Math.PI / 180.0)));
		}

		private LuaTuple Tand(LuaCallingContext ctx, LuaValue[] args)
		{
			if (args.Length < 1 || args[0] is not LuaNumber xVal)
				throw new LuaRuntimeException("math.tand(x): 1 argument expected (number).");
			return new LuaTuple(new LuaNumber(Math.Tan(xVal.Value * Math.PI / 180.0)));
		}

		// ============================================================
		//  Helpers
		// ============================================================

		private static (double real, double imag) GetComplex(LuaValue[] args, string funcName)
		{
			if (args.Length < 1)
				throw new LuaRuntimeException($"{funcName}(z): 1 argument expected.");

			if (args[0] is LuaTable tbl)
			{
				double real = tbl.Get("real") is LuaNumber reNum ? reNum.Value : 0;
				double imag = tbl.Get("imag") is LuaNumber imNum ? imNum.Value : 0;
				return (real, imag);
			}

			if (args[0] is LuaNumber num)
				return (num.Value, 0);

			throw new LuaRuntimeException($"{funcName}(z): argument must be a number or complex table.");
		}

		private static double CallFunction(LuaFunction func, LuaCallingContext ctx, double x)
		{
			var result = func.Invoke(ctx, new LuaNumber(x));
			if (result.Count > 0 && result[0] is LuaNumber num)
				return num.Value;
			throw new LuaRuntimeException("Function returned non-numeric value.");
		}

		private static double ComputeGamma(double x)
		{
			if (x <= 0) return double.NaN;
			double[] p = {
				0.99999999999980993, 676.5203681218851, -1259.1392167224028,
				771.32342877765313, -176.61502916214059, 12.507343278686905,
				-0.13857109526572012, 9.9843695780195716e-6, 1.5056327351493116e-7
			};
			if (x < 0.5)
				return Math.PI / (Math.Sin(Math.PI * x) * ComputeGamma(1 - x));
			x -= 1;
			double a = p[0];
			for (int i = 1; i < p.Length; i++) a += p[i] / (x + i);
			double t = x + 7.5;
			return Math.Sqrt(2 * Math.PI) * Math.Pow(t, x + 0.5) * Math.Exp(-t) * a;
		}

		private static double ComputeFactorial(int n)
		{
			if (n < 0) return double.NaN;
			double r = 1;
			for (long i = 2; i <= n && !double.IsPositiveInfinity(r); i++) r *= i;
			return r;
		}
	}
}
