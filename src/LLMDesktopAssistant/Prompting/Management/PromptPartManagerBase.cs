using System.Collections.Specialized;
using LLMDesktopAssistant.Services;
using LLMDesktopAssistant.Settings.Application;
using LLMDesktopAssistant.StructuredValues.Parameterization;
using LLMDesktopAssistant.Utils;
using LLTSharp;
using LLTSharp.Locale;
using LLTSharp.Metadata;
using LLTSharp.Metadata.Types;

namespace LLMDesktopAssistant.Prompting.Management
{
	public abstract class PromptPartManagerBase<K, V> : IPromptPartManager<K, V>
		where K : notnull
		where V : PromptPartBase, new()
	{
		/// <inheritdoc/>
		public event EventHandler? Changed;

		private readonly Lock _lock = new();

		private readonly HashSet<V> _parts = [];
		private readonly Dictionary<K, HashSet<V>> _byKey = [];
		private readonly Dictionary<ITemplate, V> _byTemplate = [];
		private readonly Dictionary<PromptPartSource, HashSet<V>> _bySource = [];
		private readonly Dictionary<ITemplate, (K, PromptPartSource)> _templateSourceKeys = [];

		private readonly string _templateType;
		private readonly PromptPartConfigurationBase<V> _capturedConfiguration;

		protected abstract bool RequiresGuid { get; }
		protected abstract bool RequiresStrId { get; }
		public abstract string TemplateType { get; }
		protected abstract PromptPartConfigurationBase<V> Configuration { get; }

		public PromptPartManagerBase()
		{
			_templateType = TemplateType;
			_capturedConfiguration = Configuration;

			_capturedConfiguration.PromptParts.CollectionChanged += PromptParts_CollectionChanged;
			PromptParts_CollectionChanged(null, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, _capturedConfiguration.PromptParts));
		}

		protected abstract void PopulateFromMetadata(V part, IMetadataCollection metadata, bool isLocalized);
		protected abstract void PopulateLocalized(V original, V localized);
		protected abstract K GetKey(V part);

		private void PromptParts_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
		{
			_lock.Enter();
			try
			{
				if (e.OldItems != null)
					foreach (var _item in e.OldItems)
					{
						if (_item is not V part)
							continue;
						if (!_parts.Remove(part))
							continue;
						var key = GetKey(part);
						_byKey[key].Remove(part);
						_bySource[PromptPartSource.Configuration].Remove(part);
					}

				if (e.NewItems != null)
					foreach (var _item in e.NewItems)
					{
						if (_item is not V part)
							continue;
						if (!_parts.Add(part))
							continue;
						var key = GetKey(part);
						if (!_byKey.TryGetValue(key, out var keySet))
							_byKey[key] = keySet = [];
						keySet.Add(part);
						if (!_bySource.TryGetValue(PromptPartSource.Configuration, out var sourceSet))
							_bySource[PromptPartSource.Configuration] = sourceSet = [];
						sourceSet.Add(part);
						part.Source = PromptPartSource.Configuration;
					}
			}
			finally
			{
				_lock.Exit();
			}
		}

