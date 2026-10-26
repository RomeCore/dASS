using System.Text;
using AngleSharp.Html.Parser;
using AsyncLua;
using AsyncLua.Values;
using LLMDesktopAssistant.LLM.Services;
using LLMDesktopAssistant.LLM.Settings;
using LLMDesktopAssistant.Utils.Web;

namespace LLMDesktopAssistant.Scripting.Lua.API
{
	/// <summary>
	/// Lua API for HTTP/web operations: <c>web.request()</c>, <c>web.fetch()</c>, <c>web.status()</c>, <c>web.parse()</c>, <c>web.download()</c>.
	/// Registered in the global <c>web</c> namespace alongside <c>web.search()</c>.
	/// </summary>
	[LuaApi(chatScoped: true)]
	public class LuaApiWebRequest : LuaApiBase
	{
		public const int DefaultTimeout = 30;

		public override string? Namespace => "web";

		public override string? Manuals => """
			--- web - web request, fetch, parse and download API

			Provides HTTP requests, content fetching (HTML → Markdown),
			website status checking, HTML parsing with CSS selectors, and file downloads.

			FUNCTIONS:

			--- async web.request(method, url, [params])
			  Performs an HTTP request with the specified method and URL.
			  Returns structured response data.

			  Parameters:
			    - method: string - HTTP method: "GET", "POST", "PUT", "DELETE"
			    - url: string - The request URL
			    - params: table (optional) - Additional parameters:
			      - use_pure_client: boolean (default: false) - If true, use http client without additional headers
			      - headers: table (optional) - Custom headers as key-value pairs
			      - content: string or table (optional) - Request body, if table it's serialized to JSON
			      - contentType: string (default: "application/json") - Content-Type header
			      - timeout: number (optional) - Request timeout in seconds (default: $TIMEOUT$)

			  Returns: table with:
			    - status_code: number - HTTP status code (e.g. 200)
			    - status_description: string - Status description (e.g. "OK")
			    - headers: table - Response headers as key-value pairs
			    - content_length: number - Length of the response content
			    - content: string - Response body (sliced by start/count)
			
			--- async web.status(url, [timeout])
			  Checks if a website is accessible and returns detailed status info.
			
			  Parameters:
			    - url: string - URL to check
			    - timeout: number (optional, default: $TIMEOUT$) - Timeout in seconds
			
			  Returns: table with:
			    - url: string - the checked URL
			    - status_code: number - HTTP status code
			    - status_description: string - Status description
			    - is_accessible: boolean - Whether the site is reachable
			    - response_time_ms: number - Response time in milliseconds
			    - content_type: string or nil - Content-Type header
			    - content_length: number or nil - Content-Length header
			    - server: string or nil - Server header
			
			--- async web.fetch(url, [contentType], [fetchLevel])
			  Fetches a web page and converts it to the specified content type.
			  The fetch level escalates automatically when the page denies access.

			  Parameters:
			    - url: string - URL to fetch
			    - contentType: string (default: "markdown") - "html", "sanitized_html", or "markdown"
			    - fetchLevel: string (default: "http_client") - "http_client", "browser", or "stealth_browser"

			  Returns: table with:
			    - url: string - the fetched URL
			    - content_type: string - the actual content type returned
			    - content_length: number - total length of fetched content
			    - content: string - the converted content

			--- async web.parse(url, selector, [fetchLevel])
			  Fetches HTML content and parses specific elements using a CSS selector.
			  The fetch level escalates automatically when the page denies access.

			  Parameters:
			    - url: string - URL to fetch HTML from
			    - selector: string - CSS selector (e.g. "h1", ".content", "#main > p")
			    - fetchLevel: string (default: "http_client") - "http_client", "browser", or "stealth_browser"

			  Returns: table with:
			    - url: string - the fetched URL
			    - selector: string - the CSS selector used
			    - match_count: number - number of matched elements
			    - contents: array of strings - text content of each matched element
			    - html: string - combined HTML of matched elements (sliced)

			--- async web.download(url, savePath, [options])
			  Downloads a file from a URL and saves it to the local filesystem.

			  Parameters:
			    - url: string - URL of the file to download
			    - savePath: string - Path to save the file (relative to the working directory)
			    - options: table (optional):
			      - headers: table (optional) - Custom headers as key-value pairs

			  Returns: table with:
			    - url: string - source URL
			    - file_size: number - downloaded file size in bytes
			    - status_code: number - HTTP status code
			    - success: boolean - whether the download succeeded

			EXAMPLES:

			  -- Fetch a page as Markdown
			  local r = await web.fetch("https://example.com")
			  print(r.content)

			  -- Fetch as raw HTML
			  local r = await web.fetch("https://example.com", "html")
			  print(r.content)

			  -- Fetch with a headless browser
			  local r = await web.fetch("https://example.com", "markdown", "browser")
			  print(r.content)

			  -- Check website status
			  local s = await web.status("https://google.com")
			  print(s.status_code, s.response_time_ms)

			  -- Parse specific elements
			  local r = await web.parse("https://news.ycombinator.com", "a.storylink")
			  for _, title in ipairs(r.contents) do
			    print(title)
			  end

			  -- Download a file
			  local r = await web.download("https://example.com/image.png", "downloaded_image.png")
			  print("Saved", r.file_size, "bytes to", r.save_path)

			  -- Request with custom headers
			  local r = await web.request("POST", "https://api.example.com/data", {
			    headers = { ["Authorization"] = "Bearer token" },
			    content = { key = "value" },
			    contentType = "application/json"
			  })
			  print(r.status_code, r.content)
			""".Replace("$TIMEOUT$", DefaultTimeout.ToString());

