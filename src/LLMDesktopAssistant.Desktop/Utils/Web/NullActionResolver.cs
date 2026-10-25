using WebReaper.Core.Actions.Abstract;
using WebReaper.Domain.PageActions;

namespace LLMDesktopAssistant.Desktop.Utils.Web;

/// <summary>
/// An <see cref="IActionResolver"/> that never resolves semantic actions -
/// the default for fetches that do not use page actions.
/// </summary>
internal sealed class NullActionResolver : IActionResolver
{
	public Task<PageAction?> ResolveAsync(string intent, string pageHtml, CancellationToken cancellationToken = default)
		=> Task.FromResult<PageAction?>(null);
}
