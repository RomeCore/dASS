using System.Collections.Concurrent;
using System.Text;
using LLMDesktopAssistant.Addons.Parsers.Frontmatter;
using LLMDesktopAssistant.Localization;
using LLMDesktopAssistant.StructuredValues.Converters;
using LLMDesktopAssistant.Scripting;
using LLMDesktopAssistant.StructuredValues;
using LLMDesktopAssistant.StructuredValues.Const;
using RCParsing;
using RCParsing.Building;
using YamlDotNet.RepresentationModel;
using YamlDotNet.Serialization;

namespace LLMDesktopAssistant.Addons.Parsers
{
	public abstract class FrontmatterBasedAddonParser<T> : IAddonFileParser<T>
		where T : AddonBase<T>, new()
	{
		protected class AddonParserDescriptor : IEquatable<AddonParserDescriptor>
		{
			/// <summary>
			/// The sequence of characters that marks the start of the frontmatter.
			/// Examples: '---' (Markdown), '"""' (Python), '--[[' (Lua).
			/// </summary>
			public required string FrontmatterStart { get; init; }

			/// <summary>
			/// The sequence of characters that marks the end of the frontmatter.
			/// Examples: '---' (Markdown), '"""' (Python), ']]' (Lua).
			/// </summary>
			public required string FrontmatterEnd { get; init; }

			/// <summary>
			/// The optional EXACT prefix of every frontmatter line.
			/// This prefix is often used to strip the comment prefix in languages that not supports block comments
			/// (like 'REM ' in Windows Batch or '// ' in C-like languages).
			/// </summary>
			public string? FrontmatterLinePrefix { get; init; }

			/// <summary>
			/// Whether the parser requires frontmatter to be present in the file.
			/// </summary>
			public bool RequiresFrontmatter { get; init; }

			/// <summary>
			/// Whether to fetch name and description from markdown if cannot fetch them from frontmatter.
			/// </summary>
			public bool UseMarkdownFallback { get; init; }

			public override bool Equals(object? obj)
			{
				return obj is AddonParserDescriptor descriptor && Equals(descriptor);
			}

			public bool Equals(AddonParserDescriptor? other)
			{
				return other != null
					&& FrontmatterStart == other.FrontmatterStart && FrontmatterEnd == other.FrontmatterEnd
					&& FrontmatterLinePrefix == other.FrontmatterLinePrefix
					&& RequiresFrontmatter == other.RequiresFrontmatter
					&& UseMarkdownFallback == other.UseMarkdownFallback;
			}

			public override int GetHashCode()
			{
				// The cache is keyed by the descriptor, so a field that changes the parse (the line prefix) must be here,
				// or two kinds whose markers differ only by prefix would share a compiled parser.
				return HashCode.Combine(FrontmatterStart, FrontmatterEnd, FrontmatterLinePrefix, RequiresFrontmatter,
					UseMarkdownFallback);
			}
		}

		protected class FrontmatteredAddonDocument
		{
			/// <summary>
			/// The frontmatter content of the document.
			/// </summary>
			public required string? Frontmatter { get; init; }

			/// <summary>
			/// The required frontmatter content of the document.
			/// This property will throw an exception if the frontmatter is not present.
			/// </summary>
			public string RequiredFrontmatter => Frontmatter ?? throw new InvalidOperationException("Frontmatter is required but not present.");

			/// <summary>
			/// The body content of the document. This is the content after the frontmatter.
			/// </summary>
			public required string Body { get; init; }

			/// <summary>
			/// The fallback name that fetched from body content.
			/// </summary>
			public string? FallbackName { get; init; }

			/// <summary>
			/// The fallback description that fetched from body content.
			/// </summary>
			public string? FallbackDescription { get; init; }
		}

		private static readonly ConcurrentDictionary<AddonParserDescriptor, Parser> _parserCache = [];

		private static readonly IDeserializer _yamlDeserializer = new DeserializerBuilder()
			.IgnoreUnmatchedProperties()
			.Build();

		private static Parser GetParser(AddonParserDescriptor descriptor)
		{
			var builder = new ParserBuilder();

			builder.Settings.Skip(b => b.Whitespaces(), ParserSkippingStrategy.TryParseThenSkip);
			builder.Settings.IgnoreErrors();

			var ruleBuilder = builder.CreateMainRule();

			RuleBuilder AddFrontmatterRule(RuleBuilder builder, string start, string end, string? prefix = null)
			{
				if (string.IsNullOrEmpty(prefix))
					return builder
						.Literal(start).Newline()
						.TextUntil(end).Label("frontmatter")
						.Literal(end)
						.Transform(v => v["frontmatter"].Text);
				else
					return builder
						.Literal(prefix + start).Newline()
						.TextUntil(prefix + end).Label("frontmatter")
						.Literal(prefix + end)
						.Transform(v =>
						{
							var frontmatter = v["frontmatter"].Span.TrimEnd().ToString();
							var lines = frontmatter.Split(new string[] { "\r\n", "\n", "\r" }, StringSplitOptions.None);
							var sb = new StringBuilder();
							foreach (var line in lines)
							{
								if (!line.StartsWith(prefix))
									throw new SemanticException(v, $"Invalid frontmatter line. Expected line to start with prefix '{prefix}' but got '{line}'.");
								sb.AppendLine(line[prefix.Length..]);
							}
							while (sb.Length > 0 && char.IsWhiteSpace(sb[^1]))
								sb.Length--;
							return sb.ToString();
						});
			}

			if (descriptor.RequiresFrontmatter)
			{
				ruleBuilder
					.Repeat(b => AddFrontmatterRule(b, descriptor.FrontmatterStart, descriptor.FrontmatterEnd, descriptor.FrontmatterLinePrefix),
					1, 1).Label("frontmatter")
					.TransformLast(v => v[0].Value);
			}
			else
			{
				ruleBuilder
					.Optional(b => AddFrontmatterRule(b, descriptor.FrontmatterStart, descriptor.FrontmatterEnd, descriptor.FrontmatterLinePrefix)
					).Label("frontmatter");
			}

			if (descriptor.UseMarkdownFallback)
			{
				ruleBuilder
					.Optional(b => b
						.Literal("#").TextUntil("\n", "\r", "\r\n").Optional(b => b.Whitespaces())
						.Transform(v => v[1].Text)
					).Label("name")

					.Optional(b => b
						.OneOrMoreSeparated(b => b.TextUntil("\n", "\r", "\r\n"), s => s.Newline()).Optional(b => b.Whitespaces())
						.Transform(v => v[0].Text)
					).Label("description");
			}

			ruleBuilder
				.AllText()
				
				.Transform(v =>
				{
					var frontmatter = v.TryGetValue<string>("frontmatter");
					string body = v.Span[v["frontmatter"].EndIndex..].Trim().ToString();
					var fallbackName = v.TryGetValue<string>("name");
					var fallbackDesc = v.TryGetValue<string>("description");

					return new FrontmatteredAddonDocument
					{
						Frontmatter = frontmatter,
						Body = body,
						FallbackName = fallbackName,
						FallbackDescription = fallbackDesc
					};
				});

			return builder.Build();
		}

		public IEnumerable<T> Parse(string content, AddonPathInfo fileInfo)
		{
			var descriptor = GetDescriptorFor(content, fileInfo);
			var parser = _parserCache.GetOrAdd(descriptor, GetParser);
			var result = new T();

			result.Path ??= fileInfo.Path;
			result.SourcePack ??= fileInfo.SourcePack;
			if (result.SourcePack is not null)
				result.AddonSource = AddonSource.Pack;

			var fallbackName = GetFallbackName(fileInfo);

			FrontmatteredAddonDocument document;
			try
			{
				document = parser.Parse<FrontmatteredAddonDocument>(content);
			}
			catch (Exception ex)
			{
				result.Name = fallbackName;
				result.Description = string.Empty;
				result.Diagnostic = new AddonDiagnostic
				{
					IsFatal = true,
					Codes = AddonDiagnosticCode.GeneralParsingError,
					Exceptions = [ex]
				};
				return Finish(result);
			}

			result.BodyGetter = _ => document.Body;

			var diagnostic = (AddonDiagnostic?)null;

			var hasFrontmatter = !string.IsNullOrWhiteSpace(document.Frontmatter);

			if (descriptor.RequiresFrontmatter && !hasFrontmatter)
			{
				diagnostic = diagnostic.Combine(new AddonDiagnostic
				{
					IsFatal = true,
					Codes = AddonDiagnosticCode.MissingFrontmatter
				});
			}

			AddonFrontmatterDocument? frontmatterDocument = null;
			if (hasFrontmatter)
			{
				try
				{
					var yamlRoot = _yamlDeserializer.Deserialize<YamlMappingNode>(document.Frontmatter!);
					if (yamlRoot is not null)
						frontmatterDocument = new AddonFrontmatterDocument(yamlRoot.ToConstNodeValue()!);
				}
				catch (Exception ex)
				{
					diagnostic = diagnostic.Combine(new AddonDiagnostic
					{
						IsFatal = descriptor.RequiresFrontmatter,
						Codes = AddonDiagnosticCode.FrontmatterParsingError,
						Exceptions = [ex]
					});
				}
			}

			if (frontmatterDocument is not null)
			{
				try
				{
					Populate(result, frontmatterDocument, ref diagnostic);
				}
				catch (Exception ex)
				{
					diagnostic = diagnostic.Combine(new AddonDiagnostic
					{
						IsFatal = true,
						Codes = AddonDiagnosticCode.FrontmatterDecodingError,
						Exceptions = [ex]
					});
				}

				// Apply common metadata (compatibility, license, metadata, tags) and pack info.
				var metadata = frontmatterDocument.GetMetadata(ref diagnostic, fileInfo.SourcePack);
				result.Metadata = metadata.Metadata;
				result.AdditionalMetadata = metadata.AdditionalMetadata;
				result.Tags = metadata.Tags;

				// Apply common name.
				var fmName = frontmatterDocument.Get<string>("name");
				if (!string.IsNullOrWhiteSpace(fmName))
				{
					result.Name = fmName.Trim();
				}
				else
				{
					diagnostic = diagnostic.Combine(new AddonDiagnostic
					{
						Codes = AddonDiagnosticCode.MissingFrontmatterName
					});
					if (!string.IsNullOrWhiteSpace(document.FallbackName))
						result.Name = document.FallbackName;
					else if (!string.IsNullOrWhiteSpace(fallbackName))
						result.Name = fallbackName;
				}

				if (frontmatterDocument.TryRequest("order", ref diagnostic, out int order))
					result.Order = order;

				if (frontmatterDocument.TryRequest("aliases", ref diagnostic, out ImmutableList<string> aliases))
					result.Aliases = aliases;

				var fmDescription = frontmatterDocument.Get<string>("description");
				if (!string.IsNullOrWhiteSpace(fmDescription))
				{
					result.Description = fmDescription.Trim();
				}
				else
				{
					diagnostic = diagnostic.Combine(new AddonDiagnostic
					{
						Codes = AddonDiagnosticCode.MissingFrontmatterDescription
					});
					if (!string.IsNullOrWhiteSpace(document.FallbackDescription))
						result.Description = document.FallbackDescription;
					else
						result.Description = string.Empty;
				}

				// === Display metadata ===

				if (frontmatterDocument.TryRequest("title", ref diagnostic, out string title) && !string.IsNullOrWhiteSpace(title))
					result.NameKey = Locale.GetConstKey(title.Trim());

				if (frontmatterDocument.TryRequest("category", ref diagnostic, out string category) && !string.IsNullOrWhiteSpace(category))
					result.CategoryKey = Locale.GetConstKey(category.Trim());

				result.AdditionalProperties = frontmatterDocument.GetAdditionalProperties();
			}
			else
			{
				if (!string.IsNullOrWhiteSpace(document.FallbackName))
					result.Name = document.FallbackName;
				else if (!string.IsNullOrWhiteSpace(fallbackName))
					result.Name = fallbackName;

				if (!string.IsNullOrWhiteSpace(document.FallbackDescription))
					result.Description = document.FallbackDescription;
				else
					result.Description = string.Empty;
			}

			if (string.IsNullOrWhiteSpace(result.Name))
			{
				diagnostic = diagnostic.Combine(new AddonDiagnostic
				{
					Codes = AddonDiagnosticCode.MissingName
				});
				result.Name = "unknown";
			}

			if (!IsValidName(result.Name))
			{
				result.Name = NormalizeName(result.Name);
				diagnostic = diagnostic.Combine(new AddonDiagnostic
				{
					Codes = AddonDiagnosticCode.NameFormatError
				});
			}

			if (!IsNameMatchingFile(result.Name, fileInfo))
			{
				diagnostic = diagnostic.Combine(new AddonDiagnostic
				{
					Codes = AddonDiagnosticCode.NameFSMismatch
				});
			}

			if (string.IsNullOrWhiteSpace(result.Description))
			{
				diagnostic = diagnostic.Combine(new AddonDiagnostic
				{
					Codes = AddonDiagnosticCode.MissingDescription
				});
				result.Description = string.Empty;
			}

			if (diagnostic is not null)
				result.Diagnostic = result.Diagnostic.Combine(diagnostic);

			return Finish(result);
		}

		private IEnumerable<T> Finish(T result)
		{
			result.Name ??= "unknown";
			result.Description ??= string.Empty;
			result.ValidateProperties();
			result.Freeze();
			return [result];
		}

		/// <summary>
		/// Returns the fallback name for the addon based on its file path.
		/// </summary>
		protected virtual string GetFallbackName(AddonPathInfo fileInfo)
		{
			return fileInfo.FallbackName;
		}

		/// <summary>
		/// Validates the resulting addon name. Return false to fail with the <see cref="AddonDiagnosticCode.NameFormatError"/> code.
		/// </summary>
		protected virtual bool IsValidName(string name)
		{
			return true;
		}

		/// <summary>
		/// Normalizes an invalid name into a valid format.
		/// </summary>
		protected virtual string NormalizeName(string name)
		{
			return name;
		}

		/// <summary>
		/// Checks whether the given name matches the addon file name. Return false to fail with the
		/// <see cref="AddonDiagnosticCode.NameFSMismatch"/> code.
		/// </summary>
		protected virtual bool IsNameMatchingFile(string name, AddonPathInfo fileInfo)
		{
			return name.Equals(GetFallbackName(fileInfo), StringComparison.Ordinal);
		}

		/// <summary>
		/// Gets the parser descriptor (frontmatter markers, fallback settings) for the given file.
		/// </summary>
		protected abstract AddonParserDescriptor GetDescriptorFor(string content, AddonPathInfo fileInfo);

		/// <summary>
		/// Populates the addon with addon-specific properties from the already-parsed frontmatter.
		/// Note: the base class already fills <see cref="AddonBase{Self}.Name"/>, <see cref="AddonBase{Self}.Description"/>,
		/// <see cref="AddonBase{Self}.Path"/>, <see cref="AddonBase{Self}.SourcePack"/>, <see cref="AddonBase{Self}.Body"/>,
		/// <see cref="AddonMetadata.Metadata"/>, <see cref="AddonMetadata.AdditionalMetadata"/>
		/// and <see cref="AddonMetadata.Tags"/>.
		/// </summary>
		protected abstract void Populate(T addon, AddonFrontmatterDocument frontmatter, ref AddonDiagnostic? diagnostic);
	}