		public bool ImportFromTemplate(ITemplate template, PromptPartSource source)
		{
			ArgumentNullException.ThrowIfNull(template);

			if (_byTemplate.ContainsKey(template))
				return false;

			if (source is not PromptPartSource.BuiltInTemplate and not PromptPartSource.UserTemplate and not PromptPartSource.WorkdirTemplate)
				throw new ArgumentException("Invalid source type, must be one of BuiltInTemplate, UserTemplate, or WorkdirTemplate.", nameof(source));

			var type = template.Metadata.TryGetAdditional<string>("type");
			if (type is null)
				throw new InvalidDataException($"Importing template missing 'type' metadata.");
			if (type != _templateType)
				throw new InvalidDataException($"Importing template (type:'{type ?? "null"}') does not match expected type '{_templateType}'.");

			var diagnosticCode = PromptPartDiagnosticCode.None;
			bool isDiagnosticFatal = false;
			V part;

			string id = string.Empty;
			Guid guid = Guid.Empty;
			string strid = string.Empty;
			LanguageCode langCode = LanguageCode.Invariant;
			string? title = null, description = null, category = null;
			LanguageCode? localizedFor = null;
			ParameterSchema? parameterSchema = null;
			ParameterSchemaParsingDiagnostic? parameterSchemaParsingDiagnostic = null;

			try
			{
				id = template.Metadata.TryGet<TemplateIdentifierMetadata>()?.Identifier!;
				if (id is null)
				{
					diagnosticCode |= PromptPartDiagnosticCode.MissingTemplateIdentifier;
					id = string.Empty;
				}

				var guidStr = template.Metadata.TryGetAdditional<string>("guid");
				if (string.IsNullOrWhiteSpace(guidStr))
				{
					if (RequiresGuid)
					{
						diagnosticCode |= PromptPartDiagnosticCode.MissingGuid;
						isDiagnosticFatal = true;
					}
				}
				else
				{
					if (!Guid.TryParse(guidStr, out guid))
					{
						diagnosticCode |= PromptPartDiagnosticCode.InvalidGuid;
						isDiagnosticFatal = true;
						guid = Guid.Empty;
					}
				}

				strid = template.Metadata.TryGetAdditional<string>("strid")!;
				if (string.IsNullOrWhiteSpace(strid))
				{
					if (RequiresStrId)
					{
						diagnosticCode |= PromptPartDiagnosticCode.MissingStrId;
						isDiagnosticFatal = true;
					}
					strid = string.Empty;
				}

				var lang = template.Metadata.TryGet<LanguageMetadata>();
				langCode = lang?.LanguageCode ?? LanguageCode.Invariant;
				if (lang is null)
				{
					diagnosticCode |= PromptPartDiagnosticCode.MissingLanguage;
				}

				var parameterSchemaData = template.Metadata.TryGet<ParameterSchemaTemplateMetadata>();
				var parameterSchemaParser = ServiceRegistry.Provider.GetService<IParameterSchemaParserManager>();
				if (parameterSchemaData is not null && parameterSchemaParser is not null)
				{
					try
					{
						var errors = new AppendOnlyList<ParameterSchemaParsingError>();
						parameterSchema = parameterSchemaParser.ParseRoot(parameterSchemaData.Value, errors);

						if (errors.Count > 0)
						{
							parameterSchemaParsingDiagnostic = new ParameterSchemaParsingDiagnostic
							{
								IsFatal = false,
								Errors = errors.ToImmutableList(),
								Exception = null
							};
							diagnosticCode |= PromptPartDiagnosticCode.InvalidParameterSchema;
						}
					}
					catch (Exception ex)
					{
						parameterSchemaParsingDiagnostic = new ParameterSchemaParsingDiagnostic
						{
							IsFatal = true,
							Errors = [],
							Exception = ex
						};
						diagnosticCode |= PromptPartDiagnosticCode.InvalidParameterSchema;
					}
				}

				title = template.Metadata.TryGetAdditional<string>("title");
				description = template.Metadata.TryGetAdditional<string>("description");
				category = template.Metadata.TryGetAdditional<string>("category");
				var localizedForRaw = template.Metadata.TryGetAdditional<string>("localized_for");
				localizedFor = localizedForRaw != null ? new LanguageCode(localizedForRaw) : null;

				part = new V
				{
					Guid = guid,
					StrId = strid,
					Name = title ?? id,
					Description = description,
					Category = category,
					Language = langCode,
					LocalizedFor = localizedFor,
					Source = source,
					Diagnostic = new PromptPartDiagnostic
					{
						IsFatal = isDiagnosticFatal,
						Code = diagnosticCode,
						Messages = [],
						Exception = null
					},
					Template = new SerializableTemplate(template),
					ParameterSchema = parameterSchema,
					ParameterSchemaParsingDiagnostic = parameterSchemaParsingDiagnostic
				};
				PopulateFromMetadata(part, template.Metadata, localizedFor != null);
			}
			catch (Exception ex)
			{
				part = new V
				{
					Guid = guid,
					StrId = strid,
					Name = title ?? id,
					Description = description,
					Category = category,
					Language = langCode,
					LocalizedFor = localizedFor,
					Source = source,
					Diagnostic = new PromptPartDiagnostic
					{
						IsFatal = isDiagnosticFatal,
						Code = diagnosticCode,
						Messages = [ex.Message],
						Exception = ex
					},
					ParameterSchema = parameterSchema,
					ParameterSchemaParsingDiagnostic = parameterSchemaParsingDiagnostic
				};
			}
			var key = GetKey(part);
			var imported = false;

			_lock.Enter();
			try
			{
				_parts.Add(part);
				if (!_byKey.TryGetValue(key, out var keySet))
					_byKey[key] = keySet = [];
				keySet.Add(part);
				if (!_bySource.TryGetValue(source, out var sourceSet))
					_bySource[source] = sourceSet = [];
				sourceSet.Add(part);
				_byTemplate[template] = part;
				_templateSourceKeys[template] = (key, source);

				imported = true;
			}
			finally
			{
				_lock.Exit();
			}

			if (imported)
				Changed?.Invoke(this, EventArgs.Empty);
			return imported;
		}

