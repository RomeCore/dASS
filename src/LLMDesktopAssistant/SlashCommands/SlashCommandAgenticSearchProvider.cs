using System.Text;
using LLMDesktopAssistant.Addons;
using LLMDesktopAssistant.Addons.Search;
using LLMDesktopAssistant.Agents;
using LLMDesktopAssistant.LLM.Services;
using LLMDesktopAssistant.SlashCommands.Arguments;

namespace LLMDesktopAssistant.SlashCommands
{
	/// <summary>
	/// Serves the chat's slash commands to the addon tools ('addon-search', 'addon-list_available' and
	/// 'addon-info'), so an agent can tell the user which commands exist and how to type them.
	/// </summary>
	/// <remarks>
	/// <para>
	/// An agent cannot run a command, and does not need to: skills, sub-agents and tools are reachable directly,
	/// and a scriptable command is a user-facing action. The catalogue is therefore read-only advice — every card
	/// carries the exact token, so the model quotes it instead of inventing one.
	/// </para>
	/// <para>
	/// Commands derived from other addons (<see cref="SlashCommandInfo.IsDerived"/>) are excluded: their tokens
	/// mirror an addon the model already finds through that addon's own kind ('skills', 'agents', ...), and a real
	/// installation would otherwise drown every query in one command per skill.
	/// </para>
	/// <para>
	/// The candidates come from <see cref="IAddonSetCollector{T}.GetAddonsForChat"/> — the same set autocomplete
	/// and resolution use, with the chat's enablement settings and per-command overrides already applied. Commands
	/// are chat-level and agent-agnostic, so the agent is ignored.
	/// </para>
	/// </remarks>
	[ChatService(typeof(IAddonAgenticSearchProvider))]
	public class SlashCommandAgenticSearchProvider(
		IAddonSetCollector<SlashCommandInfo> collector,
		IAddonSearchService<SlashCommandInfo> searchService)
		: AddonAgenticSearchProvider<SlashCommandInfo>(collector, searchService)
	{
		/// <inheritdoc/>
		public override AddonKind Kind => AddonKind.SlashCommand;

		/// <inheritdoc/>
		public override string Title => "Slash commands";

		/// <inheritdoc/>
		public override string UsageHint =>
			"typed by the user at the start of a message; tell the user about them, never try to run one; " +
			"derived commands (tools, skills, sub-agents) are not shown";

		/// <inheritdoc/>
		protected override IEnumerable<SlashCommandInfo> GetCandidates(ChatAgentDescriptor? agent)
		{
			return Collector.GetAddonsForChat();
		}

		/// <inheritdoc/>
		protected override bool Include(SlashCommandInfo addon)
		{
			return !addon.IsDerived;
		}

		/// <inheritdoc/>
		protected override void AppendAddon(StringBuilder builder, SlashCommandInfo command, bool detailed)
		{
			AddonSearchFormatting.AppendItem(builder, "/" + command.CanonicalToken, command.Description,
				BuildSuffix(command));

			builder.Append("  - source kind: ").AppendLine(command.SourceKind.ToString().ToLowerInvariant());

			if (!detailed)
				return;

			AddonSearchFormatting.AppendMetadata(builder, command.Tags, command.SourcePack?.Name, command.Path);
		}

		/// <summary>
		/// Renders the trailing parenthesised suffix of the card: the argument shape and the aliases.
		/// </summary>
		private static string? BuildSuffix(SlashCommandInfo command)
		{
			var parts = new List<string>();

			var arguments = FormatArguments(command.Executor.ArgumentSchema);
			if (arguments.Length > 0)
				parts.Add("takes " + arguments);

			if (command.Aliases.Count > 0)
				parts.Add("aliases: " + string.Join(", ", command.Aliases.Select(alias => "/" + alias)));

			return parts.Count == 0 ? null : "(" + string.Join("; ", parts) + ")";
		}

		/// <summary>
		/// Renders the argument schema as the authored hint would read: <c>&lt;required&gt;</c> and
		/// <c>[optional]</c> positionals, <c>[key=value]</c> keyed arguments and a <c>...</c> rest slot.
		/// </summary>
		private static string FormatArguments(SlashCommandArgumentSchema? schema)
		{
			if (schema is null)
				return string.Empty;

			var parts = new List<string>();

			foreach (var argument in schema.Positionals)
			{
				var body = argument.Name.Value;
				if (!string.IsNullOrEmpty(argument.Default))
					body += "=" + argument.Default;

				parts.Add(Enclose(argument.Required && string.IsNullOrEmpty(argument.Default), body));
			}

			foreach (var (key, argument) in schema.Keyed.OrderBy(pair => pair.Key, StringComparer.Ordinal))
				parts.Add(Enclose(argument.Required, key + "=value"));

			if (schema.RestPositional is { } rest)
				parts.Add(Enclose(rest.Required, "..."));

			return string.Join(" ", parts);
		}

		private static string Enclose(bool required, string body)
			=> required ? "<" + body + ">" : "[" + body + "]";
	}
}
