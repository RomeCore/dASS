using LLMDesktopAssistant.Scripting;

namespace LLMDesktopAssistant.Tests.Scripting
{
	public class ScriptVariableNameTests
	{
		private static readonly IReadOnlySet<string> _pythonReserved =
			new HashSet<string>(StringComparer.Ordinal) { "os", "sys" };

		private static readonly IReadOnlySet<string> _batchReserved =
			new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "PATH", "COMSPEC", "PATHEXT" };

		[Fact]
		public void APlainName_IsAccepted()
			=> ScriptVariableNameValidator.Validate("tool_args", "Python", _pythonReserved);

		[Theory]
		[InlineData("")]
		[InlineData("1x")]
		[InlineData("two words")]
		[InlineData("xs-1")]
		public void ANameOutsideTheIdentifierGrammar_IsRejected(string name)
			=> Assert.Throws<ArgumentException>(() => ScriptVariableNameValidator.Validate(name, "Batch", _batchReserved));

		[Fact]
		public void APunctuationThatWouldEscapeASetStatement_IsRejected()
			=> Assert.Throws<ArgumentException>(() =>
				ScriptVariableNameValidator.Validate("a\"&calc", "Batch", _batchReserved));

		[Fact]
		public void APythonPreambleName_IsRejected()
			=> Assert.Throws<ArgumentException>(() => ScriptVariableNameValidator.Validate("os", "Python", _pythonReserved));

		[Fact]
		public void ABatchPreambleName_IsRejectedRegardlessOfCase()
			=> Assert.Throws<ArgumentException>(() => ScriptVariableNameValidator.Validate("Path", "Batch", _batchReserved));
	}
}
