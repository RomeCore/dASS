using System.ComponentModel;
using System.Text.Json.Nodes;
using LLMDesktopAssistant.Tools.MVVM;
using RCLargeLanguageModels.Json.Schema;
using LLMDesktopAssistant.Localization;
using LLMDesktopAssistant.LLM.Services;

namespace LLMDesktopAssistant.Tools.Implementations;

/// <summary>
/// Module for user interaction tools via forms (Human-in-the-Loop).
/// </summary>
[ToolModule]
public class FormsToolModule : ToolModule
{
	private readonly IChatExecutionStatusService _chatExecutionStatusService;

	public FormsToolModule(IChatExecutionStatusService chatExecutionStatusService)
	{
		_chatExecutionStatusService = chatExecutionStatusService;

		AddTool(new ToolInitializationInfo
		{
			Executor = FormsConfirm,
			Name = "forms-confirm",
			Description = "Requests the user to confirm an action. " +
				"Shows a message with 'Confirm' and 'Cancel' buttons. " +
				"Use this tool when you need to ask the user for permission before performing an important or dangerous action.",
			NameKey = Locale.GetKey("tool.name.forms-confirm"),
			DescriptionKey = Locale.GetKey("tool.description.forms-confirm"),
			CategoryKey = Locale.GetKey("tool.category.forms"),
			DefaultExpectedBehaviour = ToolBehaviour.UserInteraction
		});

		AddTool(new ToolInitializationInfo
		{
			Executor = FormsChoice,
			Name = "forms-choice",
			Description = "Offers the user to choose one or more options from a list. " +
				"Can allow custom input (allowCustom). " +
				"Use when the user needs to make a selection from the provided options.",
			NameKey = Locale.GetKey("tool.name.forms-choice"),
			DescriptionKey = Locale.GetKey("tool.description.forms-choice"),
			CategoryKey = Locale.GetKey("tool.category.forms"),
			DefaultExpectedBehaviour = ToolBehaviour.UserInteraction
		});

		AddTool(new ToolInitializationInfo
		{
			Executor = FormsInput,
			Name = "forms-input",
			Description = "Requests data input from the user via a form with one or more fields. " +
				"Supports field types: text, number, password, multiline. " +
				"Use when you need structured data from the user.",
			NameKey = Locale.GetKey("tool.name.forms-input"),
			DescriptionKey = Locale.GetKey("tool.description.forms-input"),
			CategoryKey = Locale.GetKey("tool.category.forms"),
			DefaultExpectedBehaviour = ToolBehaviour.UserInteraction
		});

		AddTool(new ToolInitializationInfo
		{
			Executor = FormsFilePicker,
			Name = "forms-file_picker",
			Description = "Opens a file selection dialog for the user. " +
				"Can filter by extensions and allow multiple file selection. " +
				"Use when the user needs to specify a file path on their system.",
			NameKey = Locale.GetKey("tool.name.forms-file_picker"),
			DescriptionKey = Locale.GetKey("tool.description.forms-file_picker"),
			CategoryKey = Locale.GetKey("tool.category.forms"),
			DefaultExpectedBehaviour = ToolBehaviour.UserInteraction | ToolBehaviour.DirectoryRead
		});
	}