		private readonly IWorkingDirectoryAccessService _fileAccess;
		private readonly IWebFetcher _webFetcher;
		private readonly HttpClient _httpClient, _pureHttpClient, _infiniteTimeoutClient;

		public LuaApiWebRequest(IWorkingDirectoryAccessService fileAccess, IWebFetcher webFetcher)
		{
			_fileAccess = fileAccess;
			_webFetcher = webFetcher;
			_httpClient = CreateClient(addHeaders: true, timeoutSeconds: DefaultTimeout);
			_pureHttpClient = CreateClient(addHeaders: false, timeoutSeconds: DefaultTimeout);
			_infiniteTimeoutClient = CreateClient(addHeaders: true, timeoutSeconds: null);
		}

		public override Action? Populate(LuaTable globals, LuaTable ns, LuaService luaService)
		{
			ns["request"] = new LuaCallbackFunction(RequestAsync);
			ns["status"] = new LuaCallbackFunction(StatusAsync);
			ns["fetch"] = new LuaCallbackFunction(FetchAsync);
			ns["parse"] = new LuaCallbackFunction(ParseAsync);
			ns["download"] = new LuaCallbackFunction(DownloadAsync);
			return null;
		}

		private async Task<LuaTuple> RequestAsync(LuaCallingContext ctx, LuaValue[] args)
		{
			if (args.Length < 2)
				throw new LuaRuntimeException("web.request() requires at least method and url arguments.");

			if (args[0] is not LuaString methodVal)
				throw new LuaRuntimeException("First argument must be a string (HTTP method).");
			if (args[1] is not LuaString urlVal)
				throw new LuaRuntimeException("Second argument must be a string (URL).");

			string? content = null;
			string contentType = "application/json";
			Dictionary<string, string>? headers = null;
			bool usePureClient = false;
			int? timeout = DefaultTimeout;

			if (args.Length > 2 && args[2] is LuaTable opts)
			{
				if (opts.Get("use_pure_client") is LuaBoolean upc)
					usePureClient = upc.Value;

				if (opts.Get("content") is LuaValue cv)
				{
					if (cv is LuaString cvStr)
						content = cvStr.Value;
					else if (cv is LuaTable)
					{
						var jsonNode = StructuredLuaConverter.LuaValueToJsonNode(cv);
						content = jsonNode?.ToJsonString() ?? "{}";
					}
				}

				if (opts.Get("contentType") is LuaString ct)
					contentType = ct.Value;

				if (opts.Get("headers") is LuaTable hv)
				{
					headers = new Dictionary<string, string>();
					foreach (var kv in hv.Entries)
					{
						if (kv.Key is LuaString keyStr && kv.Value is LuaString valStr)
							headers[keyStr.Value] = valStr.Value;
					}
				}

				if (opts.Get("timeout") is LuaNumber to)
				{
					if (to.Value <= 0)
						timeout = null;
					else
						timeout = (int)to.Value;
				}
			}

			try
			{
				var httpMethod = methodVal.Value.ToUpperInvariant() switch
				{
					"GET" => HttpMethod.Get,
					"POST" => HttpMethod.Post,
					"PUT" => HttpMethod.Put,
					"DELETE" => HttpMethod.Delete,
					_ => throw new LuaRuntimeException($"Invalid HTTP method: '{methodVal.Value}'. Supported: GET, POST, PUT, DELETE.")
				};

				using var request = new HttpRequestMessage(httpMethod, urlVal.Value);

				if (!string.IsNullOrEmpty(content))
					request.Content = new StringContent(content, Encoding.UTF8, contentType);

				if (headers != null)
				{
					foreach (var header in headers)
						request.Headers.TryAddWithoutValidation(header.Key, header.Value);
				}

				HttpClient client;

				if (timeout == DefaultTimeout)
					client = usePureClient ? _pureHttpClient : _httpClient;
				else
					client = CreateClient(addHeaders: !usePureClient, timeout);

				var response = await client.SendAsync(request);
				var responseContent = await response.Content.ReadAsStringAsync();

				var result = new LuaTable();

				result["status_code"] = new LuaNumber((int)response.StatusCode);
				result["status_description"] = new LuaString(response.StatusCode.ToString());

				var headersTable = new LuaTable();
				foreach (var h in response.Headers)
					headersTable[h.Key] = new LuaString(string.Join(", ", h.Value));
				foreach (var h in response.Content.Headers)
					headersTable[h.Key] = new LuaString(string.Join(", ", h.Value));
				result["headers"] = headersTable;

				result["content_length"] = new LuaNumber(responseContent.Length);
				result["content"] = new LuaString(responseContent);

				return new LuaTuple(result);
			}
			catch (LuaRuntimeException) { throw; }
			catch (Exception ex)
			{
				throw new LuaRuntimeException($"HTTP request failed: {ex.Message}");
			}
		}