	public abstract class FrontmatterBasedAddonParser<TAddon, TChange> : FrontmatterBasedAddonParser<TAddon>
		where TAddon : AddonChangedBase<TAddon, TChange>, new()
		where TChange : AddonChangeBase
	{
		protected override void Populate(TAddon addon, AddonFrontmatterDocument frontmatter, ref AddonDiagnostic? diagnostic)
		{
			if (frontmatter.TryRequest("enabled", ref diagnostic, out bool enabled))
				addon.Enabled = enabled;

			if (frontmatter.TryRequest("hidden", ref diagnostic, out bool hidden))
				addon.Hidden = hidden;
		}
	}

	/// <summary>
	/// The parser of a scriptable kind: it resolves the language from the file extension and reads the frontmatter keys
	/// that describe how the script is invoked.
	/// </summary>
	/// <remarks>
	/// The third arity exists so the three scriptable kinds share one implementation of the extension table, the
	/// frontmatter syntax and the invocation flags. A concrete parser adds only what its kind means.
	/// </remarks>
	public abstract class FrontmatterBasedAddonParser<TAddon, TChange, TEngine> : FrontmatterBasedAddonParser<TAddon, TChange>
		where TAddon : ScriptableAddonBase<TAddon, TChange>, new()
		where TChange : AddonChangeBase
		where TEngine : IScriptableAddonEngine
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="FrontmatterBasedAddonParser{TAddon, TChange, TEngine}"/> class.
		/// </summary>
		/// <param name="engines">The engines of the kind.</param>
		/// <param name="descriptors">Reads the descriptors of one engine.</param>
		protected FrontmatterBasedAddonParser(IEnumerable<TEngine> engines,
			Func<TEngine, IReadOnlyList<IScriptEngineDescriptor>> descriptors)
			=> Engines = new ScriptEngineResolver<TEngine>(engines, descriptors);

