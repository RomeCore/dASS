using AsyncLua;
using AsyncLua.Values;
using SearXSharp;
using SearXSharp.Models;

namespace LLMDesktopAssistant.Scripting.Lua.API
{
	/// <summary>
	/// Lua API for web search: <c>web.search()</c>.
	/// Registered in the global <c>web</c> namespace.
	/// Returns structured result tables instead of formatted text.
	/// </summary>
	[LuaApi(chatScoped: false)]
	public class LuaApiWebSearch : LuaApiBase
	{
		private readonly SearchEngineManager _searchManager;

		public override string? Namespace => "web";

		public override string? Manuals => """
			--- web - web search API

			Provides web search functionality returning structured results.
			All results are returned as tables (objects) with named fields.

			FUNCTIONS:

			--- async web.search(query, [options])
			  Searches the web using all available search engines.
			  Must be called with await.

			  Parameters:
			    - query: string (required) - The search query
			    - options: table (optional) - Optional parameters:
			      - page: number (default: 1) - Page number (1-10)
			      - language: string (default: "auto") - Language code, "auto", "en", "ru", "fr" etc.
			      - timeRange: string (default: "") - "day", "week", "month", "year" or ""
			      - safeSearch: string (default: "none") - "none", "moderate", "strict"
			      - category: string (default: "web") - Search category. Available values:
			        "all" - all categories (General)
			        "web" - web pages
			        "images" - image search
			        "videos" - video search
			        "news" - news articles
			        "science" - scientific publications
			        "it" - information technology (repos, code)
			        "files" - file hosting & downloads
			        "socialmedia" - social media content
			        "music" - music & audio
			        "map" - maps & geographic content
			        "repos" - code repositories (GitHub, GitLab)
			        "packages" - software packages (npm, PyPI, Docker)
			      - engines: table (optional) - Array of engine names (e.g. {"google", "bing"})
			      - maxResults: number (default: 10) - Max results per engine (1-50)

			  Returns: table with:
			    - results: array of result tables (see below)
			    - suggestions: array of strings or nil
			    - corrections: array of strings or nil
			    - total_results: number or nil
			    - engine_timings: array of strings
			    - unresponsive_engines: array of strings
			    - supports_paging: boolean

			  Each result table contains all fields from SearchResult:

			  Core fields:
			    - url (string)
			    - title (string)
			    - content (string)
			    - engine (string)
			    - engines (table of strings)
			    - template (string)
			    - score (number)
			    - type (string: "Default", "Image", "Video", etc.)
			    - category (string: "Web", "Images", "News", etc.)

			  Media fields:
			    - thumbnail (string or nil)
			    - img_src (string or nil)
			    - iframe_src (string or nil)
			    - audio_src (string or nil)

			  Time fields:
			    - published_date (string in ISO format or nil)
			    - duration (string "HH:MM:SS" or nil)

			  Metadata fields:
			    - author (string or nil)
			    - metadata (string or nil)
			    - source (string or nil)
			    - views (number or nil)
			    - position (number)
			    - seed, leech (number)
			    - magnet_link (string or nil)
			    - latitude, longitude (number)
			    - doi, journal, comments, pdf_url, pages, volume, number (string or nil)
			    - authors, tags (table or nil)

			EXAMPLES:

			  -- Simple search
			  local r = await web.search("weather in London")
			  for _, res in ipairs(r.results) do
			    print(res.title .. " - " .. res.url)
			  end

			  -- Search with options
			  local r = await web.search("Lua programming", {
			    engines = { "google", "bing" },
			    maxResults = 5,
			    category = "web"
			  })

			  -- Search images
			  local imgs = await web.search("cats", {
			    category = "images",
			    maxResults = 10
			  })
			  for _, img in ipairs(imgs.results) do
			    print(img.title .. " [" .. (img.resolution or "?") .. "]")
			  end
			""";

		public LuaApiWebSearch(SearchEngineManager searchManager)
		{
			_searchManager = searchManager;
		}

		public override Action? Populate(LuaTable globals, LuaTable ns, LuaService luaService)
		{
			ns["search"] = new LuaCallbackFunction(SearchAsync);
			return null;
		}

