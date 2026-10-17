using System.ComponentModel;
using System.Text.Json.Nodes;
using AngleSharp.Html.Parser;
using LLMDesktopAssistant.LLM.Services;
using LLMDesktopAssistant.Localization;
using LLMDesktopAssistant.Tools.Execution.Domain;
using LLMDesktopAssistant.Tools.Execution.Support;
using LLMDesktopAssistant.Utils;
using LLMDesktopAssistant.Utils.Web;
using Material.Icons;
using RCLargeLanguageModels.Json.Schema;

namespace LLMDesktopAssistant.Tools.Implementations.Web
{
	[ToolModule(chatScoped: true)]
	public class WebFetchToolModule : ToolModule
	{
		private readonly IWebFetcher _webFetcher;
		private readonly AsyncCache<(string Url, WebFetchLevel Level), FetchResult> _fetchContentCache;

		public WebFetchToolModule(IWebFetcher webFetcher)
		{
			_webFetcher = webFetcher;
			_fetchContentCache = new AsyncCache<(string Url, WebFetchLevel Level), FetchResult>(
				async ((string Url, WebFetchLevel Level) args, CancellationToken cancellationToken) =>
				{
					return await _webFetcher.FetchAsync(args.Url, args.Level, cancellationToken);
				}, slidingExpirationTime: TimeSpan.FromMinutes(15));

			AddTool(new ToolInitializationInfo
			{
				Executor = Fetch,
				StreamingAnalyzer = FetchStreaming,
				Name = "web-fetch",
				Description = "Fetch webcite content from a specified URL.",
				NameKey = Locale.GetKey("tool.name.web-fetch"),
				DescriptionKey = Locale.GetKey("tool.description.web-fetch"),
				CategoryKey = Locale.GetKey("tool.category.web"),
				DefaultExpectedBehaviour = ToolBehaviour.InternetAccess,
				ModifyArgumentSchema = RemoveUnsupportedArguments
			});

			AddTool(new ToolInitializationInfo
			{
				Executor = ParseHtml,
				StreamingAnalyzer = ParseHtmlStreaming,
				Name = "web-parse",
				Description = "Fetch HTML content and parse specific elements by tag or class.",
				NameKey = Locale.GetKey("tool.name.web-parse"),
				DescriptionKey = Locale.GetKey("tool.description.web-parse"),
				CategoryKey = Locale.GetKey("tool.category.web"),
				DefaultExpectedBehaviour = ToolBehaviour.InternetAccess,
				ModifyArgumentSchema = RemoveUnsupportedArguments
			});
		}

		private void RemoveUnsupportedArguments(JsonObject schema)
		{
			if (schema["properties"]?.AsObject()["fetchLevel"] is not JsonObject fetchLevelProperty)
				return;
			if (fetchLevelProperty["enum"] is not JsonArray enumValues)
				return;

			var maxLevel = _webFetcher.MaxLevel;
			for (int i = enumValues.Count - 1; i >= 0; i--)
			{
				if (enumValues[i]?.GetValue<string>() is string value &&
					Enum.TryParse<WebFetchLevel>(value, ignoreCase: true, out var level) &&
					level > maxLevel)
				{
					enumValues.RemoveAt(i);
				}
			}
		}

		private static WebFetchLevel ParseLevel(string fetchLevel)
			=> Enum.TryParse<WebFetchLevel>(fetchLevel, ignoreCase: true, out var level) ? level : WebFetchLevel.HttpClient;

		private StreamingToolAnalysisResult FetchStreaming(
			string? url,
			IToolExecutionContext ctx)
		{
			ctx.Call.SetStatus(MaterialIconKind.Web, $"`{url}`");
			return StreamingToolAnalysisResult.Continue;
		}

