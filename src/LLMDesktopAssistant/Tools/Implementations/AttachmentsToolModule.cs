using System.ComponentModel;
using System.Globalization;
using LLMDesktopAssistant.Agents.Tasks;
using LLMDesktopAssistant.LLM.MVVM.Additional;
using LLMDesktopAssistant.LLM.Services;
using LLMDesktopAssistant.LLM.Settings;
using LLMDesktopAssistant.Localization;
using LLMDesktopAssistant.Prompting;
using LLMDesktopAssistant.Providers;
using LLMDesktopAssistant.Tools.Execution.Contexts;
using LLMDesktopAssistant.Tools.Execution.Domain;
using LLMDesktopAssistant.Tools.Execution.Support;
using LLMDesktopAssistant.Tools.Implementations.Filesystem;
using LLMDesktopAssistant.Utils;
using LLMDesktopAssistant.Utils.Attachments;
using Material.Icons;
using RCLargeLanguageModels;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;

namespace LLMDesktopAssistant.Tools.Implementations
{
	[ToolModule(chatScoped: true)]
	public class AttachmentsToolModule : ToolModule
	{
		private readonly IChatSettingsService _chatSettings;
		private readonly ITemplateLibraryAccessor _templates;
		private readonly IWorkingDirectoryAccessService _wdAccess;
		private readonly IAgentTaskExecutor _agentTaskExecutor;
		private readonly IModelManager _modelManager;

		public AttachmentsToolModule(IChatSettingsService chatSettings,
			ITemplateLibraryAccessor templates,
			IWorkingDirectoryAccessService wdAccess,
			IAgentTaskExecutor agentTaskExecutor,
			IModelManager modelManager)
		{
			_chatSettings = chatSettings;
			_templates = templates;
			_wdAccess = wdAccess;
			_agentTaskExecutor = agentTaskExecutor;
			_modelManager = modelManager;

			AddTool(new ToolInitializationInfo
			{
				Executor = ReadImage,
				StreamingAnalyzer = PathStreaming,
				PreviewExecutor = ReadImagePreview,
				SyncAnalyzer = FilesystemSynchronizationAnalyzer.AnalyzeFileRead,
				Name = "attach-read_image",
				Description = "Reads image from file and puts image content into result, " +
					"with optional relative cropping supported (coordinates have top-left origin). " +
					"Use when you are sure that you are a vision model.",
				NameKey = Locale.GetKey("tool.name.attach-read_image"),
				DescriptionKey = Locale.GetKey("tool.description.attach-read_image"),
				CategoryKey = Locale.GetKey("tool.category.attachments"),
				DefaultExpectedBehaviour = ToolBehaviour.FileRead | ToolBehaviour.AccessOutsideWorkdir
			});

			AddTool(new ToolInitializationInfo
			{
				Executor = DescribeImage,
				StreamingAnalyzer = PathStreaming,
				PreviewExecutor = DescribeImagePreview,
				SyncAnalyzer = FilesystemSynchronizationAnalyzer.AnalyzeFileRead,
				Name = "attach-describe_image",
				Description = "Describes an image using another LLM agent that can see images. Use it when you cannot see images by yourself (e.g. you are not a vision model).",
				NameKey = Locale.GetKey("tool.name.attach-describe_image"),
				DescriptionKey = Locale.GetKey("tool.description.attach-describe_image"),
				CategoryKey = Locale.GetKey("tool.category.attachments"),
				DefaultExpectedBehaviour = ToolBehaviour.AgentExecution | ToolBehaviour.LongRunningTask |
					ToolBehaviour.FileRead | ToolBehaviour.AccessOutsideWorkdir
			});
		}

		private StreamingToolAnalysisResult PathStreaming(
			IToolExecutionContext ctx,
			string? path)
		{
			path ??= "?";
			ctx.Call.SetStatus(MaterialIconKind.Image, $"**{path.MarkdownEscape()}**");
			return StreamingToolAnalysisResult.Continue;
		}

		private PreviewToolExecutionResult ReadImagePreview(IToolExecutionContext ctx,
			string path, [SharedContext] out string fullPath,
			double cropX = 0, double cropY = 0, double cropWidth = 1, double cropHeight = 1)
		{
			fullPath = _wdAccess.CheckedAccessPath(path, DirectoryAccessMode.Read, out var isAccessed);

			ctx.Call.SetStatus(MaterialIconKind.Image, $"**{path.MarkdownEscape()}**");

			if (!File.Exists(fullPath))
			{
				ctx.Call.SetResult($"File not found: {path}");
				return new PreviewToolExecutionResult
				{
					ExpectedBehaviour = !isAccessed ? ToolBehaviour.AccessOutsideWorkdir : ToolBehaviour.None,
					InterruptingSuccess = false
				};
			}

			// Caught before the call runs, so a nonsense crop never reaches the tool body.
			if (!TryValidateCropArguments(cropX, cropY, cropWidth, cropHeight, out var cropError))
			{
				ctx.Call.SetResult($"Invalid crop arguments: {cropError}.");
				return new PreviewToolExecutionResult
				{
					ExpectedBehaviour = !isAccessed ? ToolBehaviour.AccessOutsideWorkdir : ToolBehaviour.None,
					InterruptingSuccess = false
				};
			}

			return new PreviewToolExecutionResult
			{
				ExpectedBehaviour = ToolBehaviour.FileRead |
					(!isAccessed ? ToolBehaviour.AccessOutsideWorkdir : ToolBehaviour.None)
			};
		}

