namespace LLMDesktopAssistant.Utils.Files
{
	/// <summary>
	/// Walks a resolved search and yields the entries the pattern accepts. Stateless, so static,
	/// and free of any access policy: the caller resolves and checks the root before enumerating.
	/// </summary>
	public static class GlobWalker
	{
		/// <summary>
		/// Yields accepted entries lazily, honouring the ignore set, the reparse-point rule and the
		/// limit. A directory is descended into only while the pattern can still match below it.
		/// </summary>
		/// <param name="search">The resolved search to walk.</param>
		/// <param name="options">What to report and what to skip.</param>
		/// <param name="cancellationToken">Checked once per entry.</param>
		/// <returns>The accepted entries, in file system order.</returns>
		public static IEnumerable<GlobEntry> Enumerate(GlobSearch search, GlobWalkOptions options,
			CancellationToken cancellationToken = default)
		{
			// When the prefix took everything, the root is the entry the caller named, not a place to walk.
			if (search.Pattern.IsEmpty)
			{
				if (Directory.Exists(search.Root))
				{
					if (options.Directories)
						yield return CreateEntry(new DirectoryInfo(search.Root), [], true);
				}
				else if (File.Exists(search.Root) && options.Files)
				{
					yield return CreateEntry(new FileInfo(search.Root), [], false);
				}

				yield break;
			}

			if (!Directory.Exists(search.Root))
				yield break;

			var root = new DirectoryInfo(search.Root);


			var reported = 0;

			foreach (var entry in Walk(search, root, [], options, cancellationToken))
			{
				if (options.Limit > 0 && reported >= options.Limit)
					yield break;

				reported++;
				yield return entry;
			}
		}

		private static IEnumerable<GlobEntry> Walk(GlobSearch search, DirectoryInfo directory,
			List<string> relative, GlobWalkOptions options, CancellationToken cancellationToken)
		{
			FileSystemInfo[] entries;

			try
			{
				entries = directory.GetFileSystemInfos();
			}
			catch (UnauthorizedAccessException)
			{
				yield break;
			}
			catch (IOException)
			{
				yield break;
			}

			foreach (var entry in entries)
			{
				cancellationToken.ThrowIfCancellationRequested();

				// A reparse point can point anywhere, including at an ancestor.
				if ((entry.Attributes & FileAttributes.ReparsePoint) != 0)
					continue;

				var name = entry.Name;

				if (options.Ignore.Matches(name) && !search.Pattern.IsExplicitName(relative, name))
					continue;

				relative.Add(name);

				if (entry is DirectoryInfo child)
				{
					if (search.Pattern.IsPrefixPossible(relative))
					{
						if (options.Directories)
							yield return CreateEntry(entry, relative, true);

						foreach (var nested in Walk(search, child, relative, options, cancellationToken))
							yield return nested;
					}
				}
				else if (options.Files && search.Pattern.Matches(relative))
				{
					yield return CreateEntry(entry, relative, false);
				}

				relative.RemoveAt(relative.Count - 1);
			}
		}

		private static GlobEntry CreateEntry(FileSystemInfo entry, List<string> relative,
			bool isDirectory)
		{
			return new GlobEntry
			{
				FullPath = entry.FullName,
				RelativeToRoot = string.Join(Path.DirectorySeparatorChar, relative),
				Name = entry.Name,
				IsDirectory = isDirectory,
			};
		}
	}
}