		private async Task<LuaTuple> SearchAsync(LuaCallingContext ctx, LuaValue[] args)
		{
			if (args.Length == 0)
				throw new LuaRuntimeException("web.search() requires at least a query string.");

			if (args[0] is not LuaString queryVal)
				throw new LuaRuntimeException("First argument must be a string (query).");

			// Defaults
			int page = 1;
			string language = "auto";
			string? timeRange = null;
			string safeSearch = "none";
			string category = "web";
			string[]? engines = null;
			int maxResults = 10;

			// Parse optional options table
			if (args.Length > 1 && args[1] is LuaTable opts)
			{
				if (opts.Get("page") is LuaNumber pageVal)
					page = (int)pageVal.Value;
				if (opts.Get("language") is LuaString langVal)
					language = langVal.Value;
				if (opts.Get("timeRange") is LuaString trVal)
					timeRange = trVal.Value;
				if (opts.Get("safeSearch") is LuaString ssVal)
					safeSearch = ssVal.Value;
				if (opts.Get("category") is LuaString catVal)
					category = catVal.Value;
				if (opts.Get("maxResults") is LuaNumber mrVal)
					maxResults = (int)mrVal.Value;
				if (opts.Get("engines") is LuaTable engVal)
				{
					var engList = new List<string>();
					foreach (var e in engVal.Values)
					{
						if (e is LuaString es)
							engList.Add(es.Value);
					}
					if (engList.Count > 0)
						engines = engList.ToArray();
				}
			}

			var searchQuery = BuildSearchQuery(queryVal.Value, page, language, timeRange, safeSearch, category, engines, maxResults);

			SearchResultList resultList;
			try
			{
				resultList = await _searchManager.SearchAllAsync(searchQuery, default);
			}
			catch (Exception ex)
			{
				throw new LuaRuntimeException($"Web search failed: {ex.Message}");
			}

			var rootTable = new LuaTable();

			var resultsArray = new LuaTable();
			int idx = 1;
			foreach (var result in resultList.Results)
			{
				resultsArray[idx] = ResultToTable(result);
				idx++;
			}
			rootTable["results"] = resultsArray;

			if (resultList.Suggestions is { Count: > 0 })
				rootTable["suggestions"] = StringArrayToTable(resultList.Suggestions) as LuaValue ?? LuaNil.Instance;
			if (resultList.Corrections is { Count: > 0 })
				rootTable["corrections"] = StringArrayToTable(resultList.Corrections) as LuaValue ?? LuaNil.Instance;
			if (resultList.TotalResults.HasValue)
				rootTable["total_results"] = new LuaNumber(resultList.TotalResults.Value);

			var timingsArray = new LuaTable();
			for (int i = 0; i < resultList.EngineTimings.Count; i++)
			{
				var t = resultList.EngineTimings[i];
				timingsArray[i + 1] = new LuaString($"{t.Engine} ({t.TotalTime}ms)");
			}
			rootTable["engine_timings"] = timingsArray;

			var unresponsiveArray = new LuaTable();
			for (int i = 0; i < resultList.UnresponsiveEngines.Count; i++)
			{
				var u = resultList.UnresponsiveEngines[i];
				unresponsiveArray[i + 1] = new LuaString($"{u.Engine}: {u.ErrorType}{(u.IsSuspended ? " (suspended)" : "")}");
			}
			rootTable["unresponsive_engines"] = unresponsiveArray;

			rootTable["supports_paging"] = LuaBoolean.FromBoolean(resultList.SupportsPaging);

			return new LuaTuple(rootTable);
		}

		private static LuaTable ResultToTable(SearchResult r)
		{
			var t = new LuaTable();

			SetField(t, "url", r.Url);
			SetField(t, "title", r.Title);
			SetField(t, "content", r.Content);
			SetField(t, "engine", r.Engine);
			SetField(t, "template", r.Template);
			t["score"] = new LuaNumber(r.Score);
			SetField(t, "type", r.Type.ToString());
			SetField(t, "category", r.Category.ToString());

			SetField(t, "thumbnail", r.Thumbnail);
			SetField(t, "img_src", r.ImgSrc);
			SetField(t, "iframe_src", r.IframeSrc);
			SetField(t, "audio_src", r.AudioSrc);

			if (r.PublishedDate.HasValue)
				t["published_date"] = new LuaString(r.PublishedDate.Value.ToString("O"));
			if (r.Duration.HasValue && r.Duration.Value > TimeSpan.Zero)
				t["duration"] = new LuaString(r.Duration.Value.ToString(@"hh\:mm\:ss"));

			SetField(t, "author", r.Author);
			SetField(t, "metadata", r.Metadata);
			SetField(t, "source", r.Source);
			if (r.Views.HasValue)
				t["views"] = new LuaNumber(r.Views.Value);
			SetField(t, "resolution", r.Resolution);

			var engArr = new LuaTable();
			int i = 1;
			foreach (var e in r.Engines)
			{
				engArr[i] = new LuaString(e);
				i++;
			}
			if (i == 1)
				engArr[i] = new LuaString(r.Engine);
			t["engines"] = engArr;
			t["position"] = new LuaNumber(r.Position);

			t["seed"] = new LuaNumber(r.Seed);
			t["leech"] = new LuaNumber(r.Leech);
			SetField(t, "magnet_link", r.MagnetLink);
			t["latitude"] = new LuaNumber(r.Latitude);
			t["longitude"] = new LuaNumber(r.Longitude);

			SetField(t, "doi", r.Doi);
			if (r.Authors is { Count: > 0 })
				t["authors"] = StringArrayToTable(r.Authors) as LuaValue ?? LuaNil.Instance;
			SetField(t, "journal", r.Journal);
			if (r.Tags is { Count: > 0 })
				t["tags"] = StringArrayToTable(r.Tags) as LuaValue ?? LuaNil.Instance;
			SetField(t, "comments", r.Comments);
			SetField(t, "pdf_url", r.PdfUrl);
			SetField(t, "pages", r.Pages);
			SetField(t, "volume", r.Volume);
			SetField(t, "number", r.Number);

			return t;
		}

		private static void SetField(LuaTable t, string key, string? value)
		{
			if (value != null)
				t[key] = new LuaString(value);
		}

		private static LuaTable? StringArrayToTable(IReadOnlyList<string>? list)
		{
			if (list == null || list.Count == 0)
				return null;
			var arr = new LuaTable();
			for (int i = 0; i < list.Count; i++)
				arr[i + 1] = new LuaString(list[i]);
			return arr;
		}

		private static SearchQuery BuildSearchQuery(
			string query, int page, string language, string? timeRange, string safeSearch,
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
}