		/// <summary>
		/// The extension table of the kind's engines.
		/// </summary>
		protected ScriptEngineResolver<TEngine> Engines { get; }

		/// <summary>
		/// Which result channels this kind consumes.
		/// </summary>
		protected abstract ScriptFeaturePolicy Features { get; }

		/// <inheritdoc/>
		protected override AddonParserDescriptor GetDescriptorFor(string content, AddonPathInfo fileInfo)
		{
			var descriptor = Engines.Resolve(fileInfo.Path).Descriptor;

			return new AddonParserDescriptor
			{
				FrontmatterStart = descriptor.FrontmatterStart,
				FrontmatterEnd = descriptor.FrontmatterEnd,
				FrontmatterLinePrefix = descriptor.FrontmatterLinePrefix,
				RequiresFrontmatter = true,
				UseMarkdownFallback = false
			};
		}

		/// <summary>
		/// Resolves the engine and the descriptor that own the addon's file.
		/// </summary>
		protected (TEngine Engine, IScriptEngineDescriptor Descriptor) Resolve(TAddon addon)
			=> Engines.Resolve(addon.Path ?? throw new InvalidOperationException($"The path of '{addon.Name}' is not set."));

		/// <inheritdoc/>
		protected override void Populate(TAddon addon, AddonFrontmatterDocument frontmatter, ref AddonDiagnostic? diagnostic)
		{
			base.Populate(addon, frontmatter, ref diagnostic);

			IScriptEngineDescriptor descriptor;
			try
			{
				(_, descriptor) = Resolve(addon);
			}
			catch (Exception ex)
			{
				diagnostic = diagnostic.Combine(new AddonDiagnostic
				{
					IsFatal = true,
					Codes = AddonDiagnosticCode.GeneralParsingError,
					Exceptions = [ex]
				});
				return;
			}

			addon.ScriptLanguage = descriptor.Language;

			if (frontmatter.Get<ConstNodeValue>("json-input") is { } input)
				ReadInputMode(addon, input, ref diagnostic);

			if (frontmatter.Get<ConstNodeValue>("json-output") is { } output)
				ReadOutputMode(addon, output, ref diagnostic);

			if (frontmatter.TryRequest("run-terminal", ref diagnostic, out bool runTerminal))
				addon.RunTerminal = runTerminal;

			if (frontmatter.TryRequest("wait", ref diagnostic, out bool wait))
				addon.TerminalWait = wait;

			Validate(addon, descriptor, Features, ref diagnostic);
		}

