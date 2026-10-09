using System;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.Input;
using LLMDesktopAssistant.Controls.Text;
using LLMDesktopAssistant.InputCompletion;
using LLMDesktopAssistant.SlashCommands;
using LLMDesktopAssistant.SlashCommands.Input;
using LLMDesktopAssistant.Utils;

namespace LLMDesktopAssistant.MVVM.Debug;

/// <summary>Info about a single highlight span, displayed in the list.</summary>
public sealed record HighlightSpanInfo(int Start, int End, string Kind)
{
	public string Display => $"[{Start}..{End}) {Kind}";
}

/// <summary>
/// View model for the TextBox highlighting debug page: lets you "feel" the real slash-command highlighting and ghost
/// preview inside <see cref="HighlightTextBox"/>. It uses the production <see cref="SlashCommandInputAnalyzer"/> and
/// <see cref="SlashCommandHighlightTransformProvider"/>, so what you see is what the chat input will do.
/// </summary>
[ViewModelFor(typeof(HighlightTextBoxDebugPageView))]
public class HighlightTextBoxDebugPageViewModel : ViewModelBase
{
	public const string DefaultSample = """
		/skill:grilling focus on concurrency
		this second line is plain text, not a command
		""";

	// A couple of commands, including a shadowed name, so every palette state is reachable from the sample.
	private static readonly SlashCommandInfo[] SampleCommands =
	[
		new() { Name = "grilling", Namespaces = ["skill", "matt-pocock"] },
		new() { Name = "grilling", Namespaces = ["agent"] },
		new() { Name = "web-searcher", Namespaces = ["agent"] },
		new() { Name = "code-review", Namespaces = ["skill"] }
	];

	private readonly SlashCommandHighlightTransformProvider _transformProvider;

	public HighlightTextBoxDebugPageViewModel()
	{
		_transformProvider = new SlashCommandHighlightTransformProvider(
			() => EnableCommandHighlight ? SampleCommands : [],
			DemoCompletion,
			SlashCommandHighlightPalette.FromResources());
		ResetCommand = new RelayCommand(() => Text = DefaultSample);
		UpdateSpans();
	}

	/// <summary>
	/// The editable text.
	/// </summary>
	public string Text
	{
		get => field;
		set
		{
			if (SetProperty(ref field, value))
				UpdateSpans();
		}
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
			{
				UpdateSpans();
				_transformProvider.NotifyLayoutChanged();
			}
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

	private void UpdateSpans()
	{
		var commands = EnableCommandHighlight ? SampleCommands : [];
		var analysis = SlashCommandInputAnalyzer.Analyze(Text, Text.Length, commands);

		var infos = new List<HighlightSpanInfo>();
		if (analysis.IsCommand)
		{
			infos.Add(new HighlightSpanInfo(analysis.TokenSpan.Start, analysis.TokenSpan.End,
				$"token:{analysis.Token} ({analysis.ResolutionState})"));

			if (analysis.ArgumentSpan.Length > 0)
				infos.Add(new HighlightSpanInfo(analysis.ArgumentSpan.Start, analysis.ArgumentSpan.End, "arguments"));
		}

		Spans.Reset(infos);
	}

	/// <summary>
	/// A stand-in for the (not yet built) completion source: while the token is partial, preview the top prefix match
	/// as a ghost, so the debug page exercises the renderer's ghost path end to end.
	/// </summary>
	private InputCompletionResult? DemoCompletion()
	{
		if (!EnableCommandHighlight)
			return null;

		var analysis = SlashCommandInputAnalyzer.Analyze(Text, Text.Length, SampleCommands);
		if (analysis.ResolutionState != SlashCommandInputResolutionState.Partial || analysis.Token.Length == 0)
			return null;

		var match = SlashCommandPrefixMatcher.Match(SampleCommands, analysis.Token).FirstOrDefault();
		if (match is null || !match.Name.StartsWith(analysis.Token, StringComparison.OrdinalIgnoreCase))
			return null;

		return new InputCompletionResult
		{
			Span = analysis.TokenSpan,
			GhostText = match.Name[analysis.Token.Length..]
		};
	}
}
