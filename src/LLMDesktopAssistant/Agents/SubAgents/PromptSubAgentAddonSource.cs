using LLMDesktopAssistant.Addons;
using LLMDesktopAssistant.Addons.Sources;
using LLMDesktopAssistant.LLM.Services;
using LLMDesktopAssistant.Prompting.ContextExpanders;
using LLMDesktopAssistant.Prompting.Management;
using LLMDesktopAssistant.Prompting.Plugins;
using LLMDesktopAssistant.StructuredValues.Converters;
using LLTSharp;

namespace LLMDesktopAssistant.Agents.SubAgents
{
	/// <summary>
	/// Contributes a <see cref="SubAgentInfo"/> per prompt template sub-agent registered in the chat.
	/// </summary>
	/// <remarks>
	/// Template sub-agents have a higher <see cref="AddonBase{Self}.OverrideOrder"/> than file-system ones, so a
	/// template with the same name wins. The source rebuilds its collection when the prompt part manager reports an
	/// import or a drop.
	/// </remarks>
	[ChatService(typeof(IAdditionalAddonSource<SubAgentInfo>))]
	public class PromptSubAgentAddonSource : ObservableAddonSourceBase<SubAgentInfo>
	{
		private readonly IPromptSubAgentManager _subAgentManager;
		private readonly IEnumerable<IPromptSystemContextExpander> _expanders;
		private readonly IEnumerable<IPromptTemplatePlugin> _plugins;

		/// <summary>
		/// Initializes a new instance of the <see cref="PromptSubAgentAddonSource"/> class.
		/// </summary>
		public PromptSubAgentAddonSource(
			IPromptSubAgentManager subAgentManager,
			IEnumerable<IPromptSystemContextExpander> promptSystemContextExpanders,
			IEnumerable<IPromptTemplatePlugin> promptTemplatePlugins)
		{
			_subAgentManager = subAgentManager;
			_expanders = promptSystemContextExpanders;
			_plugins = promptTemplatePlugins;

			subAgentManager.Changed += OnManagerChanged;
			Refresh();
		}

		/// <inheritdoc/>
		protected override void Dispose(bool disposing)
		{
			base.Dispose(disposing);

			if (disposing)
				_subAgentManager.Changed -= OnManagerChanged;
		}

		private void OnManagerChanged(object? sender, EventArgs e) => Refresh();

		private void Refresh()
		{
			var subAgents = new List<SubAgentInfo>();
			foreach (var promptSubAgent in _subAgentManager.GetAll())
			{
				subAgents.Add(new SubAgentInfo
				{
					Name = promptSubAgent.Name,
					OverrideOrder = 1, // Template sub-agents have priority over file-system sub-agents.
					Description = promptSubAgent.Description ?? string.Empty,
					BodyGetter = subAgent =>
					{
						var context = new Dictionary<string, object?>();
						foreach (var expander in _expanders)
							expander.ExpandPromptContext(context);
						var templateFunctions = new TemplateFunctionSet(_plugins.SelectMany(p => p.GetTemplateFunctions()));

						if (promptSubAgent.ParameterSchema is not null)
						{
							var parameters = promptSubAgent.ParameterSchema.Root.CreateOrFixValue(subAgent.Change?.Parameters, []);
							if (subAgent.Change is not null)
								subAgent.Change.Parameters = parameters;
							context["params"] = LLTStructuredConverter.ToTemplateDataAccessor(parameters);
						}
						return promptSubAgent.EffectiveTemplate.Render(context, templateFunctions).ToString() ?? string.Empty;
					},
					AddonSource = AddonSource.Template,
					TemplateSource = promptSubAgent.Source,
					ParameterSchema = promptSubAgent.ParameterSchema
				});
			}

			Items.Reset(subAgents);
		}
	}
}