		private async Task<LuaTuple> StatusAsync(LuaCallingContext ctx, LuaValue[] args)
		{
			if (args.Length < 1)
				throw new LuaRuntimeException("web.status(url, [timeout]): at least 1 argument expected.");

			if (args[0] is not LuaString urlVal)
				throw new LuaRuntimeException("First argument must be a string (URL).");

			int timeout = DefaultTimeout;
			if (args.Length > 1 && args[1] is LuaNumber tv)
				timeout = (int)tv.Value;

			try
			{
				using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(timeout));
				var startTime = DateTime.UtcNow;

				var response = await _httpClient.GetAsync(urlVal.Value, HttpCompletionOption.ResponseHeadersRead, cts.Token);
				var endTime = DateTime.UtcNow;

				var result = new LuaTable();
				result["url"] = new LuaString(urlVal.Value);
				result["status_code"] = new LuaNumber((int)response.StatusCode);
				result["status_description"] = new LuaString(response.StatusCode.ToString());
				result["is_accessible"] = LuaBoolean.FromBoolean(response.IsSuccessStatusCode);
				result["response_time_ms"] = new LuaNumber(Math.Round((endTime - startTime).TotalMilliseconds, 2));
				result["content_type"] = new LuaString(response.Content.Headers.ContentType?.ToString() ?? string.Empty);
				result["content_length"] = new LuaNumber(response.Content.Headers.ContentLength ?? -1);
				result["server"] = new LuaString(response.Headers.Server?.ToString() ?? "Unknown");
				return new LuaTuple(result);
			}
			catch (TaskCanceledException)
			{
				var result = new LuaTable();
				result["url"] = new LuaString(urlVal.Value);
				result["status_code"] = new LuaNumber(0);
				result["status_description"] = new LuaString("Timeout");
				result["is_accessible"] = LuaBoolean.False;
				result["response_time_ms"] = new LuaNumber(timeout * 1000);
				result["content_type"] = LuaNil.Instance;
				result["content_length"] = new LuaNumber(-1);
				result["server"] = LuaNil.Instance;
				return new LuaTuple(result);
			}
			catch (LuaRuntimeException) { throw; }
			catch (Exception ex)
			{
				throw new LuaRuntimeException($"Website status check failed: {ex.Message}");
			}
		}

