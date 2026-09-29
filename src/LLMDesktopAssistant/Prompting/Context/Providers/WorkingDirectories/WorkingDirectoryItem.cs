namespace LLMDesktopAssistant.Prompting.Context.Providers.WorkingDirectories
{
	/// <summary>
	/// The serializable snapshot of a single working directory tracked by the working directories section.
	/// The identity of the item is the pair of <see cref="Name"/> and <see cref="Path"/>:
	/// renaming a directory is therefore reported as a removal plus an addition.
	/// </summary>
	public class WorkingDirectoryItem
	{
		/// <summary>
		/// The display name of the directory, if it has one. The default working directory
		/// carries the localized name from <see cref="Utils.Directories.DefaultWorkingDirectoryName"/>.
		/// </summary>
		public string? Name { get; set; }

		/// <summary>
		/// The path of the directory.
		/// </summary>
		public string Path { get; set; } = string.Empty;

		/// <summary>
		/// Whether this directory is the active one, i.e. the root for relative paths.
		/// </summary>
		public bool IsActive { get; set; }

		/// <summary>
		/// Whether this is the built-in default working directory of the assistant.
		/// </summary>
		public bool IsDefault { get; set; }

		/// <summary>
		/// Creates an unfrozen copy of this item.
		/// </summary>
		public WorkingDirectoryItem Clone() => new()
		{
			Name = Name,
			Path = Path,
			IsActive = IsActive,
			IsDefault = IsDefault
		};
	}
}
