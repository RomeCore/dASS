using System.Collections.Specialized;
using LLMDesktopAssistant.Addons;
using LLMDesktopAssistant.SlashCommands.Execution;
using LLMDesktopAssistant.Utils;

namespace LLMDesktopAssistant.SlashCommands.Providers
{
	/// <summary>
	/// The base of every provider that synthesises a command per addon of a kind: one command per <em>valid</em>
	/// available addon, carrying the addon's identity and provenance.
	/// </summary>
	/// <remarks>
	/// The provider reads <see cref="IAddonSetCollector{T}.GetAvailableAddons"/> and filters it by
	/// <see cref="AddonBase{Self}.IsValid"/> itself - it deliberately does not go through
	/// <c>GetAddonsForChat()</c>, whose availability predicates are a prompt concern. The command list is rebuilt
	/// incrementally when the source available set changes: commands are matched by
	/// <see cref="SlashCommandInfo.Key"/> and a command whose source addon is the same instance is reused, so one
	/// changed skill emits one replace instead of a full reset.
	/// </remarks>
	/// <typeparam name="TSource">The addon kind the commands are derived from.</typeparam>
	public abstract class DerivedSlashCommandProvider<TSource> : Disposable, ISlashCommandProvider
		where TSource : AddonBase<TSource>
	{
		private readonly IAddonSetCollector<TSource> _sources;
		private readonly RangeObservableCollection<SlashCommandInfo> _commands = [];
		private readonly Dictionary<TSource, SlashCommandInfo> _commandMap = new(ReferenceEqualityComparer.Instance);
		private ReadOnlyObservableCollection<SlashCommandInfo>? _view;

		/// <inheritdoc/>
		public ReadOnlyObservableCollection<SlashCommandInfo> Addons => _view ??= new(_commands);

		protected DerivedSlashCommandProvider(IAddonSetCollector<TSource> sources)
		{
			_sources = sources;

			var addons = sources.GetAvailableAddons().Addons;
			addons.CollectionChanged += OnSourcesChanged;
			_commands.AddRange(addons.Where(FilterAddon).Select(BuildCommand));
			foreach (var command in _commands)
				_commandMap[(TSource)command.Source!] = command;
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

		/// <summary>
		/// Filters an addon to determine whether it should be included in the command set.
		/// </summary>
		protected virtual bool FilterAddon(TSource source)
		{
			return source.IsValid;
		}

		/// <inheritdoc/>
		protected override void Dispose(bool disposing)
		{
			base.Dispose(disposing);

			if (disposing)
				_sources.GetAvailableAddons().Addons.CollectionChanged -= OnSourcesChanged;
		}

		private void OnSourcesChanged(object? sender, NotifyCollectionChangedEventArgs e)
		{
			if (e.Action is NotifyCollectionChangedAction.Move)
				return;

			if (e.Action is NotifyCollectionChangedAction.Reset)
			{
				var visited = new HashSet<TSource>();

				foreach (var item in _sources.GetAvailableAddons().Addons)
				{
					visited.Add(item);
					if (!_commandMap.ContainsKey(item) && FilterAddon(item))
					{
						var command = BuildCommand(item);
						_commands.Add(command);
						_commandMap[item] = command;
					}
				}

				foreach (var (item, command) in _commandMap.ToList())
				{
					if (!visited.Contains(item))
					{
						_commands.Remove(command);
						_commandMap.Remove(item);
					}
				}

				return;
			}

			if (e.OldItems is not null)
				foreach (var item in e.OldItems)
					if (item is TSource oldAddon && _commandMap.Remove(oldAddon, out var command))
						_commands.Remove(command);

			if (e.NewItems is not null)
				foreach (var item in e.NewItems)
					if (item is TSource newAddon && FilterAddon(newAddon))
					{
						var command = BuildCommand(newAddon);
						_commands.Add(command);
						_commandMap[(TSource)command.Source!] = command;
					}
		}

		private SlashCommandInfo BuildCommand(TSource source)
		{
			var namespaceBuilder = ImmutableList.CreateBuilder<string>();
			namespaceBuilder.Add(TypeNamespace);
			if (source.SourcePack is not null)
				namespaceBuilder.Add(source.SourcePack.Name);

			var command = new SlashCommandInfo
			{
				Name = source.Name,
				DescriptionGetter = _ => source.Description,
				Aliases = source.Aliases,
				Order = source.Order,
				Namespaces = namespaceBuilder.ToImmutable(),
				OverrideOrder = SlashCommandOrderTiers.ForSource(SourceKind),
				Executor = CreateCommandExecutor(source),
				SourcePack = source.SourcePack,
				Path = source.Path,
				AddonSource = source.AddonSource,
				SourceKind = SourceKind,
				Source = source,
				NameKey = source.NameKey,
				DescriptionKey = source.DescriptionKey,
				CategoryKey = source.CategoryKey
			};

			PopulateFromSource(source, command);
			command.Freeze();

			return command;
		}
	}
}