		private async Task<LuaTuple> FetchAsync(LuaCallingContext ctx, LuaValue[] args)
		{
			if (args.Length < 1)
				throw new LuaRuntimeException("web.fetch(url, [contentType], [fetchLevel]): at least 1 argument expected.");

			if (args[0] is not LuaString urlVal)
				throw new LuaRuntimeException("First argument must be a string (URL).");

			string contentType = "markdown";
			if (args.Length > 1 && args[1] is LuaString ctVal)
				contentType = ctVal.Value;

			var fetchLevel = ParseFetchLevel(args, 2);

			try
			{
				var fetchResult = await _webFetcher.FetchAsync(urlVal.Value, fetchLevel);
				if (fetchResult.HttpStatus is >= 400)
					throw new HttpRequestException($"HTTP {fetchResult.HttpStatus} loading {urlVal.Value}.");

				string content = fetchResult.Html;

				switch (contentType)
				{
					case "sanitized_html":
						content = HtmlSanitizer.Sanitize(content);
						break;
					case "markdown":
						content = HtmlToMarkdownConverter.Convert(content);
						break;
				}

				var result = new LuaTable();
				result["url"] = new LuaString(urlVal.Value);
				result["content_type"] = new LuaString(contentType);
				result["content_length"] = new LuaNumber(content.Length);
				result["content"] = new LuaString(content);

				return new LuaTuple(result);
			}
			catch (LuaRuntimeException) { throw; }
			catch (Exception ex)
			{
				throw new LuaRuntimeException($"Web fetch failed: {ex.Message}");
			}
		}

		private async Task<LuaTuple> ParseAsync(LuaCallingContext ctx, LuaValue[] args)
		{
			if (args.Length < 2)
				throw new LuaRuntimeException("web.parse(url, selector, [fetchLevel]): at least 2 arguments expected.");

			if (args[0] is not LuaString urlVal)
				throw new LuaRuntimeException("First argument must be a string (URL).");
			if (args[1] is not LuaString selectorVal)
				throw new LuaRuntimeException("Second argument must be a string (CSS selector).");

			var fetchLevel = ParseFetchLevel(args, 2);

			try
			{
				var fetchResult = await _webFetcher.FetchAsync(urlVal.Value, fetchLevel);
				if (fetchResult.HttpStatus is >= 400)
					throw new HttpRequestException($"HTTP {fetchResult.HttpStatus} loading {urlVal.Value}.");
				var html = fetchResult.Html;
				var parser = new HtmlParser();
				var document = await parser.ParseDocumentAsync(html);
				var elements = document.QuerySelectorAll(selectorVal.Value);

				var result = new LuaTable();
				result["url"] = new LuaString(urlVal.Value);
				result["selector"] = new LuaString(selectorVal.Value);

				var matchCount = elements.Length;
				result["match_count"] = new LuaNumber(matchCount);

				var contentsTable = new LuaTable();
				int i = 1;
				foreach (var el in elements)
				{
					contentsTable[i++] = new LuaString(el.TextContent.Trim());
				}
				result["contents"] = contentsTable;

				var combinedHtml = string.Join("\n\n", elements.Select(e => e.OuterHtml));
				result["html"] = new LuaString(combinedHtml);

				return new LuaTuple(result);
			}
			catch (LuaRuntimeException) { throw; }
			catch (Exception ex)
			{
				throw new LuaRuntimeException($"HTML parse failed: {ex.Message}");
			}
		}

