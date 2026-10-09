using System;
using System.Collections.Generic;
using System.Linq;
using CommunityToolkit.Mvvm.Input;
using LLMDesktopAssistant.Addons;
using LLMDesktopAssistant.Agents;
using LLMDesktopAssistant.Controls.Text;
using LLMDesktopAssistant.InputCompletion;
using LLMDesktopAssistant.LLM.MVVM;
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
/// preview inside <see cref="HighlightTextBox"/>. It runs the production <see cref="SlashCommandCompletionSource"/>
/// (its analysis, its parse and its palette) behind the production <see cref="InputCompletionTransformProvider"/>, so
/// what you see is what the chat input will do — only the completion session is a stand-in, because the real one needs
/// a chat.
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

	/// <summary>The command set the real source expects, over the sample commands.</summary>
	private sealed class SampleCollector(IEnumerable<SlashCommandInfo> commands) : IAddonSetCollector<SlashCommandInfo>
	{
		public IEnumerable<SlashCommandInfo> GetAvailableAddons() => commands;

		public IEnumerable<SlashCommandInfo> GetAddonsForChat() => commands;

		public IEnumerable<SlashCommandInfo> GetAddonsForAgent(ChatAgentDescriptor agent) => commands;
	}

	/// <summary>
	/// The stand-in completion session: while the token is partial, it previews the top prefix match as an inline ghost,
	/// so the page exercises the ghost path end to end.
	/// </summary>
	private sealed class DemoCompletion(Func<bool> enabled, Func<string> text) : IInputCompletionService
	{
		public event EventHandler? ResultChanged;

		public InputCompletionResult? Result { get; private set; }

		public void Update(string? value, int caretIndex)
		{
			var result = Compute();
			if (ReferenceEquals(Result, result))
				return;

			Result = result;
			ResultChanged?.Invoke(this, EventArgs.Empty);
		}

		public void Close() => Update(null, 0);

		private InputCompletionResult? Compute()
		{
			if (!enabled())
				return null;

			var current = text();
			var analysis = SlashCommandInputAnalyzer.Analyze(current, current.Length, SampleCommands);
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

	private readonly SlashCommandCompletionSource _commandRenderer = new(new SampleCollector(SampleCommands));

	private readonly DemoCompletion _completion;

	private readonly InputCompletionTransformProvider _transformProvider;

	public HighlightTextBoxDebugPageViewModel()
	{
		_completion = new DemoCompletion(() => EnableCommandHighlight, () => Text);
		_transformProvider = new InputCompletionTransformProvider(_completion, [_commandRenderer]);

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
				RefreshCompletion();
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
		RefreshCompletion();
	}

	/// <summary>Recomputes the stand-in completion and repaints whatever the provider draws.</summary>
	private void RefreshCompletion()
	{
		_completion.Update(Text, Text.Length);
		_transformProvider.NotifyLayoutChanged();
	}
}
