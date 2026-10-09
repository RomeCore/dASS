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
	/// untouched and the model sees the raw message text (<see cref="ModelFacingMode.Raw"/>, the v1 default).
	/// </remarks>
	public sealed class SkillCommandExecutor(SkillInfo skill) : ISlashCommandExecutor
	{
		/// <inheritdoc/>
		public SlashCommandArgumentSchema? ArgumentSchema { get; } = new()
		{
			RestPositional = new SlashCommandArgument
			{
				Name = Locale.GetKey("command.argument.skill.arguments"),
				Description = Locale.GetKey("command.argument.skill.arguments.description")
			}
		};

		/// <inheritdoc/>
		public async Task<SlashCommandExecutionResult> ExecuteAsync(SlashCommandExecutionContext ctx, CancellationToken ct)
		{
			var agentSkill = new ChatAgentSkill(skill);

			var body = await agentSkill.GetBodyAsync(ct);
			body = SlashCommandVariableExpander.ExpandSkill(body, ctx.Arguments, skill);

			body = $"""
				[USER HAS INVOKED SKILL MANUALLY (you don't need to invoke it by yourself)]:

				{body}
				""";
			
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