		private async Task<ToolExecutionResult> ReadImage(
			[SharedContext] string? fullPath,
			[Description("The path to the image file to describe")] string path,
			IToolExecutionContext ctx,
			[Description("Relative crop coordinate from left")] double cropX = 0,
			[Description("Relative crop coordinate from top")] double cropY = 0,
			[Description("Relative horizontal crop size")] double cropWidth = 1,
			[Description("Relative vertical crop size")] double cropHeight = 1)
		{
			ctx.Call.SetStatus(MaterialIconKind.Image, $"**{path.MarkdownEscape()}**");
			ctx.Call.UseMarkdown = true;

			if (!File.Exists(fullPath))
			{
				ctx.Call.SetResult($"File not found: {path}");
				return ToolExecutionResult.Failed;
			}

			if (!TryValidateCropArguments(cropX, cropY, cropWidth, cropHeight, out var cropError))
			{
				ctx.Call.SetResult($"Invalid crop arguments: {cropError}.");
				return ToolExecutionResult.Failed;
			}

			try
			{
				fullPath ??= _wdAccess.AccessPath(path, DirectoryAccessMode.Read);
				var image = Image.Load(fullPath);

				// Read before the crop, since Mutate shrinks the image in place.
				var sourceWidth = image.Width;
				var sourceHeight = image.Height;

				var cropRectangle = ResolveCropRectangle(sourceWidth, sourceHeight,
					cropX, cropY, cropWidth, cropHeight, out var wasClamped);

				var message = "Image read and attached.";

				if (cropRectangle is { } rectangle)
				{
					image.Mutate(p => p.Crop(rectangle));

					if (wasClamped)
					{
						// Invariant culture, because the model reads this and "0,5" parses as two values.
						var x = (rectangle.X / (double)sourceWidth).ToString("0.###", CultureInfo.InvariantCulture);
						var y = (rectangle.Y / (double)sourceHeight).ToString("0.###", CultureInfo.InvariantCulture);
						var width = (rectangle.Width / (double)sourceWidth).ToString("0.###", CultureInfo.InvariantCulture);
						var height = (rectangle.Height / (double)sourceHeight).ToString("0.###", CultureInfo.InvariantCulture);

						message += $"\nCrop exceeded the image bounds and was clamped to x={x}, y={y}, width={width}, height={height}.";
					}
				}

				ctx.Call.AddAttachment(new NativeAttachmentMessagePart
				{
					NativeAttachment = new SerializableImageAttachment(image),
					IsVisible = false
				});
				ctx.Call.SetResult(message);

				return ToolExecutionResult.Succeeded;
			}
			catch (Exception ex)
			{
				ctx.Call.AppendResultLine($"Got error while reading image: {ex.Message}. ");
				return ToolExecutionResult.Failed;
			}
		}

		/// <summary>
		/// Rejects the crop arguments that stay meaningless whatever the image size is: a non-finite
		/// number and a crop of zero size have no rectangle behind them.
		/// </summary>
		private static bool TryValidateCropArguments(double cropX, double cropY,
			double cropWidth, double cropHeight, out string? error)
		{
			error = null;

			if (!double.IsFinite(cropX) || !double.IsFinite(cropY) ||
				!double.IsFinite(cropWidth) || !double.IsFinite(cropHeight))
			{
				error = "cropX, cropY, cropWidth and cropHeight must be finite numbers";
				return false;
			}

			if (cropWidth <= 0 || cropHeight <= 0)
			{
				error = "cropWidth and cropHeight must be greater than 0, since a crop of zero size reads nothing";
				return false;
			}

			return true;
		}

		/// <summary>
		/// Turns the relative crop arguments into a pixel rectangle inside the image, pulling a request
		/// that runs past the edge back onto the edge. Returns null when the arguments cover the whole
		/// image, so an ordinary read never copies the bitmap.
		/// </summary>
		private static Rectangle? ResolveCropRectangle(int sourceWidth, int sourceHeight,
			double cropX, double cropY, double cropWidth, double cropHeight, out bool wasClamped)
		{
			// The model guesses these fractions without knowing the source size, so overshooting the
			// edge is normal input and has to crop the edge instead of throwing.
			var left = ClampToPixels(sourceWidth * cropX, 0, sourceWidth - 1);
			var top = ClampToPixels(sourceHeight * cropY, 0, sourceHeight - 1);
			var width = ClampToPixels(sourceWidth * cropWidth, 1, sourceWidth - left);
			var height = ClampToPixels(sourceHeight * cropHeight, 1, sourceHeight - top);

			const double epsilon = 1e-6;
			wasClamped = cropX < -epsilon || cropY < -epsilon ||
				cropX + cropWidth > 1 + epsilon || cropY + cropHeight > 1 + epsilon;

			var rectangle = new Rectangle(left, top, width, height);
			if (rectangle.Width >= sourceWidth && rectangle.Height >= sourceHeight)
				return null;

			return rectangle;
		}

