using LLMDesktopAssistant.LLM.Domain;
using LLMDesktopAssistant.LLM.MVVM.Additional;
using LLMDesktopAssistant.Localization;
using LLMDesktopAssistant.Utils;
using LLMDesktopAssistant.Utils.Files;
using RCLargeLanguageModels.Messages.Attachments;

namespace LLMDesktopAssistant.LLM.Services.Attachments
{
	[ChatService(typeof(IAttachmentApplicationService))]
	public class AttachmentApplicationService(
		IChatSettingsService chatSettings
		) : IAttachmentApplicationService
	{
		private static readonly HttpClient HttpClient = new()
		{
			Timeout = TimeSpan.FromMinutes(5)
		};

		private static bool IsWebUrl(Uri uri) =>
			uri.Scheme.StartsWith("http", StringComparison.OrdinalIgnoreCase);

		private static async Task<string> EnsureLocalFileAsync(
			Uri uri,
			string destPath,
			CancellationToken cancellationToken = default)
		{
			if (IsWebUrl(uri))
			{
				using var response = await HttpClient.GetAsync(
					uri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
				response.EnsureSuccessStatusCode();

				Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);

				await using var fs = new FileStream(
					destPath, FileMode.Create, FileAccess.Write, FileShare.None,
					bufferSize: 8192, useAsync: true);
				await response.Content.CopyToAsync(fs, cancellationToken);

				return destPath;
			}

			var fileName = uri.LocalPath;

			if (!File.Exists(fileName))
				throw new FileNotFoundException("File not found", fileName);

			Directory.CreateDirectory(Path.GetDirectoryName(destPath)!);
			File.Copy(fileName, destPath, overwrite: true);

			return destPath;
		}

		private static string GetDestinationPath(Uri uri, string attachmentsDir)
		{
			var timestamp = DateTime.Now.ToString("yyyy-MM-dd-HHmmss");

			string fileName;

			if (IsWebUrl(uri))
			{
				var nameFromUrl = Path.GetFileName(uri.LocalPath);

				if (!string.IsNullOrWhiteSpace(nameFromUrl) && nameFromUrl.Contains('.'))
				{
					fileName = $"{timestamp}-{SanitizeFileName(nameFromUrl)}";
				}
				else
				{
					var host = SanitizeFileName(uri.Host);
					fileName = $"{timestamp}-{host}.html";
				}
			}
			else
			{
				fileName = $"{timestamp}-{Path.GetFileName(uri.LocalPath)}";
			}

			return Path.Combine(attachmentsDir, fileName);
		}

		private static string SanitizeFileName(string name)
		{
			var invalid = Path.GetInvalidFileNameChars();
			foreach (var c in invalid)
				name = name.Replace(c, '_');
			return name;
		}

		private static readonly string[] ImageExtensions =
		[
			".png", ".jpg", ".jpeg", ".bmp", ".gif", ".webp", ".tif", ".tiff", ".avif", ".qoi"
		];

		private static bool IsImageFile(string path)
		{
			var extension = Path.GetExtension(path);
			return !string.IsNullOrEmpty(extension) &&
				ImageExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase);
		}

		private static string GetTempDestinationPath(Uri uri)
		{
			var fileName = Path.GetFileName(uri.LocalPath);
			var safeName = string.IsNullOrWhiteSpace(fileName) ? "download" : SanitizeFileName(fileName);
			return Path.Combine(Directories.TempFiles, $"{Guid.NewGuid():N}-{safeName}");
		}

		public async Task<AttachmentMessagePart> ApplyAttachmentAsync(
			AttachmentApplicationParameters parameters,
			CancellationToken cancellationToken = default)
		{
			var sourceUri = parameters.SourceUri;

			var workingDir = chatSettings.Settings.Environment.GetEffectiveWorkingDirectories().GetWorkingDirectory();

			string destPath;
			if (parameters.CopyToWorkingDirectory)
			{
				var attachmentsDir = Path.Combine(workingDir, Directories.WorkingHome, "attachments");
				Directory.CreateDirectory(attachmentsDir);

				destPath = GetDestinationPath(sourceUri, attachmentsDir);
				await EnsureLocalFileAsync(sourceUri, destPath, cancellationToken);
			}
			else if (IsWebUrl(sourceUri))
			{
				// Web resources cannot be attached without downloading,
				// but they are stored outside the working directory.
				destPath = GetTempDestinationPath(sourceUri);
				await EnsureLocalFileAsync(sourceUri, destPath, cancellationToken);
			}
			else
			{
				// Use the source file as is, without copying it to the working directory.
				destPath = sourceUri.LocalPath;
				if (!File.Exists(destPath))
					throw new FileNotFoundException("File not found", destPath);
			}

			var localPath = parameters.CopyToWorkingDirectory ? Path.GetRelativePath(workingDir, destPath) : null;
			var metrics = FileUtils.GetFileMetrics(destPath);

			IAttachment? nativeAttachment = null;
			if (parameters.ApplyNative && IsImageFile(destPath))
			{
				try
				{
					nativeAttachment = new SerializableImageAttachment(destPath);
				}
				catch
				{
					// The file has an image extension but is not a valid image,
					// so it is attached without a native LLM attachment.
				}
			}

			var fileName = Path.GetFileName(sourceUri.LocalPath);
			var visuals = AttachmentChipVisuals.ForPath(fileName);

			return new AttachmentMessagePart
			{
				ChipIcon = visuals.Icon,
				ChipTitle = Locale.GetConstKey(fileName),
				ChipColor = visuals.Color,
				SourceUrl = sourceUri.AbsoluteUri,
				LocalPath = localPath,
				Size = metrics.Size,
				NativeAttachment = nativeAttachment
			};
		}
	}
}