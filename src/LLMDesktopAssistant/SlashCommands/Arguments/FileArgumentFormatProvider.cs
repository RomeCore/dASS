using LLMDesktopAssistant.LLM.Services;
using LLMDesktopAssistant.Localization;

namespace LLMDesktopAssistant.SlashCommands.Arguments
{
	public sealed class FileArgumentFormatProvider(IServiceProvider services, bool onlyDirectories)
		: ISlashCommandArgumentFormatProvider
	{
		private static readonly char[] Separators = [Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar];

		/// <inheritdoc/>
		public bool CanComplete => true;

		/// <inheritdoc/>
		public bool TryValidate(string raw, out LocaleKeyBase? error)
		{
			error = null;
			return true;
		}

		/// <inheritdoc/>
		public object Convert(string raw) => raw;

		/// <inheritdoc/>
		public IEnumerable<SlashCommandCompletionItem> Complete(string prefix, SlashCommandCompletionContext ctx)
		{
			ArgumentNullException.ThrowIfNull(ctx);

			var text = prefix ?? string.Empty;
			var separatorIndex = text.LastIndexOfAny(Separators);
			var directoryPart = separatorIndex >= 0 ? text[..(separatorIndex + 1)] : string.Empty;
			var leaf = separatorIndex >= 0 ? text[(separatorIndex + 1)..] : text;
			var separator = separatorIndex >= 0 ? text[separatorIndex] : Path.DirectorySeparatorChar;

			var root = services.GetService<IWorkingDirectoryAccessService>()?.GetWorkingDirectory();
			if (Resolve(directoryPart, root) is not { } directory)
				return [];

			List<FileSystemInfo> entries;
			try
			{
				entries = [.. new DirectoryInfo(directory).EnumerateFileSystemInfos()];
			}
			catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
			{
				// An unreadable directory offers nothing; completion never breaks the input.
				return [];
			}

			return [.. entries
				.Where(entry => entry is DirectoryInfo || !onlyDirectories)
				.Where(entry => entry.Name.StartsWith(leaf, StringComparison.OrdinalIgnoreCase))
				.OrderByDescending(entry => entry is DirectoryInfo)
				.ThenBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
				.Select(entry => new SlashCommandCompletionItem
				{
					Value = directoryPart + entry.Name + (entry is DirectoryInfo ? separator : string.Empty),
					InsertsSpace = entry is not DirectoryInfo
				})];
		}

		/// <summary>
		/// The absolute directory to list: the typed one when it is rooted, otherwise the request's working
		/// directory with it appended. Returns <see langword="null"/> when there is nothing to list.
		/// </summary>
		private static string? Resolve(string directoryPart, string? workingDirectory)
		{
			if (Path.IsPathRooted(directoryPart))
				return Directory.Exists(directoryPart) ? directoryPart : null;

			if (workingDirectory is null)
				return null;

			var resolved = Path.Combine(workingDirectory, directoryPart);
			return Directory.Exists(resolved) ? resolved : null;
		}
	}
}
