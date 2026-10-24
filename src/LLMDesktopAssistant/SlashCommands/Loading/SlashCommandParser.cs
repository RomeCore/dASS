using System.Collections.Immutable;
using LLMDesktopAssistant.Addons;
using LLMDesktopAssistant.Addons.Parsers;
using LLMDesktopAssistant.Addons.Parsers.Frontmatter;
using LLMDesktopAssistant.Scripting;
using LLMDesktopAssistant.Services;
using LLMDesktopAssistant.SlashCommands.Arguments;
using LLMDesktopAssistant.SlashCommands.Scripting;
using LLMDesktopAssistant.StructuredValues.Const;

namespace LLMDesktopAssistant.SlashCommands.Loading
{
	/// <summary>
	/// The parser of file-based slash commands (<c>commands/&lt;name&gt;.&lt;ext&gt;</c>).
	/// </summary>
	/// <remarks>
	/// The scripting engine is selected by the file extension; it defines both the frontmatter syntax and the way the
	/// command body runs. The common frontmatter fields (name, description, aliases, order, title, category, ...) are
	/// applied by the base parser; the command-specific ones are <c>namespaces</c>, <c>model-facing</c>,
	/// <c>generate</c> and <c>argument-schema</c>.
	/// </remarks>
	[Service(typeof(IAddonFileParser<SlashCommandInfo>))]
	public class SlashCommandParser : FrontmatterBasedAddonParser<SlashCommandInfo, SlashCommandChange>
	{
		private static readonly StringComparer _extensionComparer =
			OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal;

		private readonly Dictionary<string, (IScriptableSlashCommandEngine Engine, IScriptEngineDescriptor Descriptor)> _enginesByExtension;

		/// <summary>Initializes a new instance of the <see cref="SlashCommandParser"/> class.</summary>
		/// <param name="engines">The scripting engines that provide frontmatter syntax and executors.</param>
		public SlashCommandParser(IEnumerable<IScriptableSlashCommandEngine> engines)
		{
			_enginesByExtension = engines
				.SelectMany(engine => engine.Descriptors.SelectMany(descriptor => descriptor.Extensions.Select(extension => (Extension: extension, Engine: engine, Descriptor: descriptor))))
				.ToDictionary(x => x.Extension, x => (x.Engine, x.Descriptor), _extensionComparer);
		}

		private (IScriptableSlashCommandEngine Engine, IScriptEngineDescriptor Descriptor) GetEntry(string path)
		{
			var extension = Path.GetExtension(path);
			if (_enginesByExtension.TryGetValue(extension, out var entry))
				return entry;

			throw new NotSupportedException(
				$"No scriptable slash-command engine is registered for the '{extension}' extension (file: '{path}'). " +
				$"Supported extensions: {string.Join(", ", _enginesByExtension.Keys)}.");
		}

		/// <inheritdoc/>
		protected override AddonParserDescriptor GetDescriptorFor(string content, AddonPathInfo fileInfo)
		{
			var descriptor = GetEntry(fileInfo.Path).Descriptor;

			return new AddonParserDescriptor
			{
				FrontmatterStart = descriptor.FrontmatterStart,
				FrontmatterEnd = descriptor.FrontmatterEnd,
				FrontmatterLinePrefix = descriptor.FrontmatterLinePrefix,
				RequiresFrontmatter = true,
				UseMarkdownFallback = false
			};
		}

		/// <inheritdoc/>
		protected override void Populate(SlashCommandInfo addon, AddonFrontmatterDocument frontmatter, ref AddonDiagnostic? diagnostic)
		{
			base.Populate(addon, frontmatter, ref diagnostic);

			var engine = GetEntry(addon.Path ?? throw new InvalidOperationException("The command path is not set.")).Engine;

			// Namespaces are custom qualifiers; the only one filled in for the author is the source pack.
			if (!frontmatter.TryRequest("namespaces", ref diagnostic, out ImmutableList<string> namespaces))
				namespaces = [];
			if (addon.SourcePack is { } pack)
				namespaces = namespaces.Add(pack.Name);
			if (namespaces.Count > 0)
				addon.Namespaces = namespaces;

			if (frontmatter.TryRequest("model-facing", ref diagnostic, out ModelFacingMode modelFacing))
				addon.ModelFacingMode = modelFacing;

			if (frontmatter.TryRequest("generate", ref diagnostic, out bool generate))
				addon.Generate = generate;

			var schemaNode = frontmatter.Get<ConstNodeValue>("argument-schema");
			var schema = SlashCommandArgumentSchemaParser.Parse(schemaNode, out bool invalidSchema);
			if (invalidSchema)
			{
				diagnostic = diagnostic.Combine(new AddonDiagnostic
				{
					Codes = AddonDiagnosticCode.InvalidArgumentHint,
					Messages = ["Failed to parse the 'argument-schema'."]
				});
			}

			addon.SourceKind = SlashCommandSource.Script;
			addon.OverrideOrder = SlashCommandOrderTiers.Scriptable;
			addon.Executor = engine.CreateExecutor(addon, schema);
		}
	}
}
