using System.Numerics;

namespace LLMDesktopAssistant.Calculation.Ast
{
	/// <summary>
	/// Represents a function call. All functions operate on <see cref="Complex"/> values
	/// just like in the original <c>Calculator</c>.
	/// </summary>
	public sealed class FunctionCallEntity : MathEntity
	{
		public string Name { get; }
		public ImmutableList<MathEntity> Arguments { get; }

		public FunctionCallEntity(string name, IEnumerable<MathEntity> arguments)
		{
			Name = name;
			Arguments = arguments.ToImmutableList();
		}

		public override MathEntity Evaluate(MathEvaluationContext ctx)
		{
			try
			{
				string lowerName = Name.ToLowerInvariant();

				if (lowerName == "integral") return EvaluateIntegral(ctx);
				if (lowerName == "derivative") return EvaluateDerivative(ctx);

				Complex[] args = new Complex[Arguments.Count];
				for (int i = 0; i < Arguments.Count; i++)
					args[i] = Arguments[i].Evaluate(ctx).ToComplexOrThrow();

				Complex complexResult = lowerName switch
				{
					"sin" => Complex.Sin(args[0]),
					"sinh" => Complex.Sinh(args[0]),
					"asin" or "arcsin" => Complex.Asin(args[0]),
					"asinh" or "arsinh" or "arsh" => Math.Asinh(args[0].Real),

					"cos" => Complex.Cos(args[0]),
					"cosh" or "ch" => Complex.Cosh(args[0]),
					"acos" or "arccos" => Complex.Acos(args[0]),
					"acosh" or "arcosh" or "arch" => Math.Acosh(args[0].Real),

					"tan" => Complex.Tan(args[0]),
					"cot" or "ctg" => 1 / Complex.Tan(args[0]),
					"tanh" or "th" => Complex.Tanh(args[0]),
					"atan" or "arctan" => Complex.Atan(args[0]),
					"atanh" or "artanh" or "arth" => Math.Atanh(args[0].Real),

					"sind" => Complex.Sin(args[0] * (Math.PI / 180.0)),
					"cosd" => Complex.Cos(args[0] * (Math.PI / 180.0)),
					"tand" => Complex.Tan(args[0] * (Math.PI / 180.0)),
					"cotd" => 1 / Complex.Tan(args[0] * (Math.PI / 180.0)),
					"asind" => Complex.Asin(args[0]) * (180.0 / Math.PI),
					"acosd" => Complex.Acos(args[0]) * (180.0 / Math.PI),
					"atand" => Complex.Atan(args[0]) * (180.0 / Math.PI),

					"ln" => Complex.Log(args[0]),
					"log" when args.Length >= 2 => Complex.Log(args[1], args[0].Real),
					"log" => Complex.Log10(args[0]),
					"log2" => Complex.Log(args[0], 2),
					"log10" => Complex.Log(args[0], 10),
					"logb" => Complex.Log(args[1], args[0].Real),

					"exp" => Complex.Exp(args[0]),
					"sqrt" => Complex.Sqrt(args[0]),
					"pow" => Complex.Pow(args[0], args[1]),

					"abs" => Complex.Abs(args[0]),
					"mag" or "magnitude" => args[0].Magnitude,
					"conj" or "conjugate" => Complex.Conjugate(args[0]),

					"sign" or "signum" or "sgn" => new Complex(Math.Sign(args[0].Real), 0),
					"ceil" or "ceiling" => new Complex(Math.Ceiling(args[0].Real), 0),
					"floor" => new Complex(Math.Floor(args[0].Real), 0),
					"round" => new Complex(Math.Round(args[0].Real), 0),
					"trunc" or "truncate" => new Complex(Math.Truncate(args[0].Real), 0),

					"atan2" => new Complex(Math.Atan2(args[0].Real, args[1].Real), 0),
					"cbrt" => new Complex(Math.Cbrt(args[0].Real), 0),

					"mod" => new Complex(args[0].Real % args[1].Real, 0),

					"min" => new Complex(args.Min(a => a.Real), 0),
					"max" => new Complex(args.Max(a => a.Real), 0),

					"maxmag" => args.Aggregate(Complex.Zero, (a, b) => Complex.MaxMagnitude(a, b)),
					"minmag" => args.Aggregate(Complex.Zero, (a, b) => Complex.MinMagnitude(a, b)),

					"gamma" => new Complex(ComputeGamma(args[0].Real), 0),
					"compgamma" => ComputeComplexGamma(args[0]),
					"factorial" => new Complex(ComputeFactorial((int)args[0].Real), 0),

					_ => throw new MathEvaluationException($"Unknown function '{Name}'.")
				};

				return new ConstantEntity(complexResult);
			}
			catch (MathEvaluationException)
			{
				throw;
			}
			catch (Exception ex)
			{
				throw new MathEvaluationException($"Error evaluating function '{Name}': {ex.Message}; Maybe count of arguments is incorrect.", ex);
			}
		}

