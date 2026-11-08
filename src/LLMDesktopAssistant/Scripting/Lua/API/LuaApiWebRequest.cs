using System.Diagnostics;
using System.Text;
using AngleSharp.Html.Parser;
using AsyncLua;
using AsyncLua.Values;
using LLMDesktopAssistant.LLM.Services;
using LLMDesktopAssistant.LLM.Settings;
using LLMDesktopAssistant.StructuredValues.Converters;
using LLMDesktopAssistant.Utils;
using LLMDesktopAssistant.Utils.Web;
using LLMDesktopAssistant.Utils.Web.Streaming;

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
		private const int StreamIdleTimeoutSeconds = 60;

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
			      - content_type: string (default: "application/json") - Content-Type header
			      - timeout: number (optional) - Request timeout in seconds (default: $TIMEOUT$)
			      - stream: table (optional) - Decode the response as it arrives, see STREAMING

			  Returns: table with:
			    - status_code: number - HTTP status code (e.g. 200)
			    - status_description: string - Status description (e.g. "OK")
			    - headers: table - Response headers as key-value pairs
			    - content_length: number - Length of the response content
			    - content: string - Response body; ABSENT when the request was streamed
			    - content_type: string - Response body type

			  STREAMING:
			    Pass params.stream to have the response decoded on arrival instead of buffered.
			    The call returns when the stream ends, the handler stops it, or it fails.

			      stream = {
			        mode = "sse",                 -- "sse" | "jsonl" | "raw-lines" (required)
			        on_event = function(e) end,   -- required; return false to stop the stream
			        comments = false,             -- "sse" only: emit comment lines as events
			        json = false,                 -- "sse" only: parse each data payload as JSON
			        on_error = "report",          -- "ignore" | "report" | "abort"
			        idle_timeout = 60,            -- seconds without a line before failing; 0 disables
			      }

			    Every event is a table; its fields depend on the mode:
			      sse:            { index, line, event, id, data }
			      sse comment:    { index, line, comment }   -- event is ABSENT, not "COMMENT"
			      jsonl:          { index, line, data }      -- data is already decoded
			      raw_lines:      { index, line, data }      -- data is the raw line
			      decode failure: { index, line, error }     -- data is absent

			    `index` counts emitted events, not lines. `line` is the raw text the event was built from.
			    Return false from the handler to stop: the call then returns normally with
			    stream.stopped = "handler". Any other return value keeps the stream going.

			    A stream has no total timeout by default, but params.timeout still applies when given.
			    A non-2xx response is NOT streamed: its body is returned whole in `content` and the
			    handler is never called, so an error payload is never mistaken for an empty stream.

			  Returns (when streamed): the same table without `content`, plus:
			    - stream.events: number - events delivered to the handler
			    - stream.bytes: number - bytes read from the network
			    - stream.stopped: string - "eof" or "handler"
			    - stream.first_event_ms: number - time to the first event (absent if none arrived)
			
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
			
			--- async web.fetch(url, [content_type], [fetch_level])
			  Fetches a web page and converts it to the specified content type.
			  The fetch level escalates automatically when the page denies access.

			  Parameters:
			    - url: string - URL to fetch
			    - content_type: string (default: "markdown") - "html", "sanitized-html", or "markdown"
			    - fetch_level: string (default: "http_client") - "http-client", "browser", or "stealth-browser"

			  Returns: table with:
			    - url: string - the fetched URL
			    - content_type: string - the actual content type returned
			    - content_length: number - total length of fetched content
			    - content: string - the converted content

			--- async web.parse(url, selector, [fetch_level])
			  Fetches HTML content and parses specific elements using a CSS selector.
			  The fetch level escalates automatically when the page denies access.

			  Parameters:
			    - url: string - URL to fetch HTML from
			    - selector: string - CSS selector (e.g. "h1", ".content", "#main > p")
			    - fetch_level: string (default: "http-client") - "http-client", "browser", or "stealth-browser"

			  Returns: table with:
			    - url: string - the fetched URL
			    - selector: string - the CSS selector used
			    - match_count: number - number of matched elements
			    - contents: array of strings - text content of each matched element
			    - html: string - combined HTML of matched elements (sliced)

			--- async web.download(url, save_path, [options])
			  Downloads a file from a URL and saves it to the local filesystem.

			  Parameters:
			    - url: string - URL of the file to download
			    - save_path: string - Path to save the file (relative to the working directory)
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
			    content_type = "application/json"
			  })
			  print(r.status_code, r.content)

			  -- Stream Server-Sent Events from an LLM endpoint
			  local chunks = {}
			  local r = await web.request("POST", "https://api.example.com/v1/chat", {
			    headers = { ["Authorization"] = "Bearer token", ["Accept"] = "text/event-stream" },
			    content = { model = "m", stream = true },
			    timeout = 0,
			    stream = {
			      mode = "sse",
			      json = true,
			      on_event = function(e)
			        if e.error then return false end            -- e.g. the [DONE] sentinel
			        table.insert(chunks, e.data.choices[1].delta.content or "")
			      end
			    }
			  })
			  print(table.concat(chunks), r.stream.events, r.stream.first_event_ms)

			  -- JSONL progress from a long job; the last line stops the stream
			  await web.request("POST", url, {
			    content = payload,
			    timeout = 0,
			    stream = {
			      mode = "jsonl",
			      on_event = function(e)
			        dass.log.info(string.format("progress %d%%", e.data.progress))
			        return e.data.progress < 100
			      end
			    }
			  })
			""".Replace("$TIMEOUT$", DefaultTimeout.ToString());

		private readonly IWorkingDirectoryAccessService _fileAccess;
		private readonly IWebFetcher _webFetcher;
		private readonly HttpClient _httpClient, _infiniteTimeoutClient, _pureInfiniteTimeoutClient;

		public LuaApiWebRequest(IWorkingDirectoryAccessService fileAccess, IWebFetcher webFetcher)
		{
			_fileAccess = fileAccess;
			_webFetcher = webFetcher;
			_httpClient = CreateClient(addHeaders: true, timeoutSeconds: DefaultTimeout);
			_infiniteTimeoutClient = CreateClient(addHeaders: true, timeoutSeconds: null);
			_pureInfiniteTimeoutClient = CreateClient(addHeaders: false, timeoutSeconds: null);
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
			var timeoutSpecified = false;
			int? timeout = DefaultTimeout;
			StreamRequest? stream = null;

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
						var jsonNode = cv.ToConstNodeValue(tolerant: true).ToJsonNode();
						content = jsonNode?.ToJsonString() ?? "{}";
					}
				}

				if (opts.Get("content_type") is LuaString ct)
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
					timeoutSpecified = true;

					if (to.Value <= 0)
						timeout = null;
					else
						timeout = (int)to.Value;
				}

				if (opts.Get("stream") is LuaTable streamTable)
					stream = ParseStreamRequest(streamTable);
			}

			// A stream is supposed to outlive the buffered default: the idle deadline is what notices a dead one.
			if (stream is not null && !timeoutSpecified)
				timeout = null;

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

				if (stream is not null)
					return await RequestStreamAsync(ctx, request, usePureClient, timeout, stream);

				var client = usePureClient ? _pureInfiniteTimeoutClient : _infiniteTimeoutClient;
				using var cts = CreateRequestTimeout(ctx.CancellationToken, timeout);
				using var response = await client.SendAsync(request, cts.Token);
				var responseContent = await response.Content.ReadAsStringAsync(cts.Token);

				return new LuaTuple(BuildResponseTable(response, responseContent));
			}
			catch (LuaRuntimeException) { throw; }
			catch (OperationCanceledException) when (ctx.CancellationToken.IsCancellationRequested) { throw; }
			catch (TimeoutException ex)
			{
				throw new LuaRuntimeException($"web.request(): stream stalled, {ex.Message}");
			}
			catch (TaskCanceledException)
			{
				throw new LuaRuntimeException($"web.request(): timed out after {(timeout?.ToString() ?? "?")}s");
			}
			catch (Exception ex)
			{
				throw new LuaRuntimeException($"HTTP request failed: {ex.Message}");
			}
		}

		/// <summary>
		/// Sends a request whose response is decoded as a stream and delivered to a Lua handler event by event.
		/// </summary>
		/// <remarks>
		/// A non-success response is an error payload rather than an event stream, so it is buffered and read
		/// whole: running an SSE decoder over a JSON error body would turn the error into zero events.
		/// </remarks>
		private async Task<LuaTuple> RequestStreamAsync(LuaCallingContext ctx, HttpRequestMessage request,
			bool usePureClient, int? timeout, StreamRequest stream)
		{
			using var cts = CreateRequestTimeout(ctx.CancellationToken, timeout);
			var client = usePureClient ? _pureInfiniteTimeoutClient : _infiniteTimeoutClient;
			var stopwatch = Stopwatch.StartNew();

			using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token);

			if (!response.IsSuccessStatusCode)
			{
				var errorBody = await response.Content.ReadAsStringAsync(cts.Token);
				return new LuaTuple(BuildResponseTable(response, errorBody));
			}

			var counter = new CountingStream(await response.Content.ReadAsStreamAsync(cts.Token));
			using var reader = new StreamReader(counter, Encoding.UTF8);

			var events = 0;
			double? firstEventMs = null;
			var stopped = "eof";

			await foreach (var item in StreamEventReader.ReadAsync(reader, stream.Options, cts.Token))
			{
				events++;
				firstEventMs ??= stopwatch.Elapsed.TotalMilliseconds;

				var returned = await ctx.CallAsync(stream.Handler, ToEventTable(item));
				if (returned.Count > 0 && returned.First is LuaBoolean { Value: false })
				{
					stopped = "handler";
					break;
				}
			}

			var result = BuildResponseTable(response, null);
			var streamTable = new LuaTable();
			streamTable["events"] = new LuaNumber(events);
			streamTable["bytes"] = new LuaNumber(counter.BytesRead);
			streamTable["stopped"] = new LuaString(stopped);

			if (firstEventMs is { } first)
				streamTable["first_event_ms"] = new LuaNumber(Math.Round(first, 2));

			result["stream"] = streamTable;
			return new LuaTuple(result);
		}

		/// <summary>
		/// Reads the <c>stream</c> option table and validates it before a single byte is sent.
		/// </summary>
		private static StreamRequest ParseStreamRequest(LuaTable table)
		{
			if (table.Get("on_event") is not LuaFunction handler)
				throw new LuaRuntimeException("web.request(): stream.on_event must be a function.");

			if (table.Get("mode") is not LuaString modeValue)
				throw new LuaRuntimeException("web.request(): stream.mode is required: \"sse\", \"jsonl\" or \"raw-lines\".");

			var mode = modeValue.Value switch
			{
				"sse" => StreamMode.Sse,
				"jsonl" => StreamMode.JsonLines,
				"raw-lines" => StreamMode.RawLines,
				_ => throw new LuaRuntimeException(
					$"web.request(): unknown stream.mode '{modeValue.Value}'. Supported: \"sse\", \"jsonl\", \"raw-lines\".")
			};

			var policy = StreamParseErrorPolicy.Report;
			if (table.Get("on_error") is LuaString policyValue)
			{
				policy = policyValue.Value switch
				{
					"ignore" => StreamParseErrorPolicy.Ignore,
					"report" => StreamParseErrorPolicy.Report,
					"abort" => StreamParseErrorPolicy.Abort,
					_ => throw new LuaRuntimeException(
						$"web.request(): unknown stream.on_error '{policyValue.Value}'. Supported: \"ignore\", \"report\", \"abort\".")
				};
			}

			TimeSpan? idleTimeout = TimeSpan.FromSeconds(StreamIdleTimeoutSeconds);
			if (table.Get("idle_timeout") is LuaNumber idleValue)
				idleTimeout = idleValue.Value <= 0 ? null : TimeSpan.FromSeconds(idleValue.Value);

			return new StreamRequest(handler, new StreamReadOptions
			{
				Mode = mode,
				Comments = table.Get("comments") is LuaBoolean comments && comments.Value,
				ParseJson = table.Get("json") is LuaBoolean json && json.Value,
				OnError = policy,
				IdleTimeout = idleTimeout
			});
		}

		/// <summary>
		/// Converts a decoded stream item into the table a Lua handler receives.
		/// </summary>
		private static LuaTable ToEventTable(StreamEvent item)
		{
			var table = new LuaTable
			{
				["index"] = new LuaNumber(item.Index)
			};

			if (item.Line is not null)
				table["line"] = new LuaString(item.Line);
			if (item.Event is not null)
				table["event"] = new LuaString(item.Event);
			if (item.Id is not null)
				table["id"] = new LuaString(item.Id);
			if (item.Comment is not null)
				table["comment"] = new LuaString(item.Comment);
			if (item.Error is not null)
				table["error"] = new LuaString(item.Error);

			if (item.IsJsonDecoded)
				table["data"] = item.Json?.ToConstNodeValue().ToLuaValue() ?? LuaNil.Instance;
			else if (item.Data is not null)
				table["data"] = new LuaString(item.Data);

			return table;
		}

		/// <summary>
		/// Builds the response table shared by every request flavour, omitting <c>content</c> when the body
		/// was streamed instead of buffered.
		/// </summary>
		private static LuaTable BuildResponseTable(HttpResponseMessage response, string? content)
		{
			var result = new LuaTable
			{
				["status_code"] = new LuaNumber((int)response.StatusCode),
				["status_description"] = new LuaString(response.StatusCode.ToString())
			};

			var headersTable = new LuaTable();
			foreach (var h in response.Headers)
				headersTable[h.Key] = new LuaString(string.Join(", ", h.Value));
			foreach (var h in response.Content.Headers)
				headersTable[h.Key] = new LuaString(string.Join(", ", h.Value));
			result["headers"] = headersTable;

			result["content_length"] = content is null
				? new LuaNumber(response.Content.Headers.ContentLength ?? -1)
				: new LuaNumber(content.Length);

			if (content is not null)
				result["content"] = new LuaString(content);

			result["content_type"] = new LuaString(response.Content.Headers.ContentType?.ToString() ?? string.Empty);

			return result;
		}

		/// <summary>
		/// Creates the per-request deadline instead of creating an <see cref="HttpClient"/> per request.
		/// </summary>
		private static CancellationTokenSource CreateRequestTimeout(CancellationToken cancellationToken, int? timeoutSeconds)
		{
			var source = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
			if (timeoutSeconds is { } seconds)
				source.CancelAfter(TimeSpan.FromSeconds(seconds));

			return source;
		}

		/// <summary>
		/// A validated streaming request: the handler and the decoder settings.
		/// </summary>
		private sealed record StreamRequest(LuaFunction Handler, StreamReadOptions Options);

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
				using var cts = CancellationTokenSource.CreateLinkedTokenSource(ctx.CancellationToken);
				cts.CancelAfter(TimeSpan.FromSeconds(timeout));
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
				throw new LuaRuntimeException("web.fetch(url, [content_type], [fetch_level]): at least 1 argument expected.");

			if (args[0] is not LuaString urlVal)
				throw new LuaRuntimeException("First argument must be a string (URL).");

			string contentType = "markdown";
			if (args.Length > 1 && args[1] is LuaString ctVal)
				contentType = ctVal.Value;

			var fetchLevel = ParseFetchLevel(args, 2);

			try
			{
				var fetchResult = await _webFetcher.FetchAsync(urlVal.Value, fetchLevel, ctx.CancellationToken);
				if (fetchResult.HttpStatus is >= 400)
					throw new HttpRequestException($"HTTP {fetchResult.HttpStatus} loading {urlVal.Value}.");

				string content = fetchResult.Html;

				switch (contentType)
				{
					case "sanitized-html":
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
				throw new LuaRuntimeException("web.parse(url, selector, [fetch_level]): at least 2 arguments expected.");

			if (args[0] is not LuaString urlVal)
				throw new LuaRuntimeException("First argument must be a string (URL).");
			if (args[1] is not LuaString selectorVal)
				throw new LuaRuntimeException("Second argument must be a string (CSS selector).");

			var fetchLevel = ParseFetchLevel(args, 2);

			try
			{
				var fetchResult = await _webFetcher.FetchAsync(urlVal.Value, fetchLevel, ctx.CancellationToken);
				if (fetchResult.HttpStatus is >= 400)
					throw new HttpRequestException($"HTTP {fetchResult.HttpStatus} loading {urlVal.Value}.");
				var html = fetchResult.Html;
				var parser = new HtmlParser();
				var document = await parser.ParseDocumentAsync(html, ctx.CancellationToken);
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
				throw new LuaRuntimeException("web.download(url, save_path, [options]): at least 2 arguments expected.");

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

				var response = await _infiniteTimeoutClient.SendAsync(request,
					HttpCompletionOption.ResponseHeadersRead, ctx.CancellationToken);
				var result = new LuaTable();

				if (!response.IsSuccessStatusCode)
				{
					result["url"] = new LuaString(urlVal.Value);
					result["file_size"] = new LuaNumber(0);
					result["status_code"] = new LuaNumber((int)response.StatusCode);
					result["success"] = LuaBoolean.False;
					return new LuaTuple(result);
				}

				using var downloadStream = await response.Content.ReadAsStreamAsync(ctx.CancellationToken);
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
			if (args.Length > index && args[index] is LuaString levelVal)
				return KebabEnumNames<WebFetchLevel>.Deserialize(levelVal.Value, WebFetchLevel.HttpClient);
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
