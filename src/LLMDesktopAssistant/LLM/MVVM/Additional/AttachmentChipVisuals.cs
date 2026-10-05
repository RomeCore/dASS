using Avalonia.Media;
using Material.Icons;

namespace LLMDesktopAssistant.LLM.MVVM.Additional
{
	/// <summary>
	/// Maps a file (by its extension) to the chip icon and accent color used to render
	/// <see cref="AttachmentMessagePart"/> chips.
	/// </summary>
	public static class AttachmentChipVisuals
	{
		/// <summary>
		/// Resolves the chip icon and color for a file path (or file name).
		/// </summary>
		public static (VisualIconKind Icon, Color Color) ForPath(string? path)
		{
			var extension = Path.GetExtension(path ?? string.Empty).ToLowerInvariant();

			return extension switch
			{
				".png" or ".jpg" or ".jpeg" or ".gif" or ".bmp" or ".webp" or ".svg" or ".ico"
					=> (MaterialIconKind.FileImage, Colors.MediumPurple),

				".mp4" or ".mkv" or ".avi" or ".mov" or ".webm"
					=> (MaterialIconKind.FileVideo, Colors.IndianRed),

				".mp3" or ".wav" or ".ogg" or ".flac" or ".m4a"
					=> (MaterialIconKind.FileMusic, Colors.DarkOrange),

				".pdf"
					=> (MaterialIconKind.FilePdfBox, Colors.IndianRed),

				".doc" or ".docx" or ".rtf" or ".odt"
					=> (MaterialIconKind.FileWord, Colors.RoyalBlue),

				".xls" or ".xlsx" or ".csv" or ".ods"
					=> (MaterialIconKind.FileExcel, Colors.SeaGreen),

				".ppt" or ".pptx" or ".odp"
					=> (MaterialIconKind.FilePowerpoint, Colors.DarkOrange),

				".zip" or ".rar" or ".7z" or ".tar" or ".gz" or ".bz2" or ".xz"
					=> (MaterialIconKind.FolderZip, Colors.Goldenrod),

				".txt" or ".md" or ".log" or ".json" or ".xml" or ".yml" or ".yaml" or ".toml" or ".ini"
					=> (MaterialIconKind.FileDocumentOutline, Colors.Gray),

				".cs" or ".csx" or ".js" or ".ts" or ".py" or ".lua" or ".html" or ".css" or ".sh" or ".ps1" or ".c" or ".cpp" or ".h"
					=> (MaterialIconKind.FileCodeOutline, Colors.SteelBlue),

				_ => (MaterialIconKind.FileOutline, Colors.Gray)
			};
		}
	}
}
