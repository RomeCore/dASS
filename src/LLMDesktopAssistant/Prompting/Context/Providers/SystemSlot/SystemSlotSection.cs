using LLMDesktopAssistant.Agents;
using LLMDesktopAssistant.LLM.Services;
using LLMDesktopAssistant.LLM.Services.Prompting;
using LLMDesktopAssistant.Prompting.ContextExpanders;
using LLMDesktopAssistant.Prompting.Management;
using LLMDesktopAssistant.Prompting.Plugins;
using LLMDesktopAssistant.StructuredValues.Converters;
using LLMDesktopAssistant.Utils.Files;
using LiteDB;
using LLTSharp;

namespace LLMDesktopAssistant.Prompting.Context.Providers.SystemSlot
{
	/// <summary>
	/// The state of the system slot section: the rendered system prompt text and its components.
	/// </summary>
	public class SystemSlotSectionState : PromptSectionStateBase
	{
		private string _text = string.Empty;
		/// <summary>
		/// The rendered text of the system slot.
		/// </summary>
		public string Text
		{
			get => _text;
			set => SetProperty(ref _text, value);
		}
	}

	/// <summary>
	/// The delta of the system slot section: a unified diff between the previously known text
	/// and the current text of the section.
	/// </summary>
	public class SystemSlotSectionDelta : PromptSectionDeltaBase
	{
		/// <summary>
		/// The hunk groups of the diff (as computed by <see cref="UnifiedDiff.Compute"/>).
		/// </summary>
		public List<HunkGroup> Groups { get; init; } = [];

		/// <summary>
		/// The diff as a <see cref="HunkGroups"/> collection, used for folding and rendering.
		/// </summary>
		[BsonIgnore]
		public HunkGroups Diff => new() { Groups = Groups };
	}

	/// <summary>
	/// Captures the system slot state: the system prompt text (custom or a system slot element)
	/// and the prompt components.
	/// </summary>
	[ChatService(typeof(IPromptSectionStateProvider<SystemSlotSectionState>))]
	public class SystemSlotStateProvider(
		ITemplateLibraryAccessor templates,
		IChatSettingsService chatSettings,
		IPromptSlotElementManager slotElementManager,
		IPromptComponentManager componentManager,
		IEnumerable<IPromptSystemContextExpander> promptSystemContextExpanders,
		IEnumerable<IPromptTemplatePlugin> promptTemplatePlugins
		) : IPromptSectionStateProvider<SystemSlotSectionState>
	{
		/// <inheritdoc/>
		public SystemSlotSectionState CaptureState(ChatAgentDescriptor agent)
		{
			var template = templates.GetTextTemplate("system_slot_system_section");
			var functions = new TemplateFunctionSet(promptTemplatePlugins.SelectMany(p => p.GetTemplateFunctions()));
			var promptSettings = agent.Prompts;

			var generalContext = new Dictionary<string, object?>();
			foreach (var expander in promptSystemContextExpanders)
				expander.ExpandPromptContext(generalContext);
			var partsContext = generalContext.ToDictionary();

			var effectiveSystemPrompt = promptSettings.GetEffectiveSystemPrompt(chatSettings.Settings);
			var effectiveComponents = promptSettings.GetEffectivePromptComponents(chatSettings.Settings);

			string? RenderPromptPart<K, V>(IPromptPartManager<K, V> manager, PromptPartSelection selection, K key)
				where K : notnull
				where V : PromptPartBase
			{
				var part = manager.TryGet(key);
				if (part is null)
					return null;
				if (part.ParameterSchema is not null)
				{
					selection.Parameters = part.ParameterSchema.Root.CreateOrFixValue(selection.Parameters, []);
					partsContext["params"] = LLTStructuredConverter.ToTemplateDataAccessor(selection.Parameters);
				}
				var result = part.EffectiveTemplate.Render(partsContext, functions).ToString();
				partsContext.Remove("params");
				return result;
			}

			generalContext["prompt"] =
				effectiveSystemPrompt.UseCustomSystemPrompt ? effectiveSystemPrompt.CustomSystemPrompt :
				RenderPromptPart(slotElementManager, effectiveSystemPrompt, (effectiveSystemPrompt.Id, PromptSlotKind.System));
			generalContext["components"] = effectiveComponents
				.Select(c => RenderPromptPart(componentManager, c, c.Id))
				.Where(c => !string.IsNullOrWhiteSpace(c))
				.ToArray();

			return new SystemSlotSectionState
			{
				Text = template.Render(generalContext, functions)
			};
		}
	}

	/// <summary>
	/// Renders the system slot section state into a snapshot (text only).
	/// </summary>
	[ChatService(typeof(IPromptSectionStateRenderer<SystemSlotSectionState>))]
	public class SystemSlotStateRenderer : IPromptSectionStateRenderer<SystemSlotSectionState>
	{
		/// <inheritdoc/>
		public SystemPromptSnapshot Render(SystemSlotSectionState state) => state.Text;
	}

	/// <summary>
	/// Delta provider of the system slot section: diffs the current rendered text
	/// against the known one (anchor state + previously issued deltas).
	/// </summary>
	[ChatService(typeof(IPromptSectionDeltaProvider<SystemSlotSectionState, SystemSlotSectionDelta>))]
	public class SystemSlotDeltaProvider(
		IPromptSectionStateProvider<SystemSlotSectionState> stateProvider
	) : IPromptSectionDeltaProvider<SystemSlotSectionState, SystemSlotSectionDelta>
	{
		/// <inheritdoc/>
		public SystemSlotSectionDelta? CalculateDelta(SystemSlotSectionState? anchorState,
			IEnumerable<SystemSlotSectionDelta> existingDeltas, EffectiveChatContext context)
		{
			var knownText = anchorState?.Text ?? string.Empty;
			foreach (var delta in existingDeltas)
				knownText = delta.Diff.ApplyToText(knownText);

			var currentState = stateProvider.CaptureState(context.Agent);
			if (currentState is null)
				return null;

			var diff = UnifiedDiff.Compute(knownText, currentState.Text);
			if (!diff.HasGroups)
				return null;

			return new SystemSlotSectionDelta
			{
				Groups = diff.Groups
			};
		}
	}

	/// <summary>
	/// Delta renderer of the system slot section: renders the diff as a clean unified diff
	/// (no hunk headers, hunks separated by an ellipsis line).
	/// </summary>
	[ChatService(typeof(IPromptSectionDeltaRenderer<SystemSlotSectionDelta>))]
	public class SystemSlotDeltaRenderer(
		ITemplateLibraryAccessor templates
	) : IPromptSectionDeltaRenderer<SystemSlotSectionDelta>
	{
		/// <inheritdoc/>
		public string Render(SystemSlotSectionDelta delta)
		{
			return templates.GetTextTemplate("system_slot_system_section_delta").Render(new
			{
				diff = delta.Diff.ToCleanString()
			});
		}
	}

	/// <summary>
	/// The system slot section: the system prompt text and its components.
	/// </summary>
	public class SystemSlotSection(IServiceProvider services)
		: PromptAnchoredSectionBase<SystemSlotSectionState, SystemSlotSectionDelta>(services)
	{
		public override string Discriminator => "system-slot";
	}

	[ChatService(typeof(PromptContextNativeProvider))]
	public class SystemSlotSectionProvider : PromptContextNativeProvider
	{
		public SystemSlotSectionProvider(IServiceProvider services)
		{
			AddContext(new PromptContextInfo
			{
				Name = "system-slot",
				Order = 0,
				Description = string.Empty,
				IsFixed = true,
				Provider = new SystemSlotSection(services)
			});
		}
	}
}