	private async Task FormsConfirm(
		ToolExecutionContext context,
		ReactiveToolResult result,
		[Description("Title of the confirmation question. For example: 'Delete file?', 'Confirm sending?'")] string title,
		[Description("Detailed description of what needs to be confirmed. Provide context for the user.")] string? description,
		[Description("Text on the confirm button (default: 'OK')")] string? confirmText,
		[Description("Text on the cancel button (default: 'Cancel')")] string? cancelText,
		[Description("Is this a dangerous action? If true, the button will be red (default: false)")] bool? isDanger,
		CancellationToken cancellationToken = default)
	{
		if (!context.RunningInUI)
		{
			result.ResultContent = "This tool requires a UI context to run.";
			result.TryCompleteWithError();
			return;
		}

		var viewModel = new FormsConfirmViewModel
		{
			Title = title,
			Description = description ?? string.Empty,
			ConfirmText = confirmText ?? "OK",
			CancelText = cancelText ?? "Cancel",
			IsDanger = isDanger ?? false
		};
		result.AdditionalData.Add(viewModel);

		bool confirmed;
		try
		{
			using var confirmation = _chatExecutionStatusService.WithConfirmation();
			confirmed = await viewModel.Result.WaitAsync(cancellationToken);
		}
		catch (OperationCanceledException)
		{
			result.AdditionalData.Remove(viewModel);
			result.ResultContent = "User cancelled the confirmation.";
			result.TryCompleteWithError();
			return;
		}

		if (confirmed)
		{
			result.ResultContent = $"User confirmed: \"{title}\".";
			result.TryCompleteWithSuccess();
		}
		else
		{
			result.ResultContent = $"User declined: \"{title}\".";
			result.TryCompleteWithSuccess();
		}
	}

	private async Task FormsChoice(
		ToolExecutionContext context,
		ReactiveToolResult result,
		[Description("Title of the question")] string title,
		[Description("Detailed description of what needs to be selected")] string? description,
		[Description("Array of options to choose from. Each option is a string that will be shown to the user and returned as a value.")]
		string[] options,
		[Description("Can multiple options be selected (default: false)")] bool? allowMultiple,
		[Description("Can the user enter a custom option (default: false)")] bool? allowCustom,
		[Description("Minimum number of selectable options (default: 1)")] int? minSelect,
		[Description("Maximum number of selectable options (default: 1, when allowMultiple=true: all options)")] int? maxSelect,
		CancellationToken cancellationToken = default)
	{
		if (!context.RunningInUI)
		{
			result.ResultContent = "This tool requires a UI context to run.";
			result.TryCompleteWithError();
			return;
		}

		var formOptions = new List<ChoiceOption>();

		foreach (var option in options)
		{
			formOptions.Add(new ChoiceOption
			{
				Value = option,
				Label = option
			});
		}

		var viewModel = new FormsChoiceViewModel(formOptions)
		{
			Title = title,
			Description = description ?? string.Empty,
			AllowMultiple = allowMultiple ?? false,
			AllowCustom = allowCustom ?? false,
			MinSelect = minSelect ?? 1,
			MaxSelect = maxSelect ?? (allowMultiple == true ? options.Length : 1)
		};
		result.AdditionalData.Add(viewModel);

		ChoiceResult formResult;
		try
		{
			using var confirmation = _chatExecutionStatusService.WithConfirmation();
			formResult = await viewModel.Result.WaitAsync(cancellationToken);
		}
		catch (OperationCanceledException)
		{
			result.AdditionalData.Remove(viewModel);
			result.ResultContent = "User cancelled the selection.";
			result.TryCompleteWithError();
			return;
		}

		var selectedStr = string.Join(", ", formResult.Selected);
		var resultText = $"User selected: {selectedStr}.";
		if (!string.IsNullOrWhiteSpace(formResult.Custom))
			resultText += $" Additional text: \"{formResult.Custom}\".";

		result.ResultContent = resultText;
		result.TryCompleteWithSuccess();
	}

