using System.Numerics;

namespace LLMDesktopAssistant.Calculation.Ast
{
    /// <summary>
    /// Unary operations: plus, minus, factorial, absolute value.
    /// </summary>
    public enum UnaryOpKind
    {
        Plus,
        Minus,
        Factorial,
        Abs
    }

    /// <summary>
    /// Represents a unary operation applied to a sub-expression.
    /// </summary>
    public sealed class UnaryOpEntity : MathEntity
    {
        public UnaryOpKind Op { get; }
        public MathEntity Operand { get; }

        public UnaryOpEntity(UnaryOpKind op, MathEntity operand)
        {
            Op = op;
            Operand = operand;
        }

        public override MathEntity Evaluate(MathEvaluationContext ctx)
        {
            var operandVal = Operand.Evaluate(ctx);
            var value = operandVal.ToComplexOrThrow();

            Complex result = Op switch
            {
                UnaryOpKind.Plus => value,
                UnaryOpKind.Minus => -value,
                UnaryOpKind.Abs => Complex.Abs(value),
                UnaryOpKind.Factorial => SpecialFunctions.Factorial(value),
                _ => throw new MathEvaluationException($"Unknown unary operation: {Op}")
            };

            return new ConstantEntity(result);
        }

        public override string ToString()
        {
            return Op switch
            {
                UnaryOpKind.Plus => $"(+{Operand})",
                UnaryOpKind.Minus => $"(-{Operand})",
                UnaryOpKind.Factorial => $"({Operand}!)",
                UnaryOpKind.Abs => $"(|{Operand}|)",
                _ => $"(?{Operand})"
            };
        }
    }
}