		/// <summary>
		/// Rounds a fractional pixel coordinate to the nearest pixel and holds it inside the given range.
		/// </summary>
		private static int ClampToPixels(double value, int min, int max)
			=> Math.Clamp((int)Math.Round(value), min, max);

		private PreviewToolExecutionResult DescribeImagePreview(IToolExecutionContext ctx,
			string path, [SharedContext] out string fullPath)
		{
			fullPath = _wdAccess.CheckedAccessPath(path, DirectoryAccessMode.Read, out var isAccessed);

			ctx.Call.SetStatus(MaterialIconKind.Image, $"**{path.MarkdownEscape()}**");

			if (!File.Exists(fullPath))
			{
				ctx.Call.SetResult($"File not found: {path}");
				return new PreviewToolExecutionResult
				{
					ExpectedBehaviour = !isAccessed ? ToolBehaviour.AccessOutsideWorkdir : ToolBehaviour.None,
					InterruptingSuccess = false
				};
			}

			return new PreviewToolExecutionResult
			{
				ExpectedBehaviour = ToolBehaviour.AgentExecution | ToolBehaviour.LongRunningTask | ToolBehaviour.FileRead |
					(!isAccessed ? ToolBehaviour.AccessOutsideWorkdir : ToolBehaviour.None)
			};
		}

		private async Task<ToolExecutionResult> DescribeImage(
			[SharedContext] string? fullPath,
			[Description("The title of the agent call to be visible in UI")] string? callTitle,
			[Description("The path to the image file to describe")] string path,
			IToolExecutionContext ctx,
			CancellationToken cancellationToken = default)
		{
			ctx.Call.SetStatus(MaterialIconKind.Image, $"**{path.MarkdownEscape()}**");
			ctx.Call.UseMarkdown = true;

			var modelName = _chatSettings.Settings.Models.GetEffectiveSelection().VisionModel;
			if (string.IsNullOrEmpty(modelName))
			{
				ctx.Call.SetResult($"No vision model selected. Say user to select a vision model first.");
				return ToolExecutionResult.Failed;
			}

			if (!File.Exists(fullPath))
			{
				ctx.Call.SetResult($"File not found: {path}");
				return ToolExecutionResult.Failed;
			}

			LLModel llm;
			try
			{
				llm = _modelManager.GetModel(modelName);
			}
			catch (Exception ex)
			{
				ctx.Call.SetResult($"Vision model '{modelName}' is not available: {ex.Message}");
				return ToolExecutionResult.Failed;
			}

			try
			{
				fullPath ??= _wdAccess.AccessPath(path, DirectoryAccessMode.Read);
				var image = Image.Load(fullPath);
				using var memstream = new MemoryStream();
				image.SaveAsPng(memstream);
				var format = "png";
				var base64 = Convert.ToBase64String(memstream.ToArray());
				var url = "data:image/" + format + ";base64," + base64;

				var agentTask = _agentTaskExecutor.Execute(new AgentTaskLaunchParameters
				{
					TaskName = callTitle,
					TriggeredChat = ctx.Chat,
					TriggeredMessage = (ctx as ChatToolExecutionContext)?.Message,
					Model = llm,
					InitialMessages = [
						new AgentSystemMessage { Content = _templates.GetTextTemplate("image_describer_prompt")
							.Render() },
						new AgentUserMessage {
							Content = "Please describe the image.",
							Attachments = [new AgentNativeAttachment
							{
								Type = AttachmentKind.Image,
								Url = url
							}]
						}
					]
				}, cancellationToken);

				ctx.Call.SetResult(agentTask.LastGeneratedContent ?? string.Empty);

				int tokenCounter = 0;
				void LastResponseChanged(object? sender, PropertyChangedEventArgs e)
				{
					ctx.Call.SetResult(agentTask.LastGeneratedContent ?? string.Empty);
					tokenCounter++;

					if (tokenCounter > 1)
						ctx.Call.SetStatusTitle(string.Format(LocalizationManager.LocalizeStatic("tool.status.attach-describe_image.status"), tokenCounter));
				}
				agentTask.PropertyChanged += LastResponseChanged;

				try
				{
					await agentTask;
				}
				finally
				{
					agentTask.PropertyChanged -= LastResponseChanged;
				}

				ctx.Call.SetStatusTitle($"**{path.MarkdownEscape()}**");
				return ToolExecutionResult.Succeeded;
			}
			catch (Exception ex)
			{
				ctx.Call.AppendResultLine($"Got error: {ex.Message}. " +
					$"May be the model is not a vision model or API is down. Please try again later.");
				return ToolExecutionResult.Failed;
			}
		}
	}
}
