using LLMDesktopAssistant.Agents.Tasks;
using LLMDesktopAssistant.Controls.Icons;
using LLMDesktopAssistant.LLM.MVVM.Additional;
using LLMDesktopAssistant.Localization;
using LLMDesktopAssistant.Prompting.Skills;
using LLMDesktopAssistant.SlashCommands.Arguments;
using LLMDesktopAssistant.SlashCommands.Execution;
using Material.Icons;

namespace LLMDesktopAssistant.SlashCommands.Providers
{
	/// <summary>
	/// Runs <c>/skill:&lt;name&gt; [args]</c>: loads the skill body, substitutes its arguments/variables and injects it
	/// into the command's message as a content part, so the agent gets the skill in full without a <c>skill-load</c>
	/// round-trip.
	/// </summary>
	/// <remarks>
	/// The body is always injected in full (the skill's <c>InjectionMode</c> does not apply) with the home-directory
	/// note appended, exactly like the <c>skill-load</c> tool. The command leaves the caller's generation intent
	/// untouched and shows the model the bare token rather than the raw <c>/skill:…</c> text.
	/// </remarks>
	public sealed class SkillCommandExecutor(SkillInfo skill) : ISlashCommandExecutor
	{
		/// <inheritdoc/>
		public SlashCommandArgumentSchema? ArgumentSchema { get; } = new()
		{
			HasRestPositional = true
		};

		/// <inheritdoc/>
		public async Task<SlashCommandExecutionResult> ExecuteAsync(SlashCommandExecutionContext ctx, CancellationToken ct)
		{
			var agentSkill = new ChatAgentSkill(skill);

			var body = await agentSkill.GetBodyAsync(ct);
			body = SlashCommandVariableExpander.ExpandSkill(body, ctx.Arguments, skill);

			if (agentSkill.HomeDirectory is { Length: > 0 } home)
			{
				body = $"""
					{body}

					---

					**Note**: all paths in the skill are relative to skill's home path: *{home}*
					""";
			}

			ctx.Message.AdditionalData.Add(new AdditionalMessageContentPart
			{
				Content = body,
				ChipTitle = Locale.GetFormattedKey("command.skill.used", skill.Name),
				ChipIcon = (VisualIconKind)MaterialIconKind.Lightbulb,
				IsRestorable = false
			});

			return SlashCommandExecutionResult.Ok(modelFacingMode: ModelFacingMode.Raw);
		}
	}
}
