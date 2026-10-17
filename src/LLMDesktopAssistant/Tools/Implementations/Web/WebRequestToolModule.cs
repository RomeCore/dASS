using System.ComponentModel;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using LLMDesktopAssistant.LLM.Services;
using LLMDesktopAssistant.LLM.Settings;
using LLMDesktopAssistant.Utils.Files;
using LLMDesktopAssistant.Addons;
using LLMDesktopAssistant.Addons.Loading;
using LLMDesktopAssistant.Addons.Management;
using Material.Icons;
using RCLargeLanguageModels.Json.Schema;
using LLMDesktopAssistant.Localization;
using LLMDesktopAssistant.Tools.Execution.Domain;
using LLMDesktopAssistant.Tools.Execution.Support;

namespace LLMDesktopAssistant.Tools.Implementations.Web
{
	[ToolModule(chatScoped: true)]
	public class WebRequestToolModule : ToolModule
	{
		private readonly HttpClient _httpClient, _httpInfiniteTimeoutClient;
		private readonly IWorkingDirectoryAccessService _fileAccess;
		private readonly IAddonPathImpactDetector _addonDetector;
		private readonly IAddonManagerInvalidator _addonInvalidator;

		public WebRequestToolModule(IWorkingDirectoryAccessService fileAccess,
			IAddonPathImpactDetector addonDetector, IAddonManagerInvalidator addonInvalidator)
		{
			_httpClient = CreateClient();
			_httpInfiniteTimeoutClient = CreateClient();
			_httpInfiniteTimeoutClient.Timeout = Timeout.InfiniteTimeSpan;

			_fileAccess = fileAccess;
			_addonDetector = addonDetector;
			_addonInvalidator = addonInvalidator;

			AddTool(new ToolInitializationInfo
			{
				Executor = CheckWebsiteStatus,
				StreamingAnalyzer = CheckWebsiteStatusStreaming,
				Name = "web-status",
				Description = "Check if a website is accessible and return status code.",
				NameKey = Locale.GetKey("tool.name.web-status"),
				DescriptionKey = Locale.GetKey("tool.description.web-status"),
				CategoryKey = Locale.GetKey("tool.category.web"),
				DefaultExpectedBehaviour = ToolBehaviour.InternetAccess
			});

			AddTool(new ToolInitializationInfo
			{
				Executor = WebRequest,
				StreamingAnalyzer = WebRequestStreaming,
				Name = "web-request",
				Description = "Perform a request to a specified URL and method.",
				NameKey = Locale.GetKey("tool.name.web-request"),
				DescriptionKey = Locale.GetKey("tool.description.web-request"),
				CategoryKey = Locale.GetKey("tool.category.web"),
				DefaultExpectedBehaviour = ToolBehaviour.InternetAccess
			});

			AddTool(new ToolInitializationInfo
			{
				Executor = DownloadFile,
				StreamingAnalyzer = DownloadFileStreaming,
				PreviewExecutor = DownloadFilePreview,
				Name = "web-download",
				Description = "Download a file from a specified URL into the working directory.",
				NameKey = Locale.GetKey("tool.name.web-download"),
				DescriptionKey = Locale.GetKey("tool.description.web-download"),
				CategoryKey = Locale.GetKey("tool.category.web"),
				DefaultExpectedBehaviour = ToolBehaviour.FileDirectoryCreate | ToolBehaviour.FileEdit | ToolBehaviour.InternetAccess |
					ToolBehaviour.AddonPackEdit | ToolBehaviour.PromptEdit | ToolBehaviour.ScriptEdit
			});
		}

		private StreamingToolAnalysisResult CheckWebsiteStatusStreaming(
			string? url,
			IToolExecutionContext ctx)
		{
			ctx.Call.SetStatus(MaterialIconKind.Web, $"`{url}`");
			return StreamingToolAnalysisResult.Continue;
		}

		private async Task<ToolExecutionResult> CheckWebsiteStatus(
			IToolExecutionContext ctx,
			[Description("URL to check")]
			string url,
			[Description("Timeout in seconds (default: 30)")]
			int timeoutSeconds = 30,
			CancellationToken cancellationToken = default)
		{
			ctx.Call.SetStatus(MaterialIconKind.Web, $"`{url}`");

			try
			{
				using var cts1 = new CancellationTokenSource(TimeSpan.FromSeconds(timeoutSeconds));
				using var cts2 = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, cts1.Token);
				var startTime = DateTime.UtcNow;

				var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cts2.Token);
				var endTime = DateTime.UtcNow;

				ctx.Call.ResultContent = $"""
					Url: {url}
					Status code: {(int)response.StatusCode}
					Status description: {response.StatusCode.ToString()}
					Is accessible: {response.IsSuccessStatusCode}
					Response time ms: {(endTime - startTime).TotalMilliseconds}
					Content type: {response.Content.Headers.ContentType?.ToString()}
					Content length: {response.Content.Headers.ContentLength}
					Server: {response.Headers.Server?.ToString() ?? "Unknown"}
					""";