	private async Task FormsInput(
		ToolExecutionContext context,
		ReactiveToolResult result,
		[Description("Title of the form")] string title,
		[Description("Description of the form")] string? description,
		[Description("Array of JSON objects describing form fields. Each object must contain:\n- id (string, required) — field key in the result\n- label (string, required) — display label for the field\n- type (string, optional) — field type: 'text' (default), 'number', 'password', 'multiline'\n- placeholder (string, optional) — placeholder text inside the field\n- required (bool, optional) — whether the field is required (default: false)\n- default (string, optional) — default value\nExample: [{\"id\": \"name\", \"label\": \"Name\", \"required\": true}, {\"id\": \"comment\", \"label\": \"Comment\", \"type\": \"multiline\"}]")]
		JsonNode[] fields,
		CancellationToken cancellationToken = default)
	{
		if (!context.RunningInUI)
		{
			result.ResultContent = "This tool requires a UI context to run.";
			result.TryCompleteWithError();
			return;
		}

		var formFields = new List<InputField>();

		foreach (var fieldNode in fields)
		{
			if (fieldNode is not JsonObject fieldObj)
				continue;

			var id = fieldObj["id"]?.GetValue<string>() ?? string.Empty;
			if (string.IsNullOrWhiteSpace(id))
				continue;

			formFields.Add(new InputField
			{
				Id = id,
				Label = fieldObj["label"]?.GetValue<string>() ?? id,
				Placeholder = fieldObj["placeholder"]?.GetValue<string>() ?? string.Empty,
				FieldType = fieldObj["type"]?.GetValue<string>() ?? "text",
				IsRequired = fieldObj["required"]?.GetValue<bool>() ?? false,
				Value = fieldObj["default"]?.GetValue<string>() ?? string.Empty
			});
		}

		if (formFields.Count == 0)
		{
			result.ResultContent = "Failed to parse form fields. Make sure a valid array of fields with id and label is provided.";
			result.TryCompleteWithError();
			return;
		}

		var viewModel = new FormsInputViewModel(formFields)
		{
			Title = title,
			Description = description ?? string.Empty
		};
		result.AdditionalData.Add(viewModel);

		InputResult formResult;
		try
		{
			using var confirmation = _chatExecutionStatusService.WithConfirmation();
			formResult = await viewModel.Result.WaitAsync(cancellationToken);
		}
		catch (OperationCanceledException)
		{
			result.AdditionalData.Remove(viewModel);
			result.ResultContent = "User cancelled data input.";
			result.TryCompleteWithError();
			return;
		}

		var valuesStr = string.Join(", ", formResult.Values.Select(kv => $"{kv.Key}=\"{kv.Value}\""));
		result.ResultContent = $"User entered data: {valuesStr}.";
		result.TryCompleteWithSuccess();
	}

	private async Task FormsFilePicker(
		ToolExecutionContext context,
		ReactiveToolResult result,
		[Description("Title of the file selection dialog")] string title,
		[Description("Description or instructions for the user")] string? description,
		[Description("The mode of the dialog."), Enum(["open", "save", "directory"])] string mode,
		[Description("Extension filter, e.g. '*.cs;*.py;*.js'")] string? filter = null,
		[Description("Allow multiple file selection")] bool allowMultiple = false,
		CancellationToken cancellationToken = default)
	{
		if (!context.RunningInUI)
		{
			result.ResultContent = "This tool requires a UI context to run.";
			result.CompleteWithError();
			return;
		}

		var viewModel = new FormsFilePickerViewModel
		{
			Title = title,
			Description = description ?? string.Empty,
			Mode = mode switch
			{
				"open" => FilePickerMode.Open,
				"save" => FilePickerMode.Save,
				"directory" => FilePickerMode.Directory,
				_ => throw new ArgumentException("Invalid mode specified for file picker.", nameof(mode))
			},
			Filter = filter,
			AllowMultiple = allowMultiple
		};

		result.AdditionalData.Add(viewModel);

		try
		{
			using var confirmation = _chatExecutionStatusService.WithConfirmation();
			var formResult = await viewModel.Result.WaitAsync(cancellationToken);
			if (formResult.Paths.Length == 0)
			{
				result.ResultContent = "User did not select any files.";
				result.TryCompleteWithSuccess();
				return;
			}

			result.ResultContent = $"User selected files: {string.Join(", ", formResult.Paths)}.";
			result.TryCompleteWithSuccess();
			return;
		}
		catch (OperationCanceledException)
		{
			result.AdditionalData.Remove(viewModel);
			result.ResultContent = "User cancelled file selection.";
			result.TryCompleteWithError();
			return;
		}
	}
}
