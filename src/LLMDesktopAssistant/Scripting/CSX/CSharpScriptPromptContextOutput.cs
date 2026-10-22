using System.Text;

namespace LLMDesktopAssistant.Scripting.CSX
{
	/// <summary>
	/// The text sink of a C# script prompt context, in the shape the other script kinds already use
	/// (<c>Result.Write</c> / <c>SetContent</c>): the script writes, the engine reads nothing else.
	/// </summary>
	public sealed class CSharpScriptPromptContextOutput
	{
		private readonly StringBuilder _text = new();

		/// <summary>Appends a line of text.</summary>
		public void Write(string line) => _text.AppendLine(line);

		/// <summary>Appends several lines of text.</summary>
		public void WriteLines(IEnumerable<string> lines)
		{
			foreach (var line in lines)
				_text.AppendLine(line);
		}

		/// <summary>Replaces the whole collected text.</summary>
		public void SetContent(string text)
		{
			_text.Clear();
			_text.Append(text);
		}

		/// <summary>Clears the collected text.</summary>
		public void Clear() => _text.Clear();

		/// <summary>Gets the collected text.</summary>
		public string Text => _text.ToString();
	}
}