				return ToolExecutionResult.Succeeded;
			}
			catch (TaskCanceledException)
			{
				ctx.Call.SetStatusIcon(MaterialIconKind.WebCancel);
				ctx.Call.ResultContent = $"Website check timeout after {timeoutSeconds} seconds for URL: {url}";
				return ToolExecutionResult.Succeeded;
			}
			catch (Exception ex)
			{
				ctx.Call.ResultContent = $"Error checking website status: {ex.Message}";
				return ToolExecutionResult.Failed;
			}
		}

		private StreamingToolAnalysisResult WebRequestStreaming(
			string? method, string? url,
			IToolExecutionContext ctx)
		{
			ctx.Call.SetStatus(MaterialIconKind.Web, url != null ? $"{method} `{url}`" : method);
			return StreamingToolAnalysisResult.Continue;
		}

		private async Task<ToolExecutionResult> WebRequest(
			IToolExecutionContext ctx,
			[Description("Method of the request"), Enum(["GET", "POST", "PUT", "DELETE"])]
			string method,
			[Description("URL to send request to")]
			string url,
			[Description("The starting index of character to return")]
			int start = 0,
			[Description("The maximum count of characters to return")]
			int count = 10000,
			[Description("Optional: Additional headers as JSON")]
			JsonObject? headersJson = null,
			[Description("Optional: Request content")]
			string content = "",
			[Description("Content type (default: application/json)")]
			string contentType = "application/json",
			CancellationToken cancellationToken = default)
		{
			ctx.Call.SetStatus(MaterialIconKind.Web, url != null ? $"{method} `{url}`" : method);

			try
			{
				var httpMethod = method switch
				{
					"GET" => HttpMethod.Get,
					"POST" => HttpMethod.Post,
					"PUT" => HttpMethod.Put,
					"DELETE" => HttpMethod.Delete,
					_ => throw new ArgumentException("Invalid HTTP method", nameof(method))
				};

				using var request = new HttpRequestMessage(httpMethod, url);

				if (!string.IsNullOrEmpty(content))
					request.Content = new StringContent(content, Encoding.UTF8, contentType);

				if (headersJson != null)
				{
					foreach (var header in headersJson)
					{
						request.Headers.TryAddWithoutValidation(header.Key, header.Value?.GetValue<string>());
					}
				}

				var response = await _httpClient.SendAsync(request, cancellationToken);
				var responseContent = await response.Content.ReadAsStringAsync();

				var actualCount = Math.Min(count, content.Length - start);
				var slice = responseContent.Substring(start, actualCount);

				ctx.Call.ResultContent = $"""
					Status code: {(int)response.StatusCode}
					Status description: {response.StatusCode.ToString()}
					Headers: {JsonSerializer.Serialize(response.Headers)}
					Content length: {responseContent.Length}
					Showing slice: {start}-{start + actualCount}
					Content slice:
					```
					{slice}
					```
					""";
				return ToolExecutionResult.Succeeded;
			}
			catch (Exception ex)
			{
				ctx.Call.ResultContent = $"Error performing {method} request: {ex.Message}";
				return ToolExecutionResult.Failed;
			}
		}

		private StreamingToolAnalysisResult DownloadFileStreaming(
			string? url, string? savePath,
			IToolExecutionContext ctx)
		{
			ctx.Call.SetStatus(MaterialIconKind.Download, savePath != null ? $"`{url}` → `{savePath}`" : $"`{url}`");
			return StreamingToolAnalysisResult.Continue;
		}

		private PreviewToolExecutionResult DownloadFilePreview(
			string url, string savePath,
			IToolExecutionContext ctx)
		{
			try
			{
				var fullPath = _fileAccess.AccessPath(savePath, DirectoryAccessMode.Write);
				var fileExisted = File.Exists(fullPath);
				var addonKind = _addonDetector.Detect(savePath, isFile: true,
					fileExisted ? FileOperation.Edit : FileOperation.Create,
					fileExisted ? null : FileUtils.GetExistingAncestorDirectory(fullPath));

				if (fileExisted)
				{
					ctx.Call.SetStatus(MaterialIconKind.Download, savePath != null ? $"`{url}` → `{savePath}`" : $"`{url}`");
					return new PreviewToolExecutionResult
					{
						ExpectedBehaviour = ToolBehaviour.FileEdit | ToolBehaviour.InternetAccess |
							AddonKindToolBehaviourConverter.Convert(addonKind)
					};
				}

				ctx.Call.SetStatus(MaterialIconKind.Download, savePath != null ? $"`{url}` → `{savePath}`" : $"`{url}`");
				return new PreviewToolExecutionResult
				{
					ExpectedBehaviour = ToolBehaviour.FileDirectoryCreate | ToolBehaviour.InternetAccess |
						AddonKindToolBehaviourConverter.Convert(addonKind)
				};
			}
			catch (Exception ex)
			{
				ctx.Call.SetResult($"Error downloading file: {ex.Message}");
				return new PreviewToolExecutionResult { InterruptingSuccess = false };
			}
		}

		private async Task<ToolExecutionResult> DownloadFile(
			IToolExecutionContext ctx,
			[Description("URL of the file to download")] string url,
			[Description("Local working directory path to save the file")] string savePath,
			[Description("Optional: Additional headers as JSON")] JsonObject? headersJson = null,
			CancellationToken cancellationToken = default)
		{
			ctx.Call.SetStatus(MaterialIconKind.Download, $"`{url}` → `{savePath}`");

			long downloadedBytes = 0;
			long? totalBytes = null;
			try
			{
				var fullSavePath = _fileAccess.AccessPath(savePath, DirectoryAccessMode.Write);
				var fileExisted = File.Exists(fullSavePath);
				var addonKind = _addonDetector.Detect(savePath, isFile: true,
					fileExisted ? FileOperation.Edit : FileOperation.Create,
					fileExisted ? null : FileUtils.GetExistingAncestorDirectory(fullSavePath));
				var dir = Path.GetDirectoryName(fullSavePath);
				if (!Directory.Exists(dir))
					Directory.CreateDirectory(dir!);
				using var fileStream = File.Create(fullSavePath);
				_addonInvalidator.Invalidate(addonKind);
				using var request = new HttpRequestMessage(HttpMethod.Get, url);

				if (headersJson != null)
				{
					foreach (var header in headersJson)
					{
						request.Headers.TryAddWithoutValidation(header.Key, header.Value?.GetValue<string>());
					}
				}

				var response = await _httpInfiniteTimeoutClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
				if (response.IsSuccessStatusCode)
				{
					using var downloadStream = await response.Content.ReadAsStreamAsync(cancellationToken);
					totalBytes = response.Content.Headers.ContentLength;
					if (!totalBytes.HasValue)
						try
						{
							totalBytes = downloadStream.Length;
						}
						catch { }

					int bytesRead;
					byte[] buffer = new byte[16384];

					while ((bytesRead = await downloadStream.ReadAsync(buffer, cancellationToken)) > 0)
					{
						fileStream.Write(buffer, 0, bytesRead);
						await fileStream.FlushAsync();
						downloadedBytes += bytesRead;

						if (totalBytes.HasValue)
						{
							double progress = (double)downloadedBytes / totalBytes.Value;
							ctx.Call.SetStatusTitle($"`{url}` → `{savePath}` " +
								$"({FileUtils.BytesToDisplaySize(downloadedBytes)} / " +
								$"{FileUtils.BytesToDisplaySize(totalBytes.Value)}, {progress:P2})");
							ctx.Call.SetProgress(progress);
						}
						else
						{
							ctx.Call.SetStatusTitle($"`{url}` → `{savePath}` " +
								$"({FileUtils.BytesToDisplaySize(downloadedBytes)})");
							ctx.Call.SetProgress(null);
						}
					}
				}

				ctx.Call.ResultContent = $"""
					Status code: {(int)response.StatusCode}
					Status description: {response.StatusCode.ToString()}
					Downloaded: {downloadedBytes} ({FileUtils.BytesToDisplaySize(downloadedBytes)})
					Total size: {totalBytes ?? -1} {(totalBytes.HasValue ? FileUtils.BytesToDisplaySize(totalBytes.Value) : "unknown")}.
					""";
				return ToolExecutionResult.Succeeded;
			}
			catch (Exception ex)
			{
				ctx.Call.SetStatusIcon(MaterialIconKind.DownloadOff);
				ctx.Call.ResultContent = $"Error downloading file: {ex.Message}; " +
					$"downloaded ({FileUtils.BytesToDisplaySize(downloadedBytes)}) " +
					$"of {totalBytes ?? -1} ({(totalBytes.HasValue ? FileUtils.BytesToDisplaySize(totalBytes.Value) : "unknown")}).";
				return ToolExecutionResult.Failed;
			}
		}

		protected override void Dispose(bool disposing)
		{
			_httpClient.Dispose();
			_httpInfiniteTimeoutClient.Dispose();
		}

		private static HttpClient CreateClient()
		{
			var httpClient = new HttpClient();
			httpClient.DefaultRequestHeaders.Add("User-Agent",
				"Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36");
			httpClient.DefaultRequestHeaders.Add("Accept",
				"text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,*/*;q=0.8");
			httpClient.DefaultRequestHeaders.Add("Accept-Language", "en-US,en;q=0.5");
			httpClient.DefaultRequestHeaders.Add("Connection", "keep-alive");
			httpClient.DefaultRequestHeaders.Add("Upgrade-Insecure-Requests", "1");
			return httpClient;
		}
	}
}