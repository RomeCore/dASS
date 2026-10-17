using System.ComponentModel;
using System.Text.RegularExpressions;
using LLMDesktopAssistant.LLM.Services;
using LLMDesktopAssistant.LLM.Services.Attachments;
using LLMDesktopAssistant.LLM.Settings;
using LLMDesktopAssistant.Utils.Files;
using Material.Icons;
using LLMDesktopAssistant.Localization;
using LLMDesktopAssistant.Tools.Execution.Domain;
using LLMDesktopAssistant.Tools.Execution.Support;
using LLMDesktopAssistant.Addons.Loading;
using LLMDesktopAssistant.Addons;
using LLMDesktopAssistant.Addons.Management;
using DomainPreviewResult = LLMDesktopAssistant.Tools.Execution.Domain.PreviewToolExecutionResult;

namespace LLMDesktopAssistant.Tools.Implementations.Filesystem
{
	[ToolModule]
	public class FilesystemToolModule : ToolModule
	{
		private readonly IWorkingDirectoryAccessService _fileAccess;
		private readonly IAddonPathImpactDetector _addonDetector;
		private readonly IAddonManagerInvalidator _addonInvalidator;
		private readonly IDocumentReadingService _documentReader;

		public FilesystemToolModule(IWorkingDirectoryAccessService fileAccess,
			IAddonPathImpactDetector addonDetector, IAddonManagerInvalidator addonInvalidator,
			IDocumentReadingService documentReader)
		{
			_fileAccess = fileAccess;
			_addonDetector = addonDetector;
			_addonInvalidator = addonInvalidator;
			_documentReader = documentReader;

			AddTool(new ToolInitializationInfo
			{
				Executor = GetFileInfo,
				PreviewExecutor = PreviewGetFileInfo,
				Name = "fs-get_file_info",
				Description = "Returns file information including type classification.",
				NameKey = Locale.GetKey("tool.name.fs-get_file_info"),
				DescriptionKey = Locale.GetKey("tool.description.fs-get_file_info"),
				CategoryKey = Locale.GetKey("tool.category.filesystem"),
				DefaultExpectedBehaviour = ToolBehaviour.FileRead | ToolBehaviour.AccessOutsideWorkdir
			});

			AddTool(new ToolInitializationInfo
			{
				Executor = ReadBinaryFile,
				PreviewExecutor = PreviewReadBinaryFile,
				Name = "fs-read_binary_file",
				Description = "Reads binary file content as hex dump from the working directory.",
				NameKey = Locale.GetKey("tool.name.fs-read_binary_file"),
				DescriptionKey = Locale.GetKey("tool.description.fs-read_binary_file"),
				CategoryKey = Locale.GetKey("tool.category.filesystem"),
				DefaultExpectedBehaviour = ToolBehaviour.FileRead | ToolBehaviour.AccessOutsideWorkdir
			});

			AddTool(new ToolInitializationInfo
			{
				Executor = ReadDocumentFile,
				PreviewExecutor = PreviewReadDocumentFile,
				Name = "fs-read_document_file",
				Description = "Reads complex documents (DOCX, PPTX, PDF) by pages from the working directory. Supported extensions: .pdf, .docx, .pptx. This is not suitable for general text or code files, such as .txt, .py, .md, .cs, .js, etc.",
				NameKey = Locale.GetKey("tool.name.fs-read_document_file"),
				DescriptionKey = Locale.GetKey("tool.description.fs-read_document_file"),
				CategoryKey = Locale.GetKey("tool.category.filesystem"),
				DefaultExpectedBehaviour = ToolBehaviour.FileRead | ToolBehaviour.AccessOutsideWorkdir
			});

			AddTool(new ToolInitializationInfo
			{
				Executor = WriteBinaryFile,
				PreviewExecutor = PreviewWriteBinaryFile,
				Name = "fs-write_binary_file",
				Description = "Writes binary content to a file inside working directory.",
				NameKey = Locale.GetKey("tool.name.fs-write_binary_file"),
				DescriptionKey = Locale.GetKey("tool.description.fs-write_binary_file"),
				CategoryKey = Locale.GetKey("tool.category.filesystem"),
				DefaultExpectedBehaviour = ToolBehaviour.FileDirectoryCreate | ToolBehaviour.FileEdit |
					ToolBehaviour.AccessOutsideWorkdir | ToolBehaviour.AddonPackEdit | ToolBehaviour.PromptEdit | ToolBehaviour.ScriptEdit
			});

			AddTool(new ToolInitializationInfo
			{
				Executor = CreateDirectory,
				PreviewExecutor = PreviewCreateDirectory,
				Name = "fs-create_directory",
				Description = "Creates a new directory inside working directory path.",
				NameKey = Locale.GetKey("tool.name.fs-create_directory"),
				DescriptionKey = Locale.GetKey("tool.description.fs-create_directory"),
				CategoryKey = Locale.GetKey("tool.category.filesystem"),
				DefaultExpectedBehaviour = ToolBehaviour.FileDirectoryCreate | ToolBehaviour.AccessOutsideWorkdir |
					ToolBehaviour.AddonPackEdit | ToolBehaviour.PromptEdit | ToolBehaviour.ScriptEdit
			});

			AddTool(new ToolInitializationInfo
			{
				Executor = DeleteFile,
				PreviewExecutor = PreviewDeleteFile,
				Name = "fs-delete_file",
				Description = "Deletes a file inside working directory.",
				NameKey = Locale.GetKey("tool.name.fs-delete_file"),
				DescriptionKey = Locale.GetKey("tool.description.fs-delete_file"),
				CategoryKey = Locale.GetKey("tool.category.filesystem"),
				DefaultExpectedBehaviour = ToolBehaviour.FileDelete | ToolBehaviour.AccessOutsideWorkdir |
					ToolBehaviour.AddonPackEdit | ToolBehaviour.PromptEdit | ToolBehaviour.ScriptEdit
			});

			AddTool(new ToolInitializationInfo
			{
				Executor = DeleteDirectory,
				PreviewExecutor = PreviewDeleteDirectory,
				Name = "fs-delete_directory",
				Description = "Deletes a directory (empty or with contents) from the working directory.",
				NameKey = Locale.GetKey("tool.name.fs-delete_directory"),
				DescriptionKey = Locale.GetKey("tool.description.fs-delete_directory"),
				CategoryKey = Locale.GetKey("tool.category.filesystem"),
				DefaultExpectedBehaviour = ToolBehaviour.DirectoryDelete | ToolBehaviour.FileDelete | ToolBehaviour.AccessOutsideWorkdir |
					ToolBehaviour.AddonPackEdit | ToolBehaviour.PromptEdit | ToolBehaviour.ScriptEdit
			});

			AddTool(new ToolInitializationInfo
			{
				Executor = CopyFile,
				PreviewExecutor = PreviewCopyFile,
				Name = "fs-copy_file",
				Description = "Copies a file within the working directory.",
				NameKey = Locale.GetKey("tool.name.fs-copy_file"),
				DescriptionKey = Locale.GetKey("tool.description.fs-copy_file"),
				CategoryKey = Locale.GetKey("tool.category.filesystem"),
				DefaultExpectedBehaviour = ToolBehaviour.FileDirectoryCreate | ToolBehaviour.AccessOutsideWorkdir |
					ToolBehaviour.AddonPackEdit | ToolBehaviour.PromptEdit | ToolBehaviour.ScriptEdit
			});

			AddTool(new ToolInitializationInfo
			{
				Executor = CopyDirectory,
				PreviewExecutor = PreviewCopyDirectory,
				Name = "fs-copy_directory",
				Description = "Copies a directory and all its contents to a new location within the working directory.",
				NameKey = Locale.GetKey("tool.name.fs-copy_directory"),
				DescriptionKey = Locale.GetKey("tool.description.fs-copy_directory"),
				CategoryKey = Locale.GetKey("tool.category.filesystem"),
				DefaultExpectedBehaviour = ToolBehaviour.FileDirectoryCreate | ToolBehaviour.AccessOutsideWorkdir |
					ToolBehaviour.AddonPackEdit | ToolBehaviour.PromptEdit | ToolBehaviour.ScriptEdit
			});

			AddTool(new ToolInitializationInfo
			{
				Executor = RenameFile,
				PreviewExecutor = PreviewRenameFile,
				Name = "fs-rename_file",
				Description = "Renames or moves a file within the working directory.",
				NameKey = Locale.GetKey("tool.name.fs-rename_file"),
				DescriptionKey = Locale.GetKey("tool.description.fs-rename_file"),
				CategoryKey = Locale.GetKey("tool.category.filesystem"),
				DefaultExpectedBehaviour = ToolBehaviour.FileEdit | ToolBehaviour.AccessOutsideWorkdir |
					ToolBehaviour.AddonPackEdit | ToolBehaviour.PromptEdit | ToolBehaviour.ScriptEdit
			});

			AddTool(new ToolInitializationInfo
			{
				Executor = MoveDirectory,
				PreviewExecutor = PreviewMoveDirectory,
				Name = "fs-move_directory",
				Description = "Moves a directory and all its contents to a new location within the working directory.",
				NameKey = Locale.GetKey("tool.name.fs-move_directory"),
				DescriptionKey = Locale.GetKey("tool.description.fs-move_directory"),
				CategoryKey = Locale.GetKey("tool.category.filesystem"),
				DefaultExpectedBehaviour = ToolBehaviour.DirectoryEdit | ToolBehaviour.AccessOutsideWorkdir |
					ToolBehaviour.AddonPackEdit | ToolBehaviour.PromptEdit | ToolBehaviour.ScriptEdit
			});
		}

		private DomainPreviewResult PreviewGetFileInfo(IToolExecutionContext context, string path,
			[SharedContext] out string fullPath)
		{
			fullPath = _fileAccess.CheckedAccessPath(path, DirectoryAccessMode.Read, out var isAccessed);

			if (!File.Exists(fullPath))
			{
				context.Call.SetStatus(MaterialIconKind.FileQuestion, $"**{path}**");
				context.Call.SetResult($"File not found: {path}");

				return new DomainPreviewResult
				{
					ExpectedBehaviour = !isAccessed ? ToolBehaviour.AccessOutsideWorkdir : ToolBehaviour.None,
					InterruptingSuccess = false
				};
			}

			var metrics = FileUtils.GetFileMetrics(fullPath);
			context.Call.SetStatus(metrics.Type switch
			{
				FileType.Binary => MaterialIconKind.File,
				FileType.Text => MaterialIconKind.FileText,
				FileType.Code => MaterialIconKind.FileCode,
				FileType.Image => MaterialIconKind.FileImage,
				FileType.Audio => MaterialIconKind.FileMusic,
				FileType.Video => MaterialIconKind.FileVideo,
				FileType.Executable => MaterialIconKind.Application,
				FileType.Archive => MaterialIconKind.Archive,
				FileType.Document => MaterialIconKind.FileDocument,
				_ => MaterialIconKind.FileQuestion
			}, $"**{path}** *({FileUtils.BytesToDisplaySize(metrics.Size)})*");

			return new DomainPreviewResult
			{
				ExpectedBehaviour = ToolBehaviour.FileRead | (!isAccessed ? ToolBehaviour.AccessOutsideWorkdir : ToolBehaviour.None)
			};
		}

		private ToolExecutionResult GetFileInfo(IToolExecutionContext context, string path,
			[SharedContext] string? fullPath = null)
		{
			try
			{
				fullPath ??= _fileAccess.AccessPath(path, DirectoryAccessMode.Read);
				var metrics = FileUtils.GetFileMetrics(fullPath);

				context.Call.SetStatus(metrics.Type switch
				{
					FileType.Binary => MaterialIconKind.File,
					FileType.Text => MaterialIconKind.FileText,
					FileType.Code => MaterialIconKind.FileCode,
					FileType.Image => MaterialIconKind.FileImage,
					FileType.Audio => MaterialIconKind.FileMusic,
					FileType.Video => MaterialIconKind.FileVideo,
					FileType.Executable => MaterialIconKind.Application,
					FileType.Archive => MaterialIconKind.Archive,
					FileType.Document => MaterialIconKind.FileDocument,
					_ => MaterialIconKind.FileQuestion
				}, $"**{path}** *({FileUtils.BytesToDisplaySize(metrics.Size)})*");

				var additional = metrics.LineCount != null
					? $"Lines: {metrics.LineCount}"
					: "Binary";

				var output = $"""
					Name: {metrics.Name}
					Type: {metrics.Type}
					Size: {metrics.Size} bytes ~ ({FileUtils.BytesToDisplaySize(metrics.Size)})
					{additional}
					Created: {metrics.Created:yyyy-MM-dd HH:mm}
					Modified: {metrics.Modified:yyyy-MM-dd HH:mm}
					Attributes: {metrics.Attributes}
					""";

				context.Call.SetResult(output);
				return ToolExecutionResult.Succeeded;
			}
			catch (Exception ex)
			{
				context.Call.SetResult($"Error getting file info: {ex.Message}");
				return ToolExecutionResult.Failed;
			}
		}

