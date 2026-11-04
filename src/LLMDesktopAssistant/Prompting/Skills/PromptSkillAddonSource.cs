using LLMDesktopAssistant.Addons;
using LLMDesktopAssistant.Addons.Sources;
using LLMDesktopAssistant.LLM.Services;
using LLMDesktopAssistant.Prompting.ContextExpanders;
using LLMDesktopAssistant.Prompting.Management;
using LLMDesktopAssistant.Prompting.Plugins;
using LLMDesktopAssistant.StructuredValues.Converters;
using LLTSharp;

namespace LLMDesktopAssistant.Prompting.Skills
{
	/// <summary>
	/// Contributes a <see cref="SkillInfo"/> per prompt template skill registered in the chat.
	/// </summary>
	/// <remarks>
	/// The source rebuilds its collection when the prompt part manager reports an import or a drop. The template body
	/// is rendered lazily by <see cref="AddonBase{Self}.BodyGetter"/> against the clone's resolved change, so the
	/// parameters of a cloned skill are applied at render time.
	/// </remarks>
	[ChatService(typeof(IAdditionalAddonSource<SkillInfo>))]
	public class PromptSkillAddonSource : ObservableAddonSourceBase<SkillInfo>
	{
		private readonly IPromptSkillManager _skillManager;
		private readonly IEnumerable<IPromptSystemContextExpander> _expanders;
		private readonly IEnumerable<IPromptTemplatePlugin> _plugins;

		/// <summary>
		/// Initializes a new instance of the <see cref="PromptSkillAddonSource"/> class.
		/// </summary>
		public PromptSkillAddonSource(
			IPromptSkillManager skillManager,
			IEnumerable<IPromptSystemContextExpander> promptSystemContextExpanders,
			IEnumerable<IPromptTemplatePlugin> promptTemplatePlugins)
		{
			_skillManager = skillManager;
			_expanders = promptSystemContextExpanders;
			_plugins = promptTemplatePlugins;

			skillManager.Changed += OnManagerChanged;
			Refresh();
		}

		/// <inheritdoc/>
		protected override void Dispose(bool disposing)
		{
			base.Dispose(disposing);

			if (disposing)
				_skillManager.Changed -= OnManagerChanged;
		}

		private void OnManagerChanged(object? sender, EventArgs e) => Refresh();

		private void Refresh()
		{
			var skills = new List<SkillInfo>();
			foreach (var promptSkill in _skillManager.GetAll())
			{
				skills.Add(new SkillInfo
				{
					Name = promptSkill.Name,
					Description = promptSkill.Description ?? string.Empty,
					BodyGetter = skill =>
					{
						var context = new Dictionary<string, object?>();
						foreach (var expander in _expanders)
							expander.ExpandPromptContext(context);
						var templateFunctions = new TemplateFunctionSet(_plugins.SelectMany(p => p.GetTemplateFunctions()));

						if (promptSkill.ParameterSchema is not null)
						{
							var parameters = promptSkill.ParameterSchema.Root.CreateOrFixValue(skill.Change?.Parameters, []);
							if (skill.Change is not null)
								skill.Change.Parameters = parameters;
							context["params"] = LLTStructuredConverter.ToTemplateDataAccessor(parameters);
						}
						return promptSkill.EffectiveTemplate.Render(context, templateFunctions).ToString() ?? string.Empty;
					},
					AddonSource = AddonSource.Template,
					TemplateSource = promptSkill.Source,
					ParameterSchema = promptSkill.ParameterSchema
				});
			}

			Items.Reset(skills);
		}
	}
}
