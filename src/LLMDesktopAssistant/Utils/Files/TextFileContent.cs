using System.Text;

namespace LLMDesktopAssistant.Utils.Files;

/// <summary>
/// A text file read together with the facts needed to write it back unchanged: its encoding, its
/// byte order mark, its line ending, and whether the content decoded cleanly.
/// </summary>
/// <remarks>
/// An edit must change exactly the characters that matched and nothing else. Reading through
/// <see cref="File.ReadAllText(string)"/> and writing through
/// <see cref="File.WriteAllTextAsync(string, string, CancellationToken)"/> breaks that promise
/// twice: the byte order mark is consumed on read and never emitted on write, and a UTF-16 file
/// silently becomes UTF-8. Both showed up as unrelated hunks in <c>git diff</c> and as mojibake
/// far away from the edit that caused them.
/// </remarks>
public sealed class TextFileContent
{
	private static readonly byte[] utf8Bom = [0xEF, 0xBB, 0xBF];
	private static readonly byte[] utf32LeBom = [0xFF, 0xFE, 0x00, 0x00];
	private static readonly byte[] utf32BeBom = [0x00, 0x00, 0xFE, 0xFF];
	private static readonly byte[] utf16LeBom = [0xFF, 0xFE];
	private static readonly byte[] utf16BeBom = [0xFE, 0xFF];

	private TextFileContent(string text, Encoding encoding, byte[] preamble, string newLine, int replacedCharacters)
	{
		Text = text;
		Encoding = encoding;
		Preamble = preamble;
		NewLine = newLine;
		ReplacedCharacters = replacedCharacters;
	}

	/// <summary>
	/// The file's text with every line ending normalized to a single <c>'\n'</c>.
	/// </summary>
	public string Text { get; }

	/// <summary>
	/// The encoding the file was decoded with, including its byte order mark settings.
	/// </summary>
	public Encoding Encoding { get; }

	/// <summary>
	/// The byte order mark found at the start of the file, or an empty array when there was none.
	/// </summary>
	public byte[] Preamble { get; }

	/// <summary>
	/// The line ending the file uses: <c>"\r\n"</c>, <c>"\n"</c> or <c>"\r"</c>.
	/// </summary>
	public string NewLine { get; }

	/// <summary>
	/// How many characters the lenient decoder had to replace, which is non-zero only for a file
	/// that is not valid in its own encoding.
	/// </summary>
	public int ReplacedCharacters { get; }

	/// <summary>
	/// Reads a file, detecting its byte order mark, encoding and line ending.
	/// </summary>
	/// <param name="path">The absolute path to the file.</param>
	/// <returns>The decoded file and the facts needed to write it back.</returns>
	public static TextFileContent Read(string path)
	{
		var bytes = File.ReadAllBytes(path);
		var (encoding, preamble) = DetectEncoding(bytes);
		var body = bytes.AsSpan(preamble.Length);

		string text;
		var replaced = 0;
		try
		{
			text = CreateStrict(encoding).GetString(body);
		}
		catch (DecoderFallbackException)
		{
			// Undecodable bytes are not a reason to refuse the edit; they are a reason to say so.
			text = encoding.GetString(body);
			replaced = text.Count(c => c == '\uFFFD');
		}

		return new TextFileContent(Normalize(text), encoding, preamble, DetectNewLine(text), replaced);
	}

	/// <summary>
	/// Writes text back using this file's encoding, byte order mark and line ending.
	/// </summary>
	/// <param name="path">The absolute path to the file.</param>
	/// <param name="text">The new content, with <c>'\n'</c> line endings.</param>
	/// <param name="cancellationToken">The cancellation token.</param>
	public async Task WriteAsync(string path, string text, CancellationToken cancellationToken = default)
	{
		var restored = Normalize(text);
		if (NewLine != "\n")
			restored = restored.Replace("\n", NewLine);

		var body = Encoding.GetBytes(restored);
		if (Preamble.Length == 0)
		{
			await File.WriteAllBytesAsync(path, body, cancellationToken);
			return;
		}

		var bytes = new byte[Preamble.Length + body.Length];
		Preamble.CopyTo(bytes, 0);
		body.CopyTo(bytes, Preamble.Length);
		await File.WriteAllBytesAsync(path, bytes, cancellationToken);
	}

	/// <summary>
	/// A model-facing warning to add to the tool's result when the file could not be decoded
	/// cleanly, so a silently corrupted encoding stops being silent.
	/// </summary>
	/// <returns>The warning line, or <see langword="null"/> when the file decoded cleanly.</returns>
	public string? GetDecodingWarning() => ReplacedCharacters == 0
		? null
		: $"warning: the file is not valid {Encoding.WebName}; {ReplacedCharacters} character(s) were replaced while reading.";

	private static (Encoding Encoding, byte[] Preamble) DetectEncoding(byte[] bytes)
	{
		// UTF-32 little endian also starts with FF FE, so it has to be checked before UTF-16.
		if (StartsWith(bytes, utf32LeBom))
			return (new UTF32Encoding(bigEndian: false, byteOrderMark: true), utf32LeBom);
		if (StartsWith(bytes, utf32BeBom))
			return (new UTF32Encoding(bigEndian: true, byteOrderMark: true), utf32BeBom);
		if (StartsWith(bytes, utf8Bom))
			return (new UTF8Encoding(encoderShouldEmitUTF8Identifier: true), utf8Bom);
		if (StartsWith(bytes, utf16LeBom))
			return (new UnicodeEncoding(bigEndian: false, byteOrderMark: true), utf16LeBom);
		if (StartsWith(bytes, utf16BeBom))
			return (new UnicodeEncoding(bigEndian: true, byteOrderMark: true), utf16BeBom);

		return (new UTF8Encoding(encoderShouldEmitUTF8Identifier: false), []);
	}

	private static Encoding CreateStrict(Encoding encoding)
	{
		var strict = (Encoding)encoding.Clone();
		strict.DecoderFallback = DecoderFallback.ExceptionFallback;
		return strict;
	}

	private static bool StartsWith(byte[] bytes, byte[] prefix)
		=> bytes.Length >= prefix.Length && bytes.AsSpan(0, prefix.Length).SequenceEqual(prefix);

	private static string DetectNewLine(string text)
	{
		int crlf = 0, lf = 0, cr = 0;
		for (var i = 0; i < text.Length; i++)
		{
			if (text[i] == '\r')
			{
				if (i + 1 < text.Length && text[i + 1] == '\n')
				{
					crlf++;
					i++;
				}
				else
				{
					cr++;
				}
			}
			else if (text[i] == '\n')
			{
				lf++;
			}
		}

		if (crlf > 0 && crlf >= lf && crlf >= cr)
			return "\r\n";
		if (cr > lf)
			return "\r";

		return "\n";
	}

	/// <summary>
	/// Rewrites every line ending as a single <c>'\n'</c>, so content can be compared and diffed
	/// regardless of the platform and of the file it came from.
	/// </summary>
	/// <param name="text">The text to normalize.</param>
	/// <returns>The text with <c>'\n'</c> line endings.</returns>
	public static string Normalize(string text)
		=> text.Replace("\r\n", "\n").Replace("\r", "\n");
}