		private DomainPreviewResult PreviewReadBinaryFile(
			IToolExecutionContext context,
			string path, [SharedContext] out string fullPath,
			int startByte = 1,
			int endByte = 4096)
		{
			fullPath = _fileAccess.CheckedAccessPath(path, DirectoryAccessMode.Read, out var isAccessed);

			if (!File.Exists(fullPath))
			{
				context.Call.SetStatus(MaterialIconKind.FileQuestion, $"**{path}**");
				context.Call.SetResult($"File not found: {path}");

				return new DomainPreviewResult
				{
					ExpectedBehaviour = !isAccessed ? ToolBehaviour.AccessOutsideWorkdir : ToolBehaviour.None,
					InterruptingSuccess = false
				};
			}

			var expectedBehaviour = ToolBehaviour.FileRead | (!isAccessed ? ToolBehaviour.AccessOutsideWorkdir : ToolBehaviour.None);

			if (endByte < startByte)
			{
				context.Call.SetStatus(MaterialIconKind.FileCode, $"**{path}**");
				context.Call.SetResult("Invalid byte range.");

				return new DomainPreviewResult
				{
					ExpectedBehaviour = expectedBehaviour,
					InterruptingSuccess = false
				};
			}
			else
			{
				var fileInfo = new FileInfo(fullPath);
				var totalBytes = fileInfo.Length;
				context.Call.SetStatus(MaterialIconKind.FileCode, startByte == 1 && endByte >= totalBytes
					? $"**{path}**"
					: $"**{path}** *({startByte}~{Math.Min(endByte, totalBytes)} / {totalBytes})*");

				return new DomainPreviewResult
				{
					ExpectedBehaviour = expectedBehaviour
				};
			}
		}

		private ToolExecutionResult ReadBinaryFile(
			IToolExecutionContext context,
			string path,
			[Description("The 1-based index of the first byte to read.")]
			int startByte = 1,
			int endByte = 4096,
			[SharedContext] string? fullPath = null)
		{
			try
			{
				fullPath ??= _fileAccess.AccessPath(path, DirectoryAccessMode.Read);

				if (!File.Exists(fullPath))
				{
					context.Call.SetResult("File not found.");
					return ToolExecutionResult.Failed;
				}

				if (endByte < startByte)
				{
					context.Call.SetResult("Invalid byte range.");
					return ToolExecutionResult.Failed;
				}

				var fileInfo = new FileInfo(fullPath);
				var totalBytes = fileInfo.Length;

				var (lines, read) = FileUtils.ReadHexChunk(
					fullPath,
					startByte,
					endByte - startByte + 1);
				var endShown = startByte + read - 1;

				context.Call.SetStatus(MaterialIconKind.FileCode, endShown == totalBytes ?
					(startByte == 1 ? $"**{path}**" : $"**{path}** *({startByte}~{endShown})*") :
					$"**{path}** *({startByte}~{endShown} / {totalBytes})*");

				if (read == 0)
				{
					context.Call.SetResult("No content.");
					return ToolExecutionResult.Succeeded;
				}

				var output = $"""
					File: {path}
					Size: {totalBytes} bytes ~ ({FileUtils.BytesToDisplaySize(totalBytes)})
					Showing bytes: {startByte}-{startByte + read - 1}
					Hex dump:
					{string.Join(Environment.NewLine, lines)}
					""";

				context.Call.SetResult(output);
				return ToolExecutionResult.Succeeded;
			}
			catch (Exception ex)
			{
				context.Call.SetResult($"Error reading binary file: {ex.Message}");
				return ToolExecutionResult.Failed;
			}
		}

		private DomainPreviewResult PreviewReadDocumentFile(
			IToolExecutionContext context,
			string path, [SharedContext] out string fullPath,
			int startPage = 1,
			int endPage = 30)
		{
			fullPath = _fileAccess.CheckedAccessPath(path, DirectoryAccessMode.Read, out var isAccessed);

			if (!File.Exists(fullPath))
			{
				context.Call.SetStatus(MaterialIconKind.FileQuestion, $"**{path}**");
				context.Call.SetResult($"File not found: {path}");

				return new DomainPreviewResult
				{
					ExpectedBehaviour = !isAccessed ? ToolBehaviour.AccessOutsideWorkdir : ToolBehaviour.None,
					InterruptingSuccess = false
				};
			}

			var expectedBehaviour = ToolBehaviour.FileRead | (!isAccessed ? ToolBehaviour.AccessOutsideWorkdir : ToolBehaviour.None);

			if (endPage < startPage)
			{
				context.Call.SetStatus(MaterialIconKind.FileDocument, $"**{path}**");
				context.Call.SetResult("Invalid page range.");

				return new DomainPreviewResult
				{
					ExpectedBehaviour = expectedBehaviour,
					InterruptingSuccess = false
				};
			}

			context.Call.SetStatus(MaterialIconKind.FileDocument, $"**{path}** *({startPage}~{endPage})*");

			return new DomainPreviewResult
			{
				ExpectedBehaviour = expectedBehaviour
			};
		}

		private ToolExecutionResult ReadDocumentFile(
			IToolExecutionContext context,
			string path,
			[Description("The 1-based index of the first page to read.")]
			int startPage = 1,
			[Description("The 1-based index of the last page to read.")]
			int endPage = 30,
			[SharedContext] string? fullPath = null)
		{
			try
			{
				fullPath ??= _fileAccess.AccessPath(path, DirectoryAccessMode.Read);

				if (!File.Exists(fullPath))
				{
					context.Call.SetResult("File not found.");
					return ToolExecutionResult.Failed;
				}

				if (endPage < startPage)
				{
					context.Call.SetResult("Invalid page range.");
					return ToolExecutionResult.Failed;
				}

				var text = _documentReader.ExtractText(
					fullPath,
					startPage,
					endPage - startPage + 1);

				context.Call.SetStatus(MaterialIconKind.FileDocument, $"**{path}** *({startPage}~{endPage})*");

				if (string.IsNullOrWhiteSpace(text))
				{
					context.Call.SetResult("No text content extracted.");
					return ToolExecutionResult.Succeeded;
				}

				var output = $"""
					File: {path}
					Pages: {startPage}-{endPage}
					Content:
					{text}
					""";

				context.Call.SetResult(output);
				return ToolExecutionResult.Succeeded;
			}
			catch (Exception ex)
			{
				context.Call.SetResult($"Error reading document file: {ex.Message}");
				return ToolExecutionResult.Failed;
			}
		}

		private static byte[] ParseHex(string hex)
		{
			var hexClean = Regex.Replace(hex, @"[^0-9A-Fa-f]+", "");

			if (hexClean.Length % 2 != 0)
				throw new ArgumentException("Invalid hex string length. Hex string must have an even number of characters.");

			var bytes = new byte[hexClean.Length / 2];
			for (int i = 0; i < bytes.Length; i++)
			{
				var hexByte = hexClean.Substring(i * 2, 2);
				if (!byte.TryParse(hexByte, System.Globalization.NumberStyles.HexNumber, null, out byte parsedByte))
					throw new ArgumentException($"Invalid hex byte at position {i * 2}: '{hexByte}'");
				bytes[i] = parsedByte;
			}

			return bytes;
		}

		private class WriteBinaryFileContext
		{
			public required string FullPath { get; init; }
			public required byte[] Bytes { get; init; }
			public required AddonKind AddonKind { get; init; }
		}

		private DomainPreviewResult PreviewWriteBinaryFile(IToolExecutionContext context, string path, string hex,
			[SharedContext] out WriteBinaryFileContext? ctx, bool append = false)
		{
			var fullPath = _fileAccess.CheckedAccessPath(path, DirectoryAccessMode.Write, out var isAccessed);
			bool fileExisted = File.Exists(fullPath);
			var addonKind = _addonDetector.Detect(path, isFile: true,
				fileExisted ? FileOperation.Edit : FileOperation.Create,
				fileExisted ? null : FileUtils.GetExistingAncestorDirectory(fullPath));

			try
			{
				var bytes = ParseHex(hex);
				ctx = new WriteBinaryFileContext
				{
					FullPath = fullPath,
					Bytes = bytes,
					AddonKind = addonKind
				};
				context.Call.SetStatus(fileExisted ?
					(append ? MaterialIconKind.FileEdit : MaterialIconKind.FileCheck) :
					MaterialIconKind.FilePlus, $"**{path}**");

				return new DomainPreviewResult
				{
					ExpectedBehaviour = (fileExisted ? ToolBehaviour.FileEdit : ToolBehaviour.FileDirectoryCreate) |
						(!isAccessed ? ToolBehaviour.AccessOutsideWorkdir : ToolBehaviour.None) |
						AddonKindToolBehaviourConverter.Convert(addonKind)
				};
			}
			catch (Exception ex)
			{
				ctx = null;
				context.Call.SetStatus(MaterialIconKind.FileDocumentError, $"**{path}**");
				context.Call.SetResult($"Error writing binary file: {ex.Message}");

				return new DomainPreviewResult
				{
					ExpectedBehaviour = (!isAccessed ? ToolBehaviour.AccessOutsideWorkdir : ToolBehaviour.None) |
						AddonKindToolBehaviourConverter.Convert(addonKind),
					InterruptingSuccess = false
				};
			}
		}

		private ToolExecutionResult WriteBinaryFile(
			IToolExecutionContext context,
			string path,
			[Description("Hexadecimal string representing binary data in format '01 F8 2A'")]
			string hex,
			bool append = false,
			[SharedContext] WriteBinaryFileContext? ctx = null)
		{
			try
			{
				string fullPath;
				byte[] bytes;
				AddonKind addonKind;
				if (ctx != null)
				{
					fullPath = ctx.FullPath;
					bytes = ctx.Bytes;
					addonKind = ctx.AddonKind;
				}
				else
				{
					fullPath = _fileAccess.AccessPath(path, DirectoryAccessMode.Write);
					bytes = ParseHex(hex);
					var fileExistedBeforeWrite = File.Exists(fullPath);
					addonKind = _addonDetector.Detect(path, isFile: true,
						fileExistedBeforeWrite ? FileOperation.Edit : FileOperation.Create,
						fileExistedBeforeWrite ? null : FileUtils.GetExistingAncestorDirectory(fullPath));
				}

				var dir = Path.GetDirectoryName(fullPath);
				if (!Directory.Exists(dir))
					Directory.CreateDirectory(dir!);
				var fileExisted = File.Exists(fullPath);

				if (append)
				{
					using var fs = File.Open(fullPath, FileMode.Append);
					fs.Write(bytes, 0, bytes.Length);
				}
				else
				{
					File.WriteAllBytes(fullPath, bytes);
				}
				_addonInvalidator.Invalidate(addonKind);

				var fileInfo = new FileInfo(fullPath);
				var size = FileUtils.BytesToDisplaySize(fileInfo.Length);

				context.Call.SetStatus(fileExisted ?
					(append ? MaterialIconKind.FileEdit : MaterialIconKind.FileCheck) :
					MaterialIconKind.FilePlus, $"**{path}**");
				context.Call.SetResult($"""
					File: {path}
					Operation: {(append ? "Append" : "Write")}
					Bytes written: {bytes.Length}
					Total size: {fileInfo.Length} bytes ~ ({size})
					""");
				return ToolExecutionResult.Succeeded;
			}
			catch (Exception ex)
			{
				context.Call.SetStatus(MaterialIconKind.FileDocumentError, $"**{path}**");
				context.Call.SetResult($"Error writing binary file: {ex.Message}");
				return ToolExecutionResult.Failed;
			}
		}

		private class CreateDirectoryContext
		{
			public required string FullPath { get; init; }
			public required AddonKind AddonKind { get; init; }
		}

		private DomainPreviewResult PreviewCreateDirectory(IToolExecutionContext context, string path,
			[SharedContext] out CreateDirectoryContext? ctx)
		{
			var fullPath = _fileAccess.CheckedAccessPath(path, DirectoryAccessMode.Write, out var isAccessed);
			var addonKind = _addonDetector.Detect(path, isFile: false, FileOperation.Create,
				FileUtils.GetExistingAncestorDirectory(fullPath));
			ctx = new CreateDirectoryContext { FullPath = fullPath, AddonKind = addonKind };

			if (Directory.Exists(fullPath))
			{
				context.Call.SetStatus(MaterialIconKind.FolderQuestion, $"**{path}**");
				context.Call.SetResult("Directory already exists.");

				return new DomainPreviewResult
				{
					ExpectedBehaviour = !isAccessed ? ToolBehaviour.AccessOutsideWorkdir : ToolBehaviour.None,
					InterruptingSuccess = true
				};
			}

			context.Call.SetStatus(MaterialIconKind.FolderPlus, $"**{path}**");

			return new DomainPreviewResult
			{
				ExpectedBehaviour = ToolBehaviour.FileDirectoryCreate |
					(!isAccessed ? ToolBehaviour.AccessOutsideWorkdir : ToolBehaviour.None) |
					AddonKindToolBehaviourConverter.Convert(addonKind)
			};
		}

		private ToolExecutionResult CreateDirectory(IToolExecutionContext context, string path,
			[SharedContext] CreateDirectoryContext? ctx = null)
		{
			try
			{
				var fullPath = ctx?.FullPath ?? _fileAccess.AccessPath(path, DirectoryAccessMode.Write);
				var addonKind = ctx?.AddonKind ?? _addonDetector.Detect(path, isFile: false, FileOperation.Create,
					FileUtils.GetExistingAncestorDirectory(fullPath));

				if (Directory.Exists(fullPath))
				{
					context.Call.SetStatus(MaterialIconKind.FolderQuestion, $"**{path}**");
					context.Call.SetResult("Directory already exists.");
					return ToolExecutionResult.Succeeded;
				}

				Directory.CreateDirectory(fullPath);
				_addonInvalidator.Invalidate(addonKind);

				context.Call.SetStatus(MaterialIconKind.FolderPlus, $"**{path}**");
				context.Call.SetResult($"Directory '{path}' created successfully.");
				return ToolExecutionResult.Succeeded;
			}
			catch (Exception ex)
			{
				context.Call.SetStatus(MaterialIconKind.FolderPlus, $"**{path}**");
				context.Call.SetResult($"Error creating directory: {ex.Message}");
				return ToolExecutionResult.Failed;
			}
		}

		private class DeleteFileContext
		{
			public required string FullPath { get; init; }
			public required AddonKind AddonKind { get; init; }
		}

		private DomainPreviewResult PreviewDeleteFile(IToolExecutionContext context, string path,
			[SharedContext] out DeleteFileContext? ctx)
		{
			var fullPath = _fileAccess.CheckedAccessPath(path, DirectoryAccessMode.Write, out var isAccessed);
			var addonKind = _addonDetector.Detect(path, isFile: true, FileOperation.Delete);
			ctx = new DeleteFileContext { FullPath = fullPath, AddonKind = addonKind };

			if (!File.Exists(fullPath))
			{
				context.Call.SetStatus(MaterialIconKind.FileQuestion, $"**{path}**");
				context.Call.SetResult($"File not found: {path}");

				return new DomainPreviewResult
				{
					ExpectedBehaviour = !isAccessed ? ToolBehaviour.AccessOutsideWorkdir : ToolBehaviour.None,
					InterruptingSuccess = false
				};
			}

			context.Call.SetStatus(MaterialIconKind.Delete, $"**{path}**");

			return new DomainPreviewResult
			{
				ExpectedBehaviour = ToolBehaviour.FileDelete |
					(!isAccessed ? ToolBehaviour.AccessOutsideWorkdir : ToolBehaviour.None) |
					AddonKindToolBehaviourConverter.Convert(addonKind)
			};
		}

		private ToolExecutionResult DeleteFile(IToolExecutionContext context, string path,
			[SharedContext] DeleteFileContext? ctx = null)
		{
			try
			{
				var fullPath = ctx?.FullPath ?? _fileAccess.AccessPath(path, DirectoryAccessMode.Write);
				var addonKind = ctx?.AddonKind ?? _addonDetector.Detect(path, isFile: true, FileOperation.Delete);

				if (!File.Exists(fullPath))
				{
					context.Call.SetStatus(MaterialIconKind.FileQuestion, $"**{path}**");
					context.Call.SetResult($"File not found: {path}");
					return ToolExecutionResult.Succeeded;
				}

				File.Delete(fullPath);
				_addonInvalidator.Invalidate(addonKind);

				context.Call.SetStatus(MaterialIconKind.Delete, $"**{path}**");
				context.Call.SetResult($"File '{path}' deleted successfully.");
				return ToolExecutionResult.Succeeded;
			}
			catch (Exception ex)
			{
				context.Call.SetStatus(MaterialIconKind.FileDocumentError, $"**{path}**");
				context.Call.SetResult($"Error deleting file: {ex.Message}");
				return ToolExecutionResult.Failed;
			}
		}

		private class DeleteDirectoryContext
		{
			public required string FullPath { get; init; }
			public required AddonKind AddonKind { get; init; }
		}

		private DomainPreviewResult PreviewDeleteDirectory(IToolExecutionContext context, string path,
			[SharedContext] out DeleteDirectoryContext? ctx)
		{
			var fullPath = _fileAccess.CheckedAccessPath(path, DirectoryAccessMode.Write, out var isAccessed);
			var addonKind = _addonDetector.Detect(path, isFile: false, FileOperation.Delete);
			ctx = new DeleteDirectoryContext { FullPath = fullPath, AddonKind = addonKind };

			if (path == "." || path == "" || path == "/")
			{
				context.Call.SetStatus(MaterialIconKind.FolderRemove, $"**{path}**");
				context.Call.SetResult("Cannot delete the root working directory.");

				return new DomainPreviewResult
				{
					ExpectedBehaviour = (!isAccessed ? ToolBehaviour.AccessOutsideWorkdir : ToolBehaviour.None) |
						AddonKindToolBehaviourConverter.Convert(addonKind),
					InterruptingSuccess = false
				};
			}

			if (!Directory.Exists(fullPath))
			{
				context.Call.SetStatus(MaterialIconKind.FolderQuestion, $"**{path}**");
				context.Call.SetResult($"Directory not found: {path}");

				return new DomainPreviewResult
				{
					ExpectedBehaviour = (!isAccessed ? ToolBehaviour.AccessOutsideWorkdir : ToolBehaviour.None) |
						AddonKindToolBehaviourConverter.Convert(addonKind),
					InterruptingSuccess = false
				};
			}

			context.Call.SetStatus(MaterialIconKind.FolderRemove, $"**{path}**");

			return new DomainPreviewResult
			{
				ExpectedBehaviour = ToolBehaviour.DirectoryDelete |
					(Directory.GetFileSystemEntries(fullPath).Length > 0 ? ToolBehaviour.FileDelete : ToolBehaviour.None) |
					(!isAccessed ? ToolBehaviour.AccessOutsideWorkdir : ToolBehaviour.None) |
					AddonKindToolBehaviourConverter.Convert(addonKind)
			};
		}

		private ToolExecutionResult DeleteDirectory(IToolExecutionContext context, string path,
			[SharedContext] DeleteDirectoryContext? ctx = null)
		{
			try
			{
				// Prevent deleting the root working directory
				if (path == "." || path == "" || path == "/")
				{
					context.Call.SetStatus(MaterialIconKind.FolderRemove, $"**{path}**");
					context.Call.SetResult("Cannot delete the root working directory.");
					return ToolExecutionResult.Failed;
				}

				var fullPath = ctx?.FullPath ?? _fileAccess.AccessPath(path, DirectoryAccessMode.Write);
				var addonKind = ctx?.AddonKind ?? _addonDetector.Detect(path, isFile: false, FileOperation.Delete);

				if (!Directory.Exists(fullPath))
				{
					context.Call.SetStatus(MaterialIconKind.FolderQuestion, $"**{path}**");
					context.Call.SetResult($"Directory not found: {path}");
					return ToolExecutionResult.Succeeded;
				}

				Directory.Delete(fullPath, recursive: true);
				_addonInvalidator.Invalidate(addonKind);

				context.Call.SetStatus(MaterialIconKind.FolderRemove, $"**{path}**");
				context.Call.SetResult($"Directory '{path}' deleted successfully.");
				return ToolExecutionResult.Succeeded;
			}
			catch (Exception ex)
			{
				context.Call.SetStatus(MaterialIconKind.FolderRemove, $"**{path}**");
				context.Call.SetResult($"Error deleting directory: {ex.Message}");
				return ToolExecutionResult.Failed;
			}
		}

		private class RenameFileContext
		{
			public required string FullOldPath { get; init; }
			public required string FullNewPath { get; init; }
			public required AddonKind AddonKind { get; init; }
		}

		private DomainPreviewResult PreviewRenameFile(
			IToolExecutionContext context,
			string oldPath,
			string newPath,
			[SharedContext] out RenameFileContext? ctx,
			bool overwrite = false)
		{
			var fullOldPath = _fileAccess.CheckedAccessPath(oldPath, DirectoryAccessMode.Write, out var isOldAccessed);
			var fullNewPath = _fileAccess.CheckedAccessPath(newPath, DirectoryAccessMode.Write, out var isNewAccessed);
			var addonKind = _addonDetector.Detect(oldPath, isFile: true, FileOperation.Delete) |
				_addonDetector.Detect(newPath, isFile: true, FileOperation.Create,
					FileUtils.GetExistingAncestorDirectory(fullNewPath));
			ctx = new RenameFileContext { FullOldPath = fullOldPath, FullNewPath = fullNewPath, AddonKind = addonKind };

			if (!File.Exists(fullOldPath))
			{
				context.Call.SetStatus(MaterialIconKind.FileQuestion, $"**{oldPath}**");
				context.Call.SetResult("Source file not found.");

				return new DomainPreviewResult
				{
					ExpectedBehaviour = (!isOldAccessed || !isNewAccessed ? ToolBehaviour.AccessOutsideWorkdir : ToolBehaviour.None) |
						AddonKindToolBehaviourConverter.Convert(addonKind),
					InterruptingSuccess = false
				};
			}

			if (File.Exists(fullNewPath) && !overwrite)
			{
				context.Call.SetStatus(MaterialIconKind.PencilRemove, $"**{oldPath}** → **{newPath}**");
				context.Call.SetResult("Destination file already exists.");

				return new DomainPreviewResult
				{
					ExpectedBehaviour = (!isOldAccessed || !isNewAccessed ? ToolBehaviour.AccessOutsideWorkdir : ToolBehaviour.None) |
						AddonKindToolBehaviourConverter.Convert(addonKind),
					InterruptingSuccess = false
				};
			}

			context.Call.SetStatus(MaterialIconKind.Pencil, $"**{oldPath}** → **{newPath}**");

			return new DomainPreviewResult
			{
				ExpectedBehaviour = ToolBehaviour.FileEdit |
					(!isOldAccessed || !isNewAccessed ? ToolBehaviour.AccessOutsideWorkdir : ToolBehaviour.None) |
					AddonKindToolBehaviourConverter.Convert(addonKind)
			};
		}

		private ToolExecutionResult RenameFile(
			IToolExecutionContext context,
			string oldPath,
			string newPath,
			bool overwrite = false,
			[SharedContext] RenameFileContext? ctx = null)
		{
			try
			{
				var fullOldPath = ctx?.FullOldPath ?? _fileAccess.AccessPath(oldPath, DirectoryAccessMode.Write);
				var fullNewPath = ctx?.FullNewPath ?? _fileAccess.AccessPath(newPath, DirectoryAccessMode.Write);
				var addonKind = ctx?.AddonKind ??
					(_addonDetector.Detect(oldPath, isFile: true, FileOperation.Delete) |
					_addonDetector.Detect(newPath, isFile: true, FileOperation.Create,
						FileUtils.GetExistingAncestorDirectory(fullNewPath)));

				if (!File.Exists(fullOldPath))
				{
					context.Call.SetStatus(MaterialIconKind.FileQuestion, $"**{oldPath}** → **{newPath}**");
					context.Call.SetResult($"Source file not found: {oldPath}");
					return ToolExecutionResult.Failed;
				}

				if (File.Exists(fullNewPath) && !overwrite)
				{
					context.Call.SetStatus(MaterialIconKind.PencilRemove, $"**{oldPath}** → **{newPath}**");
					context.Call.SetResult($"Destination file already exists: {newPath}");
					return ToolExecutionResult.Failed;
				}

				if (Path.GetDirectoryName(fullNewPath) is string dir)
					Directory.CreateDirectory(dir);

				File.Move(fullOldPath, fullNewPath, overwrite);
				_addonInvalidator.Invalidate(addonKind);

				context.Call.SetStatus(MaterialIconKind.Pencil, $"**{oldPath}** → **{newPath}**");
				context.Call.SetResult($"File renamed from '{oldPath}' to '{newPath}'.");
				return ToolExecutionResult.Succeeded;
			}
			catch (Exception ex)
			{
				context.Call.SetStatus(MaterialIconKind.PencilRemove, $"**{oldPath}** → **{newPath}**");
				context.Call.SetResult($"Error renaming file: {ex.Message}");
				return ToolExecutionResult.Failed;
			}
		}

		private class MoveDirectoryContext
		{
			public required string FullOldPath { get; init; }
			public required string FullNewPath { get; init; }
			public required AddonKind AddonKind { get; init; }
		}

		private DomainPreviewResult PreviewMoveDirectory(
			IToolExecutionContext context,
			string oldPath,
			string newPath,
			[SharedContext] out MoveDirectoryContext? ctx,
			bool overwrite = false)
		{
			var fullOldPath = _fileAccess.CheckedAccessPath(oldPath, DirectoryAccessMode.Write, out var isOldAccessed);
			var fullNewPath = _fileAccess.CheckedAccessPath(newPath, DirectoryAccessMode.Write, out var isNewAccessed);
			var addonKind = _addonDetector.Detect(oldPath, isFile: false, FileOperation.Delete) |
				_addonDetector.Detect(newPath, isFile: false, FileOperation.Create,
					FileUtils.GetExistingAncestorDirectory(fullNewPath));
			ctx = new MoveDirectoryContext { FullOldPath = fullOldPath, FullNewPath = fullNewPath, AddonKind = addonKind };

			if (!Directory.Exists(fullOldPath))
			{
				context.Call.SetStatus(MaterialIconKind.FolderQuestion, $"**{oldPath}**");
				context.Call.SetResult("Source directory not found.");

				return new DomainPreviewResult
				{
					ExpectedBehaviour = (!isOldAccessed || !isNewAccessed ? ToolBehaviour.AccessOutsideWorkdir : ToolBehaviour.None) |
						AddonKindToolBehaviourConverter.Convert(addonKind),
					InterruptingSuccess = false
				};
			}

			if (Directory.Exists(fullNewPath) && !overwrite)
			{
				context.Call.SetStatus(MaterialIconKind.PencilRemove, $"**{oldPath}** → **{newPath}**");
				context.Call.SetResult("Destination directory already exists.");

				return new DomainPreviewResult
				{
					ExpectedBehaviour = (!isOldAccessed || !isNewAccessed ? ToolBehaviour.AccessOutsideWorkdir : ToolBehaviour.None) |
						AddonKindToolBehaviourConverter.Convert(addonKind),
					InterruptingSuccess = false
				};
			}

			context.Call.SetStatus(MaterialIconKind.Folder, $"**{oldPath}** → **{newPath}**");

			return new DomainPreviewResult
			{
				ExpectedBehaviour = ToolBehaviour.DirectoryEdit |
					(!isOldAccessed || !isNewAccessed ? ToolBehaviour.AccessOutsideWorkdir : ToolBehaviour.None) |
					AddonKindToolBehaviourConverter.Convert(addonKind)
			};
		}

		private ToolExecutionResult MoveDirectory(
			IToolExecutionContext context,
			string oldPath,
			string newPath,
			bool overwrite = false,
			[SharedContext] MoveDirectoryContext? ctx = null)
		{
			try
			{
				var fullOldPath = ctx?.FullOldPath ?? _fileAccess.AccessPath(oldPath, DirectoryAccessMode.Write);
				var fullNewPath = ctx?.FullNewPath ?? _fileAccess.AccessPath(newPath, DirectoryAccessMode.Write);
				var addonKind = ctx?.AddonKind ??
					(_addonDetector.Detect(oldPath, isFile: false, FileOperation.Delete) |
					_addonDetector.Detect(newPath, isFile: false, FileOperation.Create,
						FileUtils.GetExistingAncestorDirectory(fullNewPath)));

				if (!Directory.Exists(fullOldPath))
				{
					context.Call.SetStatus(MaterialIconKind.FolderQuestion, $"**{oldPath}** → **{newPath}**");
					context.Call.SetResult($"Source directory not found: {oldPath}");
					return ToolExecutionResult.Failed;
				}

				if (Directory.Exists(fullNewPath) && !overwrite)
				{
					context.Call.SetStatus(MaterialIconKind.PencilRemove, $"**{oldPath}** → **{newPath}**");
					context.Call.SetResult($"Destination directory already exists: {newPath}");
					return ToolExecutionResult.Failed;
				}

				Directory.CreateDirectory(Path.GetDirectoryName(fullNewPath)!);
				Directory.Move(fullOldPath, fullNewPath);
				_addonInvalidator.Invalidate(addonKind);

				context.Call.SetStatus(MaterialIconKind.Folder, $"**{oldPath}** → **{newPath}**");
				context.Call.SetResult($"Directory moved from '{oldPath}' to '{newPath}'.");
				return ToolExecutionResult.Succeeded;
			}
			catch (Exception ex)
			{
				context.Call.SetStatus(MaterialIconKind.PencilRemove, $"**{oldPath}** → **{newPath}**");
				context.Call.SetResult($"Error moving directory: {ex.Message}");
				return ToolExecutionResult.Failed;
			}
		}

		private class CopyFileContext
		{
			public required string FullOldPath { get; init; }
			public required string FullNewPath { get; init; }
			public required AddonKind AddonKind { get; init; }
		}

		private DomainPreviewResult PreviewCopyFile(
			IToolExecutionContext context,
			string oldPath,
			string newPath,
			[SharedContext] out CopyFileContext? ctx,
			bool overwrite = false)
		{
			var fullOldPath = _fileAccess.CheckedAccessPath(oldPath, DirectoryAccessMode.Write, out var isOldAccessed);
			var fullNewPath = _fileAccess.CheckedAccessPath(newPath, DirectoryAccessMode.Write, out var isNewAccessed);
			var destExisted = File.Exists(fullNewPath);
			var addonKind = _addonDetector.Detect(newPath, isFile: true,
				destExisted ? FileOperation.Edit : FileOperation.Create,
				destExisted ? null : FileUtils.GetExistingAncestorDirectory(fullNewPath));
			ctx = new CopyFileContext { FullOldPath = fullOldPath, FullNewPath = fullNewPath, AddonKind = addonKind };

			if (!File.Exists(fullOldPath))
			{
				context.Call.SetStatus(MaterialIconKind.FileQuestion, $"**{oldPath}**");
				context.Call.SetResult("Source file not found.");

				return new DomainPreviewResult
				{
					ExpectedBehaviour = (!isOldAccessed || !isNewAccessed ? ToolBehaviour.AccessOutsideWorkdir : ToolBehaviour.None) |
						AddonKindToolBehaviourConverter.Convert(addonKind),
					InterruptingSuccess = false
				};
			}

			if (destExisted && !overwrite)
			{
				context.Call.SetStatus(MaterialIconKind.ContentCopy, $"**{oldPath}** → **{newPath}**");
				context.Call.SetResult("Destination file already exists.");

				return new DomainPreviewResult
				{
					ExpectedBehaviour = (!isOldAccessed || !isNewAccessed ? ToolBehaviour.AccessOutsideWorkdir : ToolBehaviour.None) |
						AddonKindToolBehaviourConverter.Convert(addonKind),
					InterruptingSuccess = false
				};
			}

			context.Call.SetStatus(MaterialIconKind.ContentCopy, $"**{oldPath}** → **{newPath}**");

			return new DomainPreviewResult
			{
				ExpectedBehaviour = ToolBehaviour.FileDirectoryCreate |
					(!isOldAccessed || !isNewAccessed ? ToolBehaviour.AccessOutsideWorkdir : ToolBehaviour.None) |
					AddonKindToolBehaviourConverter.Convert(addonKind)
			};
		}

		private ToolExecutionResult CopyFile(
			IToolExecutionContext context,
			string oldPath,
			string newPath,
			bool overwrite = false,
			[SharedContext] CopyFileContext? ctx = null)
		{
			try
			{
				var fullOldPath = ctx?.FullOldPath ?? _fileAccess.AccessPath(oldPath, DirectoryAccessMode.Write);
				var fullNewPath = ctx?.FullNewPath ?? _fileAccess.AccessPath(newPath, DirectoryAccessMode.Write);
				var fileExisted = File.Exists(fullNewPath);
				var addonKind = ctx?.AddonKind ?? _addonDetector.Detect(newPath, isFile: true,
					fileExisted ? FileOperation.Edit : FileOperation.Create,
					fileExisted ? null : FileUtils.GetExistingAncestorDirectory(fullNewPath));

				if (!File.Exists(fullOldPath))
				{
					context.Call.SetStatus(MaterialIconKind.FileQuestion, $"**{oldPath}** → **{newPath}**+");
					context.Call.SetResult($"Source file not found: {oldPath}");
					return ToolExecutionResult.Failed;
				}

				if (fileExisted && !overwrite)
				{
					context.Call.SetStatus(MaterialIconKind.FileQuestion, $"**{oldPath}** → **{newPath}**+");
					context.Call.SetResult($"Destination file already exists: {newPath}");
					return ToolExecutionResult.Failed;
				}

				if (Path.GetDirectoryName(fullNewPath) is string dir)
					Directory.CreateDirectory(dir);

				File.Copy(fullOldPath, fullNewPath, overwrite);
				_addonInvalidator.Invalidate(addonKind);

				context.Call.SetStatus(MaterialIconKind.ContentCopy, $"**{oldPath}** → **{newPath}**+");
				context.Call.SetResult($"File copied from '{oldPath}' to '{newPath}'.");
				return ToolExecutionResult.Succeeded;
			}
			catch (Exception ex)
			{
				context.Call.SetStatus(MaterialIconKind.FileDocumentError, $"**{oldPath}** → **{newPath}**+");
				context.Call.SetResult($"Error copying file: {ex.Message}");
				return ToolExecutionResult.Failed;
			}
		}

		private class CopyDirectoryContext
		{
			public required string FullOldPath { get; init; }
			public required string FullNewPath { get; init; }
			public required AddonKind AddonKind { get; init; }
		}

		private DomainPreviewResult PreviewCopyDirectory(
			IToolExecutionContext context,
			string oldPath,
			string newPath,
			[SharedContext] out CopyDirectoryContext? ctx,
			bool overwrite = false)
		{
			var fullOldPath = _fileAccess.CheckedAccessPath(oldPath, DirectoryAccessMode.Write, out var isOldAccessed);
			var fullNewPath = _fileAccess.CheckedAccessPath(newPath, DirectoryAccessMode.Write, out var isNewAccessed);
			var destExisted = Directory.Exists(fullNewPath);
			var addonKind = _addonDetector.Detect(newPath, isFile: false,
				destExisted ? FileOperation.Edit : FileOperation.Create,
				destExisted ? null : FileUtils.GetExistingAncestorDirectory(fullNewPath));
			ctx = new CopyDirectoryContext { FullOldPath = fullOldPath, FullNewPath = fullNewPath, AddonKind = addonKind };

			if (!Directory.Exists(fullOldPath))
			{
				context.Call.SetStatus(MaterialIconKind.FolderQuestion, $"**{oldPath}**");
				context.Call.SetResult("Source directory not found.");

				return new DomainPreviewResult
				{
					ExpectedBehaviour = (!isOldAccessed || !isNewAccessed ? ToolBehaviour.AccessOutsideWorkdir : ToolBehaviour.None) |
						AddonKindToolBehaviourConverter.Convert(addonKind),
					InterruptingSuccess = false
				};
			}

			if (destExisted && !overwrite)
			{
				context.Call.SetStatus(MaterialIconKind.ContentCopy, $"**{oldPath}** → **{newPath}**");
				context.Call.SetResult("Destination directory already exists.");

				return new DomainPreviewResult
				{
					ExpectedBehaviour = (!isOldAccessed || !isNewAccessed ? ToolBehaviour.AccessOutsideWorkdir : ToolBehaviour.None) |
						AddonKindToolBehaviourConverter.Convert(addonKind),
					InterruptingSuccess = false
				};
			}

			context.Call.SetStatus(MaterialIconKind.ContentCopy, $"**{oldPath}** → **{newPath}**");

			return new DomainPreviewResult
			{
				ExpectedBehaviour = ToolBehaviour.FileDirectoryCreate |
					(!isOldAccessed || !isNewAccessed ? ToolBehaviour.AccessOutsideWorkdir : ToolBehaviour.None) |
					AddonKindToolBehaviourConverter.Convert(addonKind)
			};
		}

		private ToolExecutionResult CopyDirectory(
			IToolExecutionContext context,
			string oldPath,
			string newPath,
			bool overwrite = false,
			[SharedContext] CopyDirectoryContext? ctx = null)
		{
			try
			{
				var fullOldPath = ctx?.FullOldPath ?? _fileAccess.AccessPath(oldPath, DirectoryAccessMode.Write);
				var fullNewPath = ctx?.FullNewPath ?? _fileAccess.AccessPath(newPath, DirectoryAccessMode.Write);
				var destExisted = Directory.Exists(fullNewPath);
				var addonKind = ctx?.AddonKind ?? _addonDetector.Detect(newPath, isFile: false,
					destExisted ? FileOperation.Edit : FileOperation.Create,
					destExisted ? null : FileUtils.GetExistingAncestorDirectory(fullNewPath));

				if (!Directory.Exists(fullOldPath))
				{
					context.Call.SetStatus(MaterialIconKind.FolderQuestion, $"**{oldPath}** → **{newPath}**+");
					context.Call.SetResult($"Source directory not found: {oldPath}");
					return ToolExecutionResult.Failed;
				}

				if (destExisted && !overwrite)
				{
					context.Call.SetStatus(MaterialIconKind.FolderAlert, $"**{oldPath}** → **{newPath}**+");
					context.Call.SetResult($"Destination directory already exists: {newPath}");
					return ToolExecutionResult.Failed;
				}

				Directory.CreateDirectory(fullNewPath);

				foreach (var file in Directory.GetFiles(fullOldPath, "*", SearchOption.AllDirectories))
				{
					var relativePath = Path.GetRelativePath(fullOldPath, file);
					var destFile = Path.Combine(fullNewPath, relativePath);
					Directory.CreateDirectory(Path.GetDirectoryName(destFile)!);
					File.Copy(file, destFile, overwrite);
				}

				_addonInvalidator.Invalidate(addonKind);

				context.Call.SetStatus(MaterialIconKind.ContentCopy, $"**{oldPath}** → **{newPath}**+");
				context.Call.SetResult($"Directory copied from '{oldPath}' to '{newPath}'.");
				return ToolExecutionResult.Succeeded;
			}
			catch (Exception ex)
			{
				context.Call.SetStatus(MaterialIconKind.FolderAlert, $"**{oldPath}** → **{newPath}**+");
				context.Call.SetResult($"Error copying directory: {ex.Message}");
				return ToolExecutionResult.Failed;
			}
		}
	}
}