		private MathEntity EvaluateIntegral(MathEvaluationContext ctx)
		{
			if (Arguments.Count < 2)
				throw new MathEvaluationException("integral requires at least 2 arguments: function and variable.");
			if (Arguments[1] is not VariableEntity varEntity)
				throw new MathEvaluationException("Second argument of integral must be a variable.");

			string variable = varEntity.Name;

			if (Arguments.Count >= 4)
			{
				double a = Arguments[2].Evaluate(ctx).ToComplexOrThrow().Real;
				double b = Arguments[3].Evaluate(ctx).ToComplexOrThrow().Real;
				double value = NumericalIntegration.Simpson(Arguments[0], variable, a, b, ctx);
				return new ConstantEntity(new Complex(value, 0));
			}

			return this; // indefinite - symbolic
		}

		private MathEntity EvaluateDerivative(MathEvaluationContext ctx)
		{
			if (Arguments.Count < 2)
				throw new MathEvaluationException("derivative requires at least 2 arguments: function and variable.");
			if (Arguments[1] is not VariableEntity varEntity)
				throw new MathEvaluationException("Second argument of derivative must be a variable.");

			string variable = varEntity.Name;

			double x0;
			if (Arguments.Count >= 3)
				x0 = Arguments[2].Evaluate(ctx).ToComplexOrThrow().Real;
			else if (ctx.TryGetVariable(variable, out var v))
				x0 = v.ToComplexOrThrow().Real;
			else
				return this; // symbolic

			return new ConstantEntity(new Complex(
				NumericalDifferentiation.CentralDifference(Arguments[0], variable, x0, ctx), 0));
		}

		private static double ComputeGamma(double x)
		{
			if (x <= 0) return double.NaN;
			double[] p = { 0.99999999999980993, 676.5203681218851, -1259.1392167224028, 771.32342877765313, -176.61502916214059, 12.507343278686905, -0.13857109526572012, 9.9843695780195716e-6, 1.5056327351493116e-7 };
			if (x < 0.5) return Math.PI / (Math.Sin(Math.PI * x) * ComputeGamma(1 - x));
			x -= 1;
			double a = p[0];
			for (int i = 1; i < p.Length; i++) a += p[i] / (x + i);
			double t = x + 7.5;
			return Math.Sqrt(2 * Math.PI) * Math.Pow(t, x + 0.5) * Math.Exp(-t) * a;
		}

		private static Complex ComputeComplexGamma(Complex x)
		{
			if (x.Magnitude <= 0) return double.NaN;
			double[] p = { 0.99999999999980993, 676.5203681218851, -1259.1392167224028, 771.32342877765313, -176.61502916214059, 12.507343278686905, -0.13857109526572012, 9.9843695780195716e-6, 1.5056327351493116e-7 };
			if (x.Magnitude < 0.5) return Math.PI / (Complex.Sin(Math.PI * x) * ComputeComplexGamma(1 - x));
			x -= 1;
			Complex a = p[0];
			for (int i = 1; i < p.Length; i++) a += p[i] / (x + i);
			Complex t = x + 7.5;
			return Complex.Sqrt(2 * Math.PI) * Complex.Pow(t, x + 0.5) * Complex.Exp(-t) * a;
		}

		private static double ComputeFactorial(int n)
		{
			if (n < 0) return double.NaN;
			double r = 1;
			for (long i = 2; i <= n && !double.IsPositiveInfinity(r); i++) r *= i;
			return r;
		}

		public override string ToString() => $"{Name}({string.Join(", ", Arguments)})";
	}
}
