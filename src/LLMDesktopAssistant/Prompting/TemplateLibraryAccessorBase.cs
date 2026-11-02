using System.Globalization;
using LLMDesktopAssistant.Settings.Application;
using LLMDesktopAssistant.Utils;
using LLTSharp;
using LLTSharp.Locale;
using LLTSharp.Metadata;
using LLTSharp.Metadata.Types;

namespace LLMDesktopAssistant.Prompting
{
	public abstract class TemplateLibraryAccessorBase : ITemplateLibraryAccessor
	{
		public abstract TemplateLibrary Library { get; }

		private static LanguageMetadata GetCurrentLanguageMetadata()
		{
			var promptLanguage = ApplicationSettingsAccessor.ApplicationSettings.Language.Prompt;
			var interfaceLanguage = ApplicationStartupSettingsAccessor.Current.Language;
			var targetLanguage = (promptLanguage ?? interfaceLanguage).ToNullIfEmpty() ?? "iv";
			return new LanguageMetadata(new LanguageCode(targetLanguage));
		}

		private T GetTemplateInternal<T>(string id, IMetadata[] metadata)
			where T : ITemplate
		{
			var languageMetadata = GetCurrentLanguageMetadata();
			if (metadata.Length == 0)
				metadata = [new TemplateIdentifierMetadata(id), languageMetadata];
			else
				metadata = [new TemplateIdentifierMetadata(id), languageMetadata, .. metadata];
			var lastTemplate = (Library.TryRetrieveBestAllWithFallback(metadata)?.LastOrDefault())
				?? throw new KeyNotFoundException($"No templates found for the given ID '{id}'");
			if (lastTemplate is not T result)
				throw new InvalidCastException($"The retrieved template of ID '{id}' is not an instance of {typeof(T)}.");
			return result;
		}

		public ITemplate GetTemplate(string id, params IMetadata[] metadata)
		{
			return GetTemplateInternal<ITemplate>(id, metadata);
		}

		public ITextTemplate GetTextTemplate(string id, params IMetadata[] metadata)
		{
			return GetTemplateInternal<ITextTemplate>(id, metadata);
		}

		public IMessagesTemplate GetMessagesTemplate(string id, params IMetadata[] metadata)
		{
			return GetTemplateInternal<IMessagesTemplate>(id, metadata);
		}
	}
}
