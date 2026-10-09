using System.Collections.Immutable;
using LLMDesktopAssistant.Addons;
using LLMDesktopAssistant.SlashCommands.Arguments;
using LLMDesktopAssistant.SlashCommands.Execution;

namespace LLMDesktopAssistant.SlashCommands.Providers
{
	/// <summary>
	/// The base of every provider that synthesises a command per addon of a kind: one command per <em>valid</em>
	/// available addon, carrying the addon's identity and provenance.
	/// </summary>
	/// <remarks>
	/// The provider reads <see cref="IAddonSetCollector{T}.GetAvailableAddons"/> and filters it by
	/// <see cref="AddonBase{Self}.IsValid"/> itself — it deliberately does not go through
	/// <c>GetAddonsForChat()</c>, whose availability predicates are a prompt concern. The consumers of the command
	/// surface (autocomplete, resolution) reach it through the command collector's own <c>GetAddonsForChat()</c>.
	/// Commands are frozen before they leave the provider.
	/// </remarks>
	/// <typeparam name="TSource">The addon kind the commands are derived from.</typeparam>
	public abstract class DerivedSlashCommandProvider<TSource> : ISlashCommandProvider
		where TSource : AddonBase<TSource>
	{
		private readonly IAddonSetCollector<TSource> _sources;

		protected DerivedSlashCommandProvider(IAddonSetCollector<TSource> sources)
		{
			_sources = sources;
		}

		/// <summary>
		/// The namespace qualifier that identifies the derived type, e.g. <c>skill</c> or <c>agent</c>.
		/// </summary>
		protected abstract string TypeNamespace { get; }

		/// <summary>
		/// The source kind stamped onto every command the provider derives: a skill provider yields
		/// <see cref="SlashCommandSource.Skill"/>, a sub-agent provider <see cref="SlashCommandSource.SubAgent"/>.
		/// </summary>
		protected abstract SlashCommandSource SourceKind { get; }

		/// <summary>
		/// Builds the executor that runs the command. An explicit contract so an implementation cannot forget to
		/// supply one.
		/// </summary>
		protected abstract ISlashCommandExecutor CreateCommandExecutor(TSource source);

		/// <summary>
		/// Copies any source-specific data onto the command. The executor and the argument schema are handled by the
		/// base; this hook is for everything else a kind may add.
		/// </summary>
		protected virtual void PopulateFromSource(TSource source, SlashCommandInfo command)
		{
		}

		/// <inheritdoc/>
		public IEnumerable<SlashCommandInfo> GetCommands()
		{
			foreach (var source in _sources.GetAvailableAddons().Where(a => a.IsValid))
			{
				var command = new SlashCommandInfo
				{
					Name = source.Name,
					Description = source.Description,
					Aliases = source.Aliases,
					Order = source.Order,
					Namespaces = BuildNamespaces(source),
					OverrideOrder = SlashCommandOrderTiers.ForSource(SourceKind),
					Executor = CreateCommandExecutor(source),
					SourcePack = source.SourcePack,
					Path = source.Path,
					AddonSource = source.AddonSource,
					SourceKind = SourceKind,
					Source = source
				};

				PopulateFromSource(source, command);
				command.Freeze();

				yield return command;
			}
		}

		private ImmutableList<string> BuildNamespaces(TSource source)
		{
			var builder = ImmutableList.CreateBuilder<string>();
			builder.Add(TypeNamespace);
			if (source.SourcePack is not null)
				builder.Add(source.SourcePack.Name);
			return builder.ToImmutable();
		}
	}
}
