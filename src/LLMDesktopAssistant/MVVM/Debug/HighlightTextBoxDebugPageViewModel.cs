using System.Text;
using Avalonia.Media;
using CommunityToolkit.Mvvm.Input;
using LLMDesktopAssistant.Controls.Text;
using LLMDesktopAssistant.Utils;

namespace LLMDesktopAssistant.MVVM.Debug;

/// <summary>Info about a single highlight span, displayed in the list.</summary>
public sealed record HighlightSpanInfo(int Start, int End, string Kind)
{
	public string Display => $"[{Start}..{End}) {Kind}";
}

/// <summary>
/// View model for the TextBox highlighting debug page: lets you "feel" slash command
/// highlighting, ghost text and string transform inside <see cref="HighlightTextBox"/>.
/// </summary>
[ViewModelFor(typeof(HighlightTextBoxDebugPageView))]
public class HighlightTextBoxDebugPageViewModel : ViewModelBase
{
	public const string DefaultSample = """
		/help — show command list
		/fs-read C:\Projects\file.txt
		/fs-write /tmp/out.txt "quoted arg"
		/unknown-command argument
		plain text without commands
		""";

	private static readonly IBrush KnownCommandBrush = new SolidColorBrush(Color.Parse("#4EC9B0"));
	private static readonly IBrush UnknownCommandBrush = new SolidColorBrush(Color.Parse("#F44747"));
	private static readonly IBrush ArgumentsBrush = new SolidColorBrush(Color.Parse("#8C8C8C"));

	private static readonly string[] KnownCommands = ["help", "clear", "fs-read", "fs-write", "model", "agents", "memory"];

	private static bool IsNameChar(char c) => char.IsLetterOrDigit(c) || c is '-' or '_';

	private readonly HighlightTransformProvider _transformProvider;

	/// <summary>
	/// The editable text.
	/// </summary>
	public string Text
	{
		get => field;
		set => SetProperty(ref field, value);
	} = DefaultSample;

	private bool _enableCommandHighlight = true;
	/// <summary>
	/// Enables slash command highlighting.
	/// </summary>
	public bool EnableCommandHighlight
	{
		get => _enableCommandHighlight;
		set
		{
			if (SetProperty(ref _enableCommandHighlight, value))
				_transformProvider.NotifyLayoutChanged();
		}
	}

	private bool _enableTransform;
	/// <summary>
	/// Enables the string transform before rendering (paired angle quotes instead of double ones, 1:1).
	/// </summary>
	public bool EnableTransform
	{
		get => _enableTransform;
		set
		{
			if (SetProperty(ref _enableTransform, value))
				_transformProvider.NotifyLayoutChanged();
		}
	}

	/// <summary>
	/// Highlight spans of the current text (for display).
	/// </summary>
	public RangeObservableCollection<HighlightSpanInfo> Spans { get; } = new() { RaiseInUIThread = true };

	/// <summary>
	/// The provider for <see cref="HighlightTextBox.HighlightTransformProvider"/>.
	/// </summary>
	public IHighlightTransformProvider TransformProvider => _transformProvider;

	/// <summary>
	/// Restores the sample text.
	/// </summary>
	public IRelayCommand ResetCommand { get; }

	public HighlightTextBoxDebugPageViewModel()
	{
		_transformProvider = new HighlightTransformProvider(Transform);
		ResetCommand = new RelayCommand(() => Text = DefaultSample);
	}

	/// <summary>
	/// The transform for <see cref="HighlightTextBox.HighlightTransformProvider"/>: colors "/name"
	/// tokens at line starts (known commands in green, unknown in red, arguments of known commands
	/// in gray) and optionally replaces double quotes with paired angle quotes (1:1 transform).
	/// </summary>
	public HighlightTransformResult Transform(string text)
	{
		var spans = new List<TextHighlightSpan>();
		var infos = new List<HighlightSpanInfo>();

		if (EnableCommandHighlight && !string.IsNullOrEmpty(text))
		{
			var lineStart = 0;
			while (lineStart < text.Length)
			{
				var lineEnd = text.IndexOf('\n', lineStart);
				if (lineEnd < 0)
					lineEnd = text.Length;

				if (text[lineStart] == '/')
				{
					var nameStart = lineStart + 1;
					var nameEnd = nameStart;
					while (nameEnd < lineEnd && IsNameChar(text[nameEnd]))
						nameEnd++;

					if (nameEnd > nameStart)
					{
						var command = text[nameStart..nameEnd];
						var known = KnownCommands.Contains(command);

						spans.Add(new TextHighlightSpan(lineStart, nameEnd - lineStart,
							known ? KnownCommandBrush : UnknownCommandBrush));
						infos.Add(new HighlightSpanInfo(lineStart, nameEnd,
							known ? $"command:{command}" : $"unknown:{command}"));

						if (known)
						{
							var argStart = nameEnd;
							while (argStart < lineEnd && (text[argStart] is ' ' or '\t'))
								argStart++;

							if (argStart < lineEnd)
							{
								spans.Add(new TextHighlightSpan(argStart, lineEnd - argStart, ArgumentsBrush));
								infos.Add(new HighlightSpanInfo(argStart, lineEnd, "arguments"));
							}
						}
					}
				}

				lineStart = lineEnd + 1;
			}
		}

		// Какой-то ебучий костыль
		// Дело в том, что если мы при фокусе на TextBox переключаем вкладку (на чат например),
		// то авалония вылетает с сообщением "пук пук, мы не можем вызывать InvalidateVisual,
		// пока рендерим", а эта хрень переключает контекст рендера на другой поток, который
		// не является UI потоком, и всё работает!
		Task.Run(async () =>
		{
			await Task.Delay(50).ConfigureAwait(false);
			Spans.Reset(infos);
		});
		return new HighlightTransformResult(EnableTransform ? ApplyTransform(text) : null,
			spans.Count > 0 ? spans : null);
	}

	/// <summary>
	/// An example of a safe (1:1) string transform: double quotes are alternately replaced
	/// with paired angle quotes. Character lengths and positions are preserved.
	/// </summary>
	private static string ApplyTransform(string text)
	{
		var sb = new StringBuilder(text.Length);
		var open = true;

		foreach (var c in text)
		{
			if (c == '"')
			{
				sb.Append(open ? '«' : '»');
				open = !open;
			}
			else
			{
				sb.Append(c);
			}
		}

		return sb.ToString();
	}
}
