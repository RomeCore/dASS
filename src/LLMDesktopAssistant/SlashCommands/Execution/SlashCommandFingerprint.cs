using LLMDesktopAssistant.LLM.MVVM.Additional;
using LLMDesktopAssistant.Localization;
using LLMDesktopAssistant.SlashCommands.Arguments;
using LLMDesktopAssistant.SlashCommands.Resolution;

namespace LLMDesktopAssistant.SlashCommands.Execution
{
	/// <summary>
	/// The machine-readable trace of a slash command invocation, attached to the message the command produced.
	/// </summary>
	/// <remarks>
	/// Data-only (<see cref="AdditionalChatData.IsVisible"/> is <see langword="false"/>, so it is never rendered into the
	/// prompt and never appears in the UI) but persisted (<see cref="AdditionalChatData.IsTemporary"/> stays
	/// <see langword="false"/>), so it survives a reload and can be read back with
	/// <c>AdditionalData.TryGet&lt;SlashCommandFingerprint&gt;()</c>. A flat, BSON-safe snapshot: raw strings only, no
	/// references to frozen addons or live values. The collections are typed for LiteDB: lists are <c>IReadOnlyList</c>
	/// (the <c>Immutable*</c> collections do not round-trip) and the keyed map is a concrete
	/// <see cref="Dictionary{TKey,TValue}"/> (<c>IReadOnlyDictionary</c>/<c>ImmutableDictionary</c> do not round-trip
	/// either).
	/// </remarks>
	public class SlashCommandFingerprint : AdditionalChatData
	{
		/// <summary>The canonical, slash-free command token (<see cref="SlashCommandInfo.CanonicalToken"/>).</summary>
		public required string Token { get; init; }

		/// <summary>The bare command name; for an unresolved token, the name segment of the token.</summary>
		public required string CommandName { get; init; }

		/// <summary>The command's namespace qualifiers (type first, then pack).</summary>
		public IReadOnlyList<string> Namespaces { get; init; } = [];

		/// <summary>
		/// The kind of source the command originates from (<see cref="SlashCommandSource.Unknown"/> for a command
		/// with no source, or a token that did not resolve).
		/// </summary>
		public SlashCommandSource SourceKind { get; init; }

		/// <summary>The positional arguments, exactly as the user wrote them.</summary>
		public IReadOnlyList<string> PositionalArguments { get; init; } = [];

		/// <summary>The keyed arguments, exactly as the user wrote them, by key.</summary>
		public Dictionary<string, string> KeyedArguments { get; init; } = new();

		/// <summary>The verbatim rest-positional text (the surplus beyond the declared positionals).</summary>
		public string RestPositionalArguments { get; init; } = string.Empty;

		/// <summary>How the command's message is presented to the model.</summary>
		public ModelFacingMode ModelFacingMode { get; init; }

		/// <summary>The caller's generation intent.</summary>
		public bool GenerateIntent { get; init; }

		/// <summary>The resolved generation outcome.</summary>
		public bool GenerateOutcome { get; init; }

		/// <summary>How the invocation ended.</summary>
		public SlashCommandExecutionStatus Status { get; init; }

		/// <summary>The failure, when <see cref="Status"/> is <see cref="SlashCommandExecutionStatus.Failed"/>.</summary>
		public LocaleKeyBase? Error { get; init; }

		/// <summary>A short human-readable summary of the effect, when the executor provided one.</summary>
		public string? EffectSummary { get; init; }

		public SlashCommandFingerprint()
		{
			IsVisible = false;
		}

		/// <summary>
		/// Builds the trace. <paramref name="command"/> and <paramref name="arguments"/> are <see langword="null"/>
		/// when the token did not resolve, or resolved but did not bind — the trace is then built from the token alone.
		/// </summary>
		public static SlashCommandFingerprint Create(string token, SlashCommandInfo? command,
			SlashCommandBoundArguments? arguments, bool generateIntent, bool generateOutcome,
			SlashCommandExecutionStatus status, LocaleKeyBase? error = null, string? effectSummary = null)
		{
			var parsed = SlashCommandMatcher.ParseToken(token);

			return new SlashCommandFingerprint
			{
				Token = command?.CanonicalToken ?? token,
				CommandName = command?.Name ?? parsed.Name,
				Namespaces = command is not null ? [.. command.Namespaces] : [.. parsed.Qualifiers],
				SourceKind = command?.SourceKind ?? SlashCommandSource.Unknown,
				PositionalArguments = arguments is null
					? []
					: [.. arguments.Positionals.Where(a => a.Raw is not null).Select(a => a.Raw!.Raw)],
				KeyedArguments = arguments is null
					? new Dictionary<string, string>()
					: arguments.Keyed.Where(kv => kv.Value.Raw is not null)
						.ToDictionary(kv => kv.Key, kv => kv.Value.Raw!.Raw),
				RestPositionalArguments = arguments?.RestPositionalArguments ?? string.Empty,
				ModelFacingMode = command?.ModelFacingMode ?? ModelFacingMode.Raw,
				GenerateIntent = generateIntent,
				GenerateOutcome = generateOutcome,
				Status = status,
				Error = error,
				EffectSummary = effectSummary
			};
		}
	}
}
