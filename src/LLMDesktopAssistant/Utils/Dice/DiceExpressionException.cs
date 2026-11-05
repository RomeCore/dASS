namespace LLMDesktopAssistant.Utils.Dice
{
	/// <summary>
	/// Thrown when a dice expression cannot be parsed or describes something that makes no sense to roll.
	/// </summary>
	public sealed class DiceExpressionException : Exception
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="DiceExpressionException"/> class.
		/// </summary>
		/// <param name="message">The message shown to the caller and, through it, to the model.</param>
		public DiceExpressionException(string message) : base(message)
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="DiceExpressionException"/> class.
		/// </summary>
		/// <param name="message">The message shown to the caller and, through it, to the model.</param>
		/// <param name="innerException">The parser failure that produced this error.</param>
		public DiceExpressionException(string message, Exception innerException) : base(message, innerException)
		{
		}
	}
}
