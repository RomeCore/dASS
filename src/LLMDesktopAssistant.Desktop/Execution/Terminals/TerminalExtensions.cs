using System.Text;
using XTerm;

namespace LLMDesktopAssistant.Desktop.Execution.Terminals
{
	public static class TerminalExtensions
	{
		extension(Terminal terminal)
		{
			public string GetBufferContents()
			{
				var buffer = terminal.Buffer;
				var sb = new StringBuilder();
				for (int i = 0; i < buffer.Lines.Length; i++)
				{
					var line = buffer.Lines[i];
					if (line == null)
					{
						if (i < buffer.Lines.Length - 1)
						{
							while (sb.Length > 0 && char.IsWhiteSpace(sb[^1]) && sb[^1] is not '\n' and not '\r')
								sb.Length--;
							sb.AppendLine();
						}
						continue;
					}
					for (int j = 0; j < line.Length; j++)
					{
						var cell = line[j];
						sb.Append(cell.Content);
					}
					if (i < buffer.Lines.Length - 1)
					{
						while (sb.Length > 0 && char.IsWhiteSpace(sb[^1]) && sb[^1] is not '\n' and not '\r')
							sb.Length--;
						sb.AppendLine();
					}
				}
				while (sb.Length > 0 && char.IsWhiteSpace(sb[^1]))
					sb.Length--;
				return sb.ToString();
			}

			public List<string> GetBufferLines()
			{
				var buffer = terminal.Buffer;
				var result = new List<string>();
				var sb = new StringBuilder();
				for (int i = 0; i < buffer.Lines.Length; i++)
				{
					var line = buffer.Lines[i];
					if (line == null)
					{
						if (i < buffer.Lines.Length - 1)
						{
							while (sb.Length > 0 && char.IsWhiteSpace(sb[^1]))
								sb.Length--;
							result.Add(sb.ToString());
							sb.Clear();
						}
						continue;
					}
					for (int j = 0; j < line.Length; j++)
					{
						var cell = line[j];
						sb.Append(cell.Content);
					}
					if (i < buffer.Lines.Length - 1)
					{
						while (sb.Length > 0 && char.IsWhiteSpace(sb[^1]))
							sb.Length--;
						result.Add(sb.ToString());
						sb.Clear();
					}
				}
				while (result.Count > 0 && string.IsNullOrWhiteSpace(result[^1]))
					result.RemoveAt(result.Count - 1);
				return result;
			}
		}
	}
}