		private static void ReadInputMode(TAddon addon, ConstNodeValue node, ref AddonDiagnostic? diagnostic)
		{
			switch (node)
			{
				case INodeBooleanValue boolean:
					addon.InputMode = boolean.Value ? ScriptInputMode.Json : ScriptInputMode.Structured;
					break;

				case INodeStringValue text when string.Equals(text.Value, "auto", StringComparison.OrdinalIgnoreCase):
					addon.InputMode = ScriptInputMode.Auto;
					break;

				default:
					Fatal(ref diagnostic, "'json-input' must be true, false or auto.");
					break;
			}
		}

		private static void ReadOutputMode(TAddon addon, ConstNodeValue node, ref AddonDiagnostic? diagnostic)
		{
			switch (node)
			{
				case INodeBooleanValue boolean:
					addon.OutputMode = boolean.Value ? ScriptOutputMode.Whole : ScriptOutputMode.None;
					break;

				case INodeStringValue text when ScriptOutputPattern.CountUnescapedStars(text.Value) == 1:
					addon.OutputMode = ScriptOutputMode.Pattern;
					addon.OutputPattern = text.Value;
					break;

				case INodeStringValue:
					Fatal(ref diagnostic,
						"'json-output' needs exactly one unescaped '*'; a literal asterisk is written '\\*'.");
					break;

				default:
					Fatal(ref diagnostic, "'json-output' must be true or a pattern.");
					break;
			}
		}