		private async Task<LuaTuple> DownloadAsync(LuaCallingContext ctx, LuaValue[] args)
		{
			if (args.Length < 2)
				throw new LuaRuntimeException("web.download(url, savePath, [options]): at least 2 arguments expected.");

			if (args[0] is not LuaString urlVal)
				throw new LuaRuntimeException("First argument must be a string (URL).");
			if (args[1] is not LuaString savePathVal)
				throw new LuaRuntimeException("Second argument must be a string (save path).");

			Dictionary<string, string>? headers = null;
			if (args.Length > 2 && args[2] is LuaTable opts)
			{
				if (opts.Get("headers") is LuaTable hv)
				{
					headers = new Dictionary<string, string>();
					foreach (var kv in hv.Entries)
					{
						if (kv.Key is LuaString keyStr && kv.Value is LuaString valStr)
							headers[keyStr.Value] = valStr.Value;
					}
				}
			}

			try
			{
				var fullSavePath = _fileAccess.AccessPath(savePathVal.Value, DirectoryAccessMode.Write);

				var dir = Path.GetDirectoryName(fullSavePath);
				if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
					Directory.CreateDirectory(dir);

				using var request = new HttpRequestMessage(HttpMethod.Get, urlVal.Value);
				if (headers != null)
				{
					foreach (var header in headers)
						request.Headers.TryAddWithoutValidation(header.Key, header.Value);
				}

				var response = await _infiniteTimeoutClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead);
				var result = new LuaTable();

				if (!response.IsSuccessStatusCode)
				{
					result["url"] = new LuaString(urlVal.Value);
					result["file_size"] = new LuaNumber(0);
					result["status_code"] = new LuaNumber((int)response.StatusCode);
					result["success"] = LuaBoolean.False;
					return new LuaTuple(result);
				}

				using var downloadStream = await response.Content.ReadAsStreamAsync();
				using var fileStream = File.Create(fullSavePath);
				await downloadStream.CopyToAsync(fileStream);
				var fileSize = fileStream.Length;

				result["url"] = new LuaString(urlVal.Value);
				result["file_size"] = new LuaNumber(fileSize);
				result["status_code"] = new LuaNumber((int)response.StatusCode);
				result["success"] = LuaBoolean.True;

				return new LuaTuple(result);
			}
			catch (LuaRuntimeException) { throw; }
			catch (Exception ex)
			{
				throw new LuaRuntimeException($"File download failed: {ex.Message}");
			}
		}

		private static WebFetchLevel ParseFetchLevel(LuaValue[] args, int index)
		{
			if (args.Length > index && args[index] is LuaString levelVal &&
				Enum.TryParse<WebFetchLevel>(levelVal.Value.Replace("_", ""), ignoreCase: true, out var level))
			{
				return level;
			}

			return WebFetchLevel.HttpClient;
		}

		private static HttpClient CreateClient(bool addHeaders, int? timeoutSeconds)
		{
			var client = new HttpClient();

			if (addHeaders)
			{
				client.DefaultRequestHeaders.Add("User-Agent",
					"Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36");
				client.DefaultRequestHeaders.Add("Accept",
					"text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,*/*;q=0.8");
				client.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.5");
				client.DefaultRequestHeaders.Add("Connection", "keep-alive");
				client.DefaultRequestHeaders.Add("Upgrade-Insecure-Requests", "1");
			}

			if (timeoutSeconds.HasValue)
				client.Timeout = TimeSpan.FromSeconds(timeoutSeconds.Value);
			else
				client.Timeout = Timeout.InfiniteTimeSpan;

			return client;
		}
	}
}