		public bool DropTemplate(ITemplate template)
		{
			var dropped = false;

			_lock.Enter();
			try
			{
				if (!_byTemplate.TryGetValue(template, out var part))
					return false;

				_parts.Remove(part);
				_byTemplate.Remove(template);
				var (key, source) = _templateSourceKeys[template];
				_templateSourceKeys.Remove(template);
				_byKey[key].Remove(part);
				_bySource[source].Remove(part);

				dropped = true;
			}
			finally
			{
				_lock.Exit();
			}

			if (dropped)
				Changed?.Invoke(this, EventArgs.Empty);
			return dropped;
		}

		private static readonly HierarchicalLanguageFallbackScheme _langFallbackScheme = new(LanguageCode.Invariant);

		private V? PickBest(IEnumerable<V> parts)
		{
			var promptLanguage = ApplicationSettingsAccessor.ApplicationSettings.Language.Prompt;
			var interfaceLanguage = ApplicationStartupSettingsAccessor.Current.Language;
			var preferredLanguageCode = new LanguageCode((promptLanguage ?? interfaceLanguage).ToNullIfEmpty() ?? "iv");
			var languageGroups = parts.ToLookup(p => p.Language);
			var selectedLanguage = _langFallbackScheme.GetFallbackLanguage(preferredLanguageCode, languageGroups.Select(g => g.Key));

			var selectedPart = languageGroups[selectedLanguage].MaxBy(p => p.Source switch
			{
				PromptPartSource.BuiltInTemplate => 1,
				PromptPartSource.UserTemplate => 2,
				PromptPartSource.Configuration => 3,
				PromptPartSource.WorkdirTemplate => 4,
				_ => 0
			});

			if (selectedPart is null)
				return selectedPart;

			if (selectedPart.LocalizedFor is not null)
			{
				var selectedPartToLocalize = languageGroups[selectedPart.LocalizedFor.Value].MaxBy(p => p.Source switch
				{
					PromptPartSource.BuiltInTemplate => 1,
					PromptPartSource.UserTemplate => 2,
					PromptPartSource.Configuration => 3,
					PromptPartSource.WorkdirTemplate => 4,
					_ => 0
				});
				
				if (selectedPartToLocalize is not null)
				{
					PopulateLocalized(selectedPartToLocalize, selectedPart);
					selectedPart.LocalizedTemplate = selectedPartToLocalize.Template;
				}
			}

			return selectedPart;
		}

		private IEnumerable<V> PickBestMultiple(IEnumerable<V> parts)
		{
			return parts.GroupBy(GetKey)
				.Select(PickBest)
				.Where(p => p != null)!;
		}

		public V? TryGet(K key)
		{
			_lock.Enter();
			try
			{
				return _byKey.TryGetValue(key, out var parts) && parts.Count > 0 ? PickBest(parts) : null;
			}
			finally
			{
				_lock.Exit();
			}
		}

		public V? TryGet(ITemplate template)
		{
			_lock.Enter();
			try
			{
				return _byTemplate.TryGetValue(template, out var part) ? part : null;
			}
			finally
			{
				_lock.Exit();
			}
		}

		public ITemplate? TryGetTemplate(K key)
		{
			_lock.Enter();
			try
			{
				var result = _byKey.TryGetValue(key, out var parts) && parts.Count > 0 ? PickBest(parts) : null;
				return result != null ? (result.LocalizedTemplate ?? result.Template).Template : null;
			}
			finally
			{
				_lock.Exit();
			}
		}

		public IEnumerable<V> GetAll()
		{
			_lock.Enter();
			try
			{
				return [.. PickBestMultiple(_parts)];
			}
			finally
			{
				_lock.Exit();
			}
		}

		public IEnumerable<V> GetAll(PromptPartSource templateSource)
		{
			_lock.Enter();
			try
			{
				return _bySource.TryGetValue(templateSource, out var parts) ? [.. PickBestMultiple(parts)] : [];
			}
			finally
			{
				_lock.Exit();
			}
		}
	}
}
