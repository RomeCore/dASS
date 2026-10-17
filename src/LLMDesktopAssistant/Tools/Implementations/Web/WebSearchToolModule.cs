using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Text;
using LLMDesktopAssistant.Localization;
using LLMDesktopAssistant.Tools.Execution.Domain;
using LLMDesktopAssistant.Tools.Execution.Support;
using Material.Icons;
using RCLargeLanguageModels.Json.Schema;
using SearXSharp;
using SearXSharp.Models;

namespace LLMDesktopAssistant.Tools.Implementations.Web
{
	/// <summary>
	/// Tool module that provides web search capabilities to the AI assistant
	/// using the built-in search engines (Google, Bing, etc.).
	/// Replaces the external SearXNG dependency with native C# search engine implementations.
	/// Based on SearXNG's engine architecture with multiple parallel search backends.
	/// </summary>
	[ToolModule(chatScoped: false)]
	public class WebSearchToolModule : ToolModule
	{
		private readonly SearchEngineManager _searchManager;

		/// <summary>
		/// Initializes a new instance of the <see cref="WebSearchToolModule"/> class.
		/// </summary>
		/// <param name="searchManager">The search engine manager that orchestrates all registered engines.</param>
		public WebSearchToolModule(SearchEngineManager searchManager)
		{
			_searchManager = searchManager;

			AddTool(new ToolInitializationInfo
			{
				Executor = GetEngines,
				Name = "web-search_get_engines",
				Description = """
					Gets list of available search engines that can be used in `web-search`.
					""",
				NameKey = Locale.GetKey("tool.name.web-search_get_engines"),
				DescriptionKey = Locale.GetKey("tool.description.web-search_get_engines"),
				CategoryKey = Locale.GetKey("tool.category.web"),
				DefaultExpectedBehaviour = ToolBehaviour.None
			});

			AddTool(new ToolInitializationInfo
			{
				Executor = Search,
				StreamingAnalyzer = SearchStreaming,
				Name = "web-search",
				Description = """
					Search through the web using SearXNG and web scraping techniques.
					Returns results from multiple search engines (Google, Bing, etc.).
					""",
				NameKey = Locale.GetKey("tool.name.web-search"),
				DescriptionKey = Locale.GetKey("tool.description.web-search"),
				CategoryKey = Locale.GetKey("tool.category.web"),
				DefaultExpectedBehaviour = ToolBehaviour.InternetAccess
			});
		}

		private ToolExecutionResult GetEngines(IToolExecutionContext ctx)
		{
			ctx.Call.SetStatusIcon(MaterialIconKind.SearchExpand);
			ctx.Call.UseMarkdown = true;

			var sb = new StringBuilder();
			int counter = 0;
			foreach (var engine in _searchManager.Engines)
			{
				counter++;
				sb.AppendLine($"{counter}. **{engine.Name}**: *{engine.DisplayName}*");
				sb.AppendLine($"  Supported categories: {string.Join(", ",
					engine.SupportedCategories.Select(c => c.ToString().ToLower().Replace("general", "all")))}");
				if (engine.SupportsPaging)
					sb.AppendLine($"  Supports paging, max pages: {(engine.MaxPages == 0 ? "unlimited" : engine.MaxPages)}");
				if (engine.SupportsSafeSearch)
					sb.AppendLine($"  Supports safe search");
				if (engine.SupportsTimeRange)
					sb.AppendLine($"  Supports time range");
				sb.AppendLine();
			}
			ctx.Call.ResultContent = sb.ToString();

			return ToolExecutionResult.Succeeded;
		}

		private StreamingToolAnalysisResult SearchStreaming(
			string? query,
			IToolExecutionContext ctx)
		{
			ctx.Call.SetStatus(MaterialIconKind.Search, query);
			return StreamingToolAnalysisResult.Continue;
		}