		private async Task<ToolExecutionResult> Fetch(
			IToolExecutionContext ctx,
			[Description("URL to fetch HTML from")]
			string url,
			[Description("The starting index of character to return")]
			int start = 0,
			[Description("The maximum count of characters to return")]
			int count = 10000,
			[Description("The content type to fetch")]
			[Enum(["html", "sanitized_html", "markdown"])]
			string contentType = "markdown",
			[Description("The fetch level: plain HTTP request, headless browser (renders JavaScript), or stealth browser (evades advanced anti-bot checks). Escalates automatically when the page denies access.")]
			[Enum(["HttpClient", "Browser", "StealthBrowser"])]
			string fetchLevel = "HttpClient",
			CancellationToken cancellationToken = default)
		{
			ctx.Call.SetStatus(MaterialIconKind.Web, $"`{url}`");

			try
			{
				var level = ParseLevel(fetchLevel);
				var html = await _fetchContentCache.GetAsync((url, level), cancellationToken);
				var content = contentType switch
				{
					"markdown" => HtmlToMarkdownConverter.Convert(html.Html),
					"sanitized_html" => HtmlSanitizer.Sanitize(html.Html),
					_ => html.Html
				};

				start = Math.Max(Math.Min(start, content.Length), 0);
				count = Math.Min(count, content.Length - start);
				int end = start + count;
				var slice = content.Substring(start, count);

				string afterTip = end < content.Length ? $"\n*Can read {content.Length - end} more characters. Call tool again with same arguments (but with new `start` and `count` values) to read more.*" : "";

				ctx.Call.ResultContent = $"""
					**Url**: *{url}*
					**Showing slice**: *{start}-{start + count}* from *{content.Length}*
					[CONTENT START]
					{slice}
					[CONTENT END]{afterTip}
					""";

				return ToolExecutionResult.Succeeded;
			}
			catch (Exception ex)
			{
				ctx.Call.ResultContent = $"Error fetching web content: {ex.Message}";
				return ToolExecutionResult.Failed;
			}
		}

		private StreamingToolAnalysisResult ParseHtmlStreaming(
			string? url, string? selector,
			IToolExecutionContext ctx)
		{
			ctx.Call.SetStatus(MaterialIconKind.Web, selector != null ? $"`{url}` → `{selector}`" : $"`{url}`");
			return StreamingToolAnalysisResult.Continue;
		}

		private async Task<ToolExecutionResult> ParseHtml(
			IToolExecutionContext ctx,
			[Description("URL to fetch HTML from")]
			string url,
			[Description("The query selector to select values with")]
			string selector,
			[Description("The fetch level: plain HTTP request, headless browser (renders JavaScript), or stealth browser (evades advanced anti-bot checks). Escalates automatically when the page denies access.")]
			[Enum(["HttpClient", "Browser", "StealthBrowser"])]
			string fetchLevel = "HttpClient",
			[Description("The starting index of character to return")]
			int start = 0,
			[Description("The maximum count of characters to return")]
			int count = 10000,
			CancellationToken cancellationToken = default)
		{
			ctx.Call.SetStatus(MaterialIconKind.Web, $"`{url}` → `{selector}`");

			try
			{
				var level = ParseLevel(fetchLevel);
				var html = await _fetchContentCache.GetAsync((url, level), cancellationToken);
				var parser = new HtmlParser();
				var document = await parser.ParseDocumentAsync(html.Html);
				var elements = document.QuerySelectorAll(selector);
				var contents = elements.Select(m => m.TextContent);

				var content = string.Join("\n\n", contents);
				start = Math.Max(Math.Min(start, content.Length), 0);
				count = Math.Min(count, content.Length - start);
				int end = start + count;
				var slice = content.Substring(start, count);

				string afterTip = end < content.Length ? $"\n*Can read {content.Length - end} more characters. Call tool again with same arguments (but with new `start` and `count` values) to read more.*" : "";

				ctx.Call.ResultContent = $"""
					**Url**: *{url}*
					**Selector**: *{selector}*
					**Showing slice**: *{start}-{start + count}* from *{content.Length}*
					[CONTENT START]
					{slice}
					[CONTENT END]{afterTip}
					""";
				return ToolExecutionResult.Succeeded;
			}
			catch (Exception ex)
			{
				ctx.Call.ResultContent = $"Error parsing HTML: {ex.Message}";
				return ToolExecutionResult.Failed;
			}
		}

	}
}
