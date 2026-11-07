using System.Text.Json.Nodes;
using LLMDesktopAssistant.Tools;

namespace LLMDesktopAssistant.Agents.Tasks
{
	internal sealed class SkillLoadTool : AgentTool
	{
		/// <summary>
		/// The skills to load.
		/// </summary>
		public required ImmutableDictionary<string, AgentSkill> Skills { get; init; }

		public override string Name => "skill-load";
		public override string DisplayName => Name;
		public override string Description => "Loads a skill (SKILL.md format) by its name.";
		public override JsonObject ArgumentSchema => new JsonObject
		{
			["type"] = "object",
			["properties"] = new JsonObject
			{
				["name"] = new JsonObject
				{
					["type"] = "string",
					["description"] = "The name of the skill to load."
				}
			},
			["required"] = new JsonArray
			{
				"name"
			}
		};

		public override ToolSource Source => ToolSource.Native;

		public override Task<AgentToolCallPreResult> PreExecuteAsync(JsonNode? arguments, CancellationToken cancellationToken = default)
		{
			string skillName = arguments?.AsObject()["name"]?.GetValue<string>() ?? throw new ArgumentNullException(nameof(arguments), "Skill name is required.");

			if (!Skills.ContainsKey(skillName))
			{
				return Task.FromResult(new AgentToolCallPreResult
				{
					ExpectedBehaviour = ToolBehaviour.None,
					InterruptingSuccess = false,
					InterruptingContent = $"Skill '{skillName}' not found."
				});
			}

			return Task.FromResult(new AgentToolCallPreResult
			{
				ExpectedBehaviour = ToolBehaviour.None
			});
		}

		public override async Task<AgentToolCallResult> ExecuteAsync(JsonNode? arguments, object? sharedContext, CancellationToken cancellationToken = default)
		{
			string skillName = arguments?.AsObject()["name"]?.GetValue<string>() ?? throw new ArgumentNullException(nameof(arguments), "Skill name is required.");

			if (!Skills.TryGetValue(skillName, out var skill))
			{
				return new AgentToolCallResult
				{
					Success = false,
					Content = $"Skill '{skillName}' not found."
				};
			}

			var body = await skill.GetBodyAsync(cancellationToken);
			return new AgentToolCallResult
			{
				Success = true,
				Content = string.IsNullOrWhiteSpace(skill.HomeDirectory) ? body : $"""
					{body}

					---
					
					**Note**: all paths in the skill are relative to skill's home path: *{skill.HomeDirectory}*
					"""
			};
		}
	}
}
