using LLMDesktopAssistant.Addons;

namespace LLMDesktopAssistant.Tools
{
	public static class AddonKindToolBehaviourConverter
	{
		public static ToolBehaviour Convert(AddonKind kind)
		{
			var result = ToolBehaviour.None;

			if (kind.HasFlag(AddonKind.Pack))
				result |= ToolBehaviour.AddonPackEdit;
			if (kind.HasFlag(AddonKind.Skill))
				result |= ToolBehaviour.PromptEdit;
			if (kind.HasFlag(AddonKind.SubAgent))
				result |= ToolBehaviour.PromptEdit;
			if (kind.HasFlag(AddonKind.Tool))
				result |= ToolBehaviour.ScriptEdit;
			if (kind.HasFlag(AddonKind.MemoryBlock))
				result |= ToolBehaviour.PromptEdit;
			if (kind.HasFlag(AddonKind.PromptContext))
				result |= ToolBehaviour.PromptEdit;
			if (kind.HasFlag(AddonKind.SlashCommand))
				result |= ToolBehaviour.ScriptEdit;
			if (kind.HasFlag(AddonKind.Template))
				result |= ToolBehaviour.PromptEdit;
			if (kind.HasFlag(AddonKind.LuaScript))
				result |= ToolBehaviour.ScriptEdit;

			return result;
		}
	}
}
