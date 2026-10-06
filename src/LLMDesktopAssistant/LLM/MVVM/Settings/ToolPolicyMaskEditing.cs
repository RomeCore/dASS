using LLMDesktopAssistant.Tools;

namespace LLMDesktopAssistant.LLM.MVVM.Settings
{
	/// <summary>
	/// Helpers for reading and editing a <see cref="ToolPolicyMask"/> from the behaviour-policy toggles UI.
	/// </summary>
	public static class ToolPolicyMaskEditing
	{
		/// <summary>
		/// Gets the toggle state of a single behaviour flag:
		/// <see langword="true"/> - auto-approve, <see langword="false"/> - disallowed, <see langword="null"/> - default (ask).
		/// </summary>
		public static bool? GetFlagState(ToolPolicyMask mask, ToolBehaviour flag)
		{
			if (mask.AutoApproveBehaviours.HasFlag(flag))
				return true;
			if (mask.DisallowedBehaviours.HasFlag(flag))
				return false;
			return null;
		}

		/// <summary>
		/// Returns a copy of <paramref name="mask"/> with the state of a single behaviour flag set.
		/// </summary>
		public static ToolPolicyMask SetFlag(ToolPolicyMask mask, ToolBehaviour flag, bool? state)
		{
			return state switch
			{
				true => new ToolPolicyMask
				{
					AutoApproveBehaviours = mask.AutoApproveBehaviours | flag,
					DisallowedBehaviours = mask.DisallowedBehaviours & ~flag
				},
				false => new ToolPolicyMask
				{
					AutoApproveBehaviours = mask.AutoApproveBehaviours & ~flag,
					DisallowedBehaviours = mask.DisallowedBehaviours | flag
				},
				_ => new ToolPolicyMask
				{
					AutoApproveBehaviours = mask.AutoApproveBehaviours & ~flag,
					DisallowedBehaviours = mask.DisallowedBehaviours & ~flag
				}
			};
		}
	}
}
