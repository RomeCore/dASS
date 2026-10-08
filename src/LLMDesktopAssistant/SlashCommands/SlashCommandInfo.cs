using System.Collections.Immutable;
using LLMDesktopAssistant.Addons;
using LLMDesktopAssistant.SlashCommands.Arguments;
using LLMDesktopAssistant.SlashCommands.Execution;
using LLMDesktopAssistant.Utils;

namespace LLMDesktopAssistant.SlashCommands
{
	/// <summary>
	/// The addon info of a slash command: a user-only token at the very start of a message that triggers a runtime
	/// action (skill injection, a sub-agent launch, ...).
	/// </summary>
	/// <remarks>
	/// The bare command name is <see cref="Name"/>; <see cref="Namespaces"/> holds the ordered qualifiers
	/// (the type first, then the source pack) that make up the fully-qualified token
	/// (<c>/skill:matt-pocock:grilling</c>). Commands are never exposed to agents.
	/// </remarks>
	public class SlashCommandInfo : AddonChangedBase<SlashCommandInfo, SlashCommandChange>
	{
		/// <summary>
		/// The namespace qualifiers of the command, ordered: the type first, then the source pack.
		/// </summary>
		public ImmutableList<string> Namespaces
		{
			get;
			set => SetProperty(ref field, value);
		} = [];

		/// <summary>
		/// The fully-qualified identity of the command: its namespaces (sorted ordinally) and its name, joined by
		/// <c>:</c> — for example <c>matt-pocock:skill:grilling</c>. Two commands that share a bare name but differ in
		/// namespace (a skill and a sub-agent both called <c>grilling</c>, say) are therefore distinct, both for
		/// deduplication and for per-command settings.
		/// </summary>
		public override string Key => string.Join(':', Namespaces.OrderBy(n => n, StringComparer.Ordinal)) + ":" + Name;

		/// <summary>
		/// The canonical, slash-free token of the command: its namespaces in stored order (the type first, then the
		/// pack) followed by the name, joined by <c>:</c> — for example <c>skill:matt-pocock:grilling</c>.
		/// </summary>
		/// <remarks>
		/// Unlike <see cref="Key"/> (which sorts the namespaces ordinally and is the deduplication / settings
		/// identity), this reads like the token a user types and is re-parseable by the matcher. Tokens never carry the
		/// leading <c>/</c> marker.
		/// </remarks>
		[System.Text.Json.Serialization.JsonIgnore]
		[LiteDB.BsonIgnore]
		[YamlDotNet.Serialization.YamlIgnore]
		public string CanonicalToken => string.Join(':', Namespaces.Add(Name));

		/// <summary>
		/// How the command's message is presented to the model. Defaults to <see cref="ModelFacingMode.Raw"/>.
		/// </summary>
		public ModelFacingMode ModelFacingMode
		{
			get;
			set => SetProperty(ref field, value);
		} = ModelFacingMode.Raw;

		/// <summary>
		/// The declarative generation ceiling of the command: <see langword="null"/> leaves the caller's intent
		/// untouched, <see langword="false"/> forbids generation. The command may lower the intent, never raise it.
		/// </summary>
		public bool? Generate
		{
			get;
			set => SetProperty(ref field, value);
		} = null;

		/// <summary>
		/// The argument schema of the command, or <see langword="null"/> when it takes no arguments.
		/// </summary>
		/// <remarks>
		/// The reference is settable (the addon model is mutable and <c>Clone()</c> copies settable
		/// properties), but the schema instance itself is immutable.
		/// </remarks>
		public SlashCommandArgumentSchema? ArgumentSchema
		{
			get;
			set => SetProperty(ref field, value);
		}

		/// <summary>
		/// The addon the command was derived from — a <c>SkillInfo</c>, a <c>SubAgentInfo</c>, a <c>ToolInfo</c> and so
		/// on — or <see langword="null"/> for native/scriptable commands. Typed <see cref="object"/> because the addon
		/// bases are CRTP-parameterized and share no non-generic base. Runtime-only, never serialized.
		/// </summary>
		[System.Text.Json.Serialization.JsonIgnore]
		[LiteDB.BsonIgnore]
		[YamlDotNet.Serialization.YamlIgnore]
		public object? Source
		{
			get;
			set => SetProperty(ref field, value);
		}

		/// <summary>
		/// The kind of source the command originates from. Defaults to <see cref="SlashCommandSource.Unknown"/>;
		/// a derived provider stamps it alongside <see cref="Source"/>.
		/// </summary>
		public SlashCommandSource SourceKind
		{
			get;
			set => SetProperty(ref field, value);
		} = SlashCommandSource.Unknown;

		/// <summary>
		/// The executor that runs the command's action. Never <see langword="null"/>.
		/// </summary>
		/// <remarks>
		/// Defaults to <see cref="StubCommandExecutor.Instance"/> until the real executors land (Stage 3); it is a
		/// runtime service instance, so it is never serialized.
		/// </remarks>
		[System.Text.Json.Serialization.JsonIgnore]
		[LiteDB.BsonIgnore]
		[YamlDotNet.Serialization.YamlIgnore]
		public ISlashCommandExecutor Executor
		{
			get;
			set => SetProperty(ref field, value);
		} = StubCommandExecutor.Instance;

		protected override void ValidatePropertiesCore(AppendOnlyList<string> errors)
		{
			base.ValidatePropertiesCore(errors);

			if (Executor is null)
				errors.Add("Executor is required.");
		}
	}
}
