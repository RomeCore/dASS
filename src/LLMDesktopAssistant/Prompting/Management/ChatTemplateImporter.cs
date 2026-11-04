using System.Collections.Specialized;
using LLMDesktopAssistant.Addons.Sources;
using LLMDesktopAssistant.LLM.Services;
using LLMDesktopAssistant.StructuredValues.Parameterization;
using LLMDesktopAssistant.Utils;
using LLTSharp;
using LLTSharp.Locale;
using LLTSharp.Metadata;

namespace LLMDesktopAssistant.Prompting.Management
{
	[ChatService(typeof(IChatTemplateImporter))]
	public class ChatTemplateImporter : Disposable, IChatTemplateImporter
	{
		private readonly TemplateLibrary _library;
		private readonly IAppTemplateImporter _appTemplateImporter;
		private readonly IFileAddonAccessor<ITemplate> _templatesAccessor;
		private readonly Dictionary<string, IImportablePromptPartManager> _promptPartManagers;
		private readonly RangeObservableCollection<ITemplate> _templates = [];

		public TemplateLibrary Library => _library;
		public ReadOnlyObservableCollection<ITemplate> Templates =>
			field ??= new ReadOnlyObservableCollection<ITemplate>(_templates);

		public ChatTemplateImporter(IAppTemplateImporter appTemplateImporter,
			IFileAddonAccessor<ITemplate> templatesAccessor,
			IEnumerable<IImportablePromptPartManager> promptPartManagers)
		{
			_library = new TemplateLibrary();
			_library.MetadataFactories.Add(new ParameterSchemaTemplateMetadataFactory());
			_library.SetLanguageFallbackScheme(new HierarchicalLanguageFallbackScheme(LanguageCode.Invariant));

			_appTemplateImporter = appTemplateImporter;
			_templatesAccessor = templatesAccessor;
			_promptPartManagers = promptPartManagers.ToDictionary(t => t.TemplateType);

			foreach (var template in appTemplateImporter.BuiltInTemplates)
			{
				_library.Add(template);
				_templates.Add(template);
				if (template.Metadata.TryGetAdditional<string>("type") is string type &&
					_promptPartManagers.TryGetValue(type, out var manager))
				{
					manager.ImportFromTemplate(template, PromptPartSource.BuiltInTemplate);
				}
			}

			_templatesAccessor.Addons.CollectionChanged += Templates_CollectionChanged;
			Templates_CollectionChanged(null, new NotifyCollectionChangedEventArgs(
				NotifyCollectionChangedAction.Add, _templatesAccessor.Addons));
		}

		protected override void Dispose(bool disposing)
		{
			base.Dispose(disposing);

			if (disposing)
			{
				_templatesAccessor.Addons.CollectionChanged -= Templates_CollectionChanged;
			}
		}

		private void Templates_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
		{
			if (e.Action is NotifyCollectionChangedAction.Move)
				return;

			if (e.OldItems != null)
				foreach (var item in e.OldItems)
				{
					if (item is not ITemplate template)
						continue;
					_library.Remove(template);
					_templates.Remove(template);

					if (template.Metadata.TryGetAdditional<string>("type") is string type &&
						_promptPartManagers.TryGetValue(type, out var manager))
					{
						manager.DropTemplate(template);
					}
				}

			if (e.NewItems != null)
				foreach (var item in e.NewItems)
				{
					if (item is not ITemplate template)
						continue;
					_library.Add(template);
					_templates.Add(template);

					if (template.Metadata.TryGetAdditional<string>("type") is string type &&
						_promptPartManagers.TryGetValue(type, out var manager))
					{
						manager.ImportFromTemplate(template, PromptPartSource.UserTemplate);
					}
				}
		}
	}
}
