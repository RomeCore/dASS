using System.Collections.Specialized;
using LLMDesktopAssistant.Addons;
using LLMDesktopAssistant.Addons.Management;
using LLMDesktopAssistant.Services;
using LLMDesktopAssistant.StructuredValues.Parameterization;
using LLMDesktopAssistant.Utils;
using LLTSharp;
using LLTSharp.Locale;

namespace LLMDesktopAssistant.Prompting.Management
{
	[Service(typeof(IAppTemplateImporter))]
	public class AppTemplateImporter : IAppTemplateImporter
	{
		private readonly TemplateLibrary _library;
		private readonly IAddonAccessor<ITemplate> _templatesAccessor;
		private readonly ITemplateParser _templateParser;

		private readonly ImmutableList<ITemplate> _builtInTemplates;
		private readonly RangeObservableCollection<ITemplate> _templates = [];

		public TemplateLibrary Library => _library;
		public IEnumerable<ITemplate> BuiltInTemplates => _builtInTemplates;
		public ReadOnlyObservableCollection<ITemplate> Templates =>
			field ??= new ReadOnlyObservableCollection<ITemplate>(_templates);
		
		public AppTemplateImporter(IAddonAccessor<ITemplate> templatesAccessor, ITemplateParser templateParser)
		{
			_library = [];
			_library.MetadataFactories.Add(new ParameterSchemaTemplateMetadataFactory());
			_library.SetLanguageFallbackScheme(new HierarchicalLanguageFallbackScheme(LanguageCode.Invariant));

			_templatesAccessor = templatesAccessor;
			_templateParser = templateParser;

			var builtInTemplatesBuilder = ImmutableList.CreateBuilder<ITemplate>();
			var embeddedLoadingErrors = new List<Exception>();
			var supportedExtensions = _templateParser.SupportedExtensions.Select(e => e.TrimStart('.')).ToHashSet();
			foreach (var asm in ReflectionUtility.ObservedAssemblies)
			{
				foreach (var embeddedResource in asm.GetManifestResourceNames())
				{
					var extension = embeddedResource.Split('.').Last();
					if (supportedExtensions.Contains(extension))
					{
						try
						{
							using var stream = asm.GetManifestResourceStream(embeddedResource);
							if (stream is not null)
							{
								using var reader = new StreamReader(stream);
								var content = reader.ReadToEnd(); ;
								builtInTemplatesBuilder.AddRange(_templateParser.Parse(content, extension));
							}
						}
						catch (Exception ex)
						{
							embeddedLoadingErrors.Add(ex);
						}
					}
				}
			}
			if (embeddedLoadingErrors.Count > 0)
				throw new AggregateException("Failed to load built-in templates.", embeddedLoadingErrors);

			_builtInTemplates = builtInTemplatesBuilder.ToImmutable();
			_library.AddRange(_builtInTemplates);
			_templates.AddRange(_builtInTemplates);

			_templatesAccessor.Addons.CollectionChanged += Addons_CollectionChanged;
			Addons_CollectionChanged(null, new NotifyCollectionChangedEventArgs(
				NotifyCollectionChangedAction.Add, _templatesAccessor.Addons));
		}

		private void Addons_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
		{
			if (e.Action is NotifyCollectionChangedAction.Move)
				return;

			if (e.OldItems != null)
				foreach (var item in e.OldItems)
					if (item is ITemplate template)
					{
						_library.Remove(template);
						_templates.Remove(template);
					}

			if (e.NewItems != null)
				foreach (var item in e.NewItems)
					if (item is ITemplate template)
					{
						_library.Add(template);
						_templates.Add(template);
					}
		}
	}
}