		private static void Validate(TAddon addon, IScriptEngineDescriptor descriptor, ScriptFeaturePolicy features,
			ref AddonDiagnostic? diagnostic)
		{
			// Two independent questions, both required: does the kind consume this channel, and can the language produce it.

			var structured = addon.OutputMode is not ScriptOutputMode.None;

			if (structured && !features.HasFlag(ScriptFeaturePolicy.JsonOutput))
				Fatal(ref diagnostic, "This addon kind has no structured result; 'json-output' is not accepted here.");

			if (addon.RunTerminal && !features.HasFlag(ScriptFeaturePolicy.Terminal))
				Fatal(ref diagnostic, "This addon kind cannot run in a terminal.");

			if (!features.HasFlag(ScriptFeaturePolicy.PlainOutput) && !structured)
				Fatal(ref diagnostic, "This addon kind has no plain result; 'json-output' is required.");

			if (addon.InputMode is ScriptInputMode.Structured &&
				!descriptor.Capabilities.HasFlag(ScriptEngineCapabilities.StructuredInput))
			{
				Fatal(ref diagnostic,
					$"'{descriptor.Language}' cannot pass a structured value; declare 'json-input: true'.");
			}

			if (structured && !descriptor.Capabilities.HasFlag(ScriptEngineCapabilities.JsonOutput))
				Fatal(ref diagnostic, $"'{descriptor.Language}' cannot report a structured result.");

			if (!addon.RunTerminal)
				return;

			if (!descriptor.Capabilities.HasFlag(ScriptEngineCapabilities.Terminal))
				Fatal(ref diagnostic, $"'{descriptor.Language}' cannot run in a terminal.");

			if (structured)
				Fatal(ref diagnostic, "'run-terminal' cannot be combined with 'json-output'.");
		}

		private static void Fatal(ref AddonDiagnostic? diagnostic, string message)
			=> diagnostic = diagnostic.Combine(new AddonDiagnostic
			{
				IsFatal = true,
				Codes = AddonDiagnosticCode.InvalidKind,
				Messages = [message]
			});
	}

}
