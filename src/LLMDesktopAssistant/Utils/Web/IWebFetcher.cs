namespace LLMDesktopAssistant.Utils.Web;

/// <summary>
/// Fetches the raw HTML content of web pages. Implementations return the
/// page body exactly as received - no parsing, sanitization, or Markdown
/// conversion is performed. Conversion and parsing are the caller's
/// responsibility (for example with AngleSharp).
/// </summary>
public interface IWebFetcher
{
	/// <summary>
	/// Gets the highest <see cref="WebFetchLevel"/> this fetcher supports.
	/// </summary>
	WebFetchLevel MaxLevel { get; }

	/// <summary>
	/// Fetches the page and returns a <see cref="FetchResult"/> with the body,
	/// HTTP status, and response headers. Non-success statuses are returned as
	/// data; only genuine transport failures throw.
	/// </summary>
	/// <param name="url">The URL to fetch.</param>
	/// <param name="minFetchLevel">
	/// The minimum <see cref="WebFetchLevel"/> to use. The fetcher may escalate
	/// above this level (up to <see cref="MaxLevel"/>) when the page denies
	/// access at lower levels.
	/// </param>
	/// <param name="cancellationToken">Cancellation token.</param>
	/// <returns>The <see cref="FetchResult"/> with the raw body and response metadata.</returns>
	Task<FetchResult> FetchAsync(string url, WebFetchLevel minFetchLevel = WebFetchLevel.HttpClient, CancellationToken cancellationToken = default);
}