		/// <summary>
		/// Performs a search across all available search engines with the specified category.
		/// Returns ALL non-default fields from SearchResult for rich metadata.
		/// </summary>
		private async Task<ToolExecutionResult> Search(
			IToolExecutionContext ctx,
			[Description("The query to search by")] string query,
			[Description("The page number to return results for (1-based)"), Range(1, 10)] int page = 1,
			[Description("Language code (auto, en, ru, etc.)")] string language = "auto",
			[Description("Time range filter: day, week, month, year, or empty for no filter")]
			[Enum(["", "day", "week", "month", "year"])] string timeRange = "",
			[Description("Safe search level: none, moderate, strict")]
			[Enum(["none", "moderate", "strict"])] string safeSearch = "none",
			[Description("Search category.")]
			[Enum(["all", "web", "images", "videos", "news", "science", "it", "files", "socialmedia", "music", "map", "repos", "packages"])] string category = "web",
			[Description("The engines to use for the search. If null, all available engines are used. It's not recommended to limit engines to a few amount, as this can lead to incomplete results.")] string[]? engines = null,
			[Description("Maximum number of results (per engine) to return"), Range(1, 50)] int maxResults = 10,
			CancellationToken ct = default)
		{
			ctx.Call.SetStatus(MaterialIconKind.Search, $"{query}");
			ctx.Call.UseMarkdown = true;

			try
			{
					var searchQuery = BuildSearchQuery(query, page, language, timeRange, safeSearch, category, engines, maxResults);
					var resultList = await _searchManager.SearchAllAsync(searchQuery, ct);

					if (resultList.Results.Count == 0 && resultList.UnresponsiveEngines.Count > 0)
					{
						var errors = string.Join("; ", resultList.UnresponsiveEngines.Select(e =>
							$"{e.Engine}: {e.ErrorType}{(e.IsSuspended ? " (suspended)" : "")}"));

						ctx.Call.SetStatusIcon(MaterialIconKind.SearchMinus);
						ctx.Call.ResultContent = $"All search engines failed: {errors}";
						return ToolExecutionResult.Failed;
					}

					var allEngines = resultList.Results.SelectMany(r => r.Engines.Count > 0 ? r.Engines : [r.Engine])
						.Concat(resultList.EngineTimings.Select(e => e.Engine)).Distinct().ToList();
					var engineResults = resultList.Results.SelectMany(r => r.Engines.Count > 0 ?
						r.Engines.Select(e => (r, e)) : [(r, r.Engine)]).GroupBy(r => r.r.Engine).ToDictionary(k => k.Key, v => v.Count());
					var engineTimings = resultList.EngineTimings.ToDictionary(e => e.Engine, e => e.TotalTime);
					var engineInfos = allEngines.Select(e =>
					{
						if (!engineResults.TryGetValue(e, out var resultCount))
							resultCount = 0;
						if (!engineTimings.TryGetValue(e, out var totalTime))
							totalTime = double.NaN;
						return (e, resultCount, totalTime);
					}).OrderByDescending(t => t.Item2);

					var sb = new StringBuilder();
					var enginesInfo = string.Join(", ", engineInfos.Select(t => $"{t.Item1} ({t.Item2} results, {t.Item3}ms)"));

					sb.AppendLine($"**Search results for \"{query}\"** (engines: {enginesInfo})");
					sb.AppendLine();

					var count = 0;
					IEnumerable<SearchResult> results = resultList.Results;
					if (searchQuery.Category != SearchCategory.General)
						results = results.OrderBy(r => r.Category == searchQuery.Category ? 0 : 1);

					foreach (var result in results)
					{
						count++;
						sb.AppendLine($"**{count}. [{result.Title}]({result.Url})**");
						if (count > maxResults * 3)
							continue;

						// Content
						if (!string.IsNullOrEmpty(result.Content))
							sb.AppendLine($"  {result.Content.Truncate(500)}");

						// Engine(s)
						var engine = result.Engines.Count > 0 ? result.Engines.First() : result.Engine;
						if (result.Engines.Count > 1)
							sb.AppendLine($"  *Engines: {string.Join(", ", result.Engines)}*");
						else if (!string.IsNullOrEmpty(engine))
							sb.AppendLine($"  *Engine: {engine}*");

						// Source & Metadata
						if (!string.IsNullOrEmpty(result.Source))
							sb.AppendLine($"  Source: {result.Source}");
						if (!string.IsNullOrEmpty(result.Metadata))
							sb.AppendLine($"  Metadata: {result.Metadata}");

						// Author
						if (!string.IsNullOrEmpty(result.Author))
							sb.AppendLine($"  Author: {result.Author}");

						// Published date
						if (result.PublishedDate.HasValue)
							sb.AppendLine($"  Published: {result.PublishedDate:yyyy-MM-dd}");

						// Duration (video/audio)
						if (result.Duration.HasValue && result.Duration.Value > TimeSpan.Zero)
							sb.AppendLine($"  Duration: {result.Duration:hh\\:mm\\:ss}");

						// Image fields
						if (!string.IsNullOrEmpty(result.ImgSrc))
							sb.AppendLine($"  Image URL: {result.ImgSrc}");
						if (!string.IsNullOrEmpty(result.Thumbnail))
							sb.AppendLine($"  Thumbnail: {result.Thumbnail}");
						if (!string.IsNullOrEmpty(result.Resolution))
							sb.AppendLine($"  Resolution: {result.Resolution}");

						// Embedded content
						if (!string.IsNullOrEmpty(result.IframeSrc))
							sb.AppendLine($"  Iframe: {result.IframeSrc}");
						if (!string.IsNullOrEmpty(result.AudioSrc))
							sb.AppendLine($"  Audio: {result.AudioSrc}");

						// Views
						if (result.Views.HasValue && result.Views.Value > 0)
							sb.AppendLine($"  Views: {result.Views:N0}");

						// Torrent fields
						if (result.Seed > 0)
							sb.AppendLine($"  Seed: {result.Seed}");
						if (result.Leech > 0)
							sb.AppendLine($"  Leech: {result.Leech}");
						if (!string.IsNullOrEmpty(result.MagnetLink))
							sb.AppendLine($"  Magnet: {result.MagnetLink}");

						// Paper fields
						if (!string.IsNullOrEmpty(result.Doi))
							sb.AppendLine($"  DOI: {result.Doi}");
						if (result.Authors is { Count: > 0 })
							sb.AppendLine($"  Authors: {string.Join(", ", result.Authors)}");
						if (!string.IsNullOrEmpty(result.Journal))
							sb.AppendLine($"  Journal: {result.Journal}");
						if (result.Tags is { Count: > 0 })
							sb.AppendLine($"  Tags: {string.Join(", ", result.Tags)}");
						if (!string.IsNullOrEmpty(result.Comments))
							sb.AppendLine($"  Comments: {result.Comments}");
						if (!string.IsNullOrEmpty(result.PdfUrl))
							sb.AppendLine($"  PDF: {result.PdfUrl}");
						if (!string.IsNullOrEmpty(result.Number))
							sb.AppendLine($"  Number: {result.Number}");
						if (!string.IsNullOrEmpty(result.Pages))
							sb.AppendLine($"  Pages: {result.Pages}");
						if (!string.IsNullOrEmpty(result.Volume))
							sb.AppendLine($"  Volume: {result.Volume}");

						// Geo fields
						if (Math.Abs(result.Latitude) > 0.001 || Math.Abs(result.Longitude) > 0.001)
							sb.AppendLine($"  Location: {result.Latitude}, {result.Longitude}");

						// Score & Position
						if (result.Score > 0)
							sb.AppendLine($"  Score: {result.Score:F2}");
						if (result.Position > 0)
							sb.AppendLine($"  Position: {result.Position}");

						// Type & Category
						if (result.Type != SearchResultType.Default)
							sb.AppendLine($"  Type: {result.Type}");
						if (result.Category != SearchCategory.General)
							sb.AppendLine($"  Category: {result.Category}");

						sb.AppendLine();
					}

					if (count > maxResults * 3)
						sb.AppendLine();

					// Suggestions
					if (resultList.Suggestions.Count > 0)
					{
						sb.AppendLine("**Suggestions:** " + string.Join(", ", resultList.Suggestions.Take(5)));
						sb.AppendLine();
					}

					// Corrections
					if (resultList.Corrections.Count > 0)
					{
						sb.AppendLine("**Corrections:** " + string.Join(", ", resultList.Corrections.Take(5)));
						sb.AppendLine();
					}

					// Infoboxes
					if (resultList.Infoboxes.Count > 0)
					{
						sb.AppendLine("**Infoboxes:**");
						foreach (var info in resultList.Infoboxes.Take(3))
						{
							sb.AppendLine($"- **[{info.Title}]({info.Url})**");
							if (!string.IsNullOrEmpty(info.Content))
								sb.AppendLine($"  {info.Content.Truncate(500)}");
						}
						sb.AppendLine();
					}

					// Answers
					if (resultList.Answers.Count > 0)
					{
						sb.AppendLine("**Direct answers:**");
						foreach (var answer in resultList.Answers.Take(3))
						{
							sb.AppendLine($"- {answer.Answer}");
							if (!string.IsNullOrEmpty(answer.Url))
								sb.AppendLine($"  *Source: {answer.Url}*");
						}
						sb.AppendLine();
					}

					// Total results info
					if (resultList.TotalResults.HasValue)
						sb.AppendLine($"*Total results: {resultList.TotalResults:N0}*");

					// Unresponsive engines
					if (resultList.UnresponsiveEngines.Count > 0)
					{
						sb.AppendLine($"*Note: some engines ({string.Join(", ", resultList.UnresponsiveEngines.Select(e => e.Engine))}) did not respond*");
					}

					// Fewer results than expected
					if (resultList.Results.Count < maxResults * 2)
					{
						sb.AppendLine("*Note: fewer results than expected. Consider change the category (some categories have few number of engines and low quality for results, especially 'news' or 'it'), using more engines (at least 7-8), or using 'auto' language if needed.*");
					}

					ctx.Call.SetStatusTitle(LocalizationManager.LocalizeStaticFormat("tool.status.web-search.results", $"{query}", resultList.TotalResults ?? 0));
					ctx.Call.ResultContent = sb.ToString().Trim();
					return ToolExecutionResult.Succeeded;
				}
				catch (Exception ex)
				{
					ctx.Call.SetStatusIcon(MaterialIconKind.SearchMinus);
					ctx.Call.ResultContent = $"Web search failed: {ex.Message}";
					return ToolExecutionResult.Failed;
				}
		}

		/// <summary>
		/// Builds a <see cref="SearchQuery"/> from tool parameters.
		/// </summary>
		private static SearchQuery BuildSearchQuery(
			string query, int page, string language, string timeRange, string safeSearch,
			string category, string[]? engines, int maxResults)
		{
			var searchCategory = category?.ToLowerInvariant() switch
			{
				"all" => SearchCategory.General,
				"web" => SearchCategory.Web,
				"images" => SearchCategory.Images,
				"videos" => SearchCategory.Videos,
				"news" => SearchCategory.News,
				"science" => SearchCategory.Science,
				"it" => SearchCategory.IT,
				"files" => SearchCategory.Files,
				"socialmedia" => SearchCategory.SocialMedia,
				"music" => SearchCategory.Music,
				"map" => SearchCategory.Map,
				"repos" => SearchCategory.Repos,
				"packages" => SearchCategory.Packages,
				_ => SearchCategory.General,
			};

			var safeSearchLevel = safeSearch switch
			{
				"none" => SafeSearchLevel.None,
				"moderate" => SafeSearchLevel.Moderate,
				"strict" => SafeSearchLevel.Strict,
				_ => SafeSearchLevel.None,
			};

			TimeRange? parsedTimeRange = timeRange?.ToLowerInvariant() switch
			{
				"day" => TimeRange.Day,
				"week" => TimeRange.Week,
				"month" => TimeRange.Month,
				"year" => TimeRange.Year,
				_ => null,
			};

			return new SearchQuery
			{
				Query = query,
				Page = page,
				Category = searchCategory,
				Language = language,
				TimeRange = parsedTimeRange,
				SafeSearch = safeSearchLevel,
				Engines = engines,
				MaxResults = maxResults,
				TimeoutSeconds = 5.0,
			};
		}
	}

	/// <summary>
	/// Extension methods for string truncation.
	/// </summary>
	internal static class StringExtensions
	{
		/// <summary>
		/// Truncates a string to the specified maximum length, appending "..." if truncated.
		/// </summary>
		public static string Truncate(this string value, int maxLength)
		{
			if (string.IsNullOrEmpty(value)) return value;
			return value.Length <= maxLength ? value : value[..maxLength] + "...";
		}
	}
}