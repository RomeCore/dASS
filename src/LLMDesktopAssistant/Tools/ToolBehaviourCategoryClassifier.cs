namespace LLMDesktopAssistant.Tools;

/// <summary>
/// Maps <see cref="ToolBehaviour"/> flags to their corresponding <see cref="ToolBehaviourCategory"/>.
/// </summary>
public static class ToolBehaviourCategoryClassifier
{
	/// <summary>
	/// Returns the category of the given tool behaviour flag,
	/// or <see cref="ToolBehaviourCategory.None"/> for <see cref="ToolBehaviour.None"/>,
	/// <see cref="ToolBehaviour.All"/>, or composite values spanning multiple categories.
	/// </summary>
	/// <param name="behaviour">The tool behaviour flag to classify.</param>
	/// <returns>The corresponding category, or <see cref="ToolBehaviourCategory.None"/> if the value cannot be classified.</returns>
	public static ToolBehaviourCategory GetCategory(ToolBehaviour behaviour)
	{
		return behaviour switch
		{
			ToolBehaviour.None or ToolBehaviour.All => ToolBehaviourCategory.None,

			ToolBehaviour.FileDirectoryCreate or ToolBehaviour.FileRead or ToolBehaviour.FileEdit
				or ToolBehaviour.FileDelete or ToolBehaviour.DirectoryRead
				or ToolBehaviour.DirectoryEdit or ToolBehaviour.DirectoryDelete => ToolBehaviourCategory.Filesystem,

			ToolBehaviour.SemanticMemoryRead or ToolBehaviour.SemanticMemoryWrite
				or ToolBehaviour.SemanticMemoryDelete or ToolBehaviour.SemanticMemoryClear => ToolBehaviourCategory.SemanticMemory,

			ToolBehaviour.DatabaseRead or ToolBehaviour.DatabaseChange
				or ToolBehaviour.DatabaseCustomConnect => ToolBehaviourCategory.Database,

			ToolBehaviour.ReadSecrets or ToolBehaviour.AccessOutsideWorkdir or ToolBehaviour.WorkdirChange
				or ToolBehaviour.ClipboardWrite or ToolBehaviour.ClipboardRead => ToolBehaviourCategory.Security,

			ToolBehaviour.InternetAccess => ToolBehaviourCategory.Network,

			ToolBehaviour.LongRunningTask => ToolBehaviourCategory.Performance,

			ToolBehaviour.ExecuteExternalProcess or ToolBehaviour.PossiblyUnexpected
				or ToolBehaviour.RunTerminal => ToolBehaviourCategory.Execution,

			ToolBehaviour.UserInteraction => ToolBehaviourCategory.UserInteraction,

			ToolBehaviour.AgentExecution => ToolBehaviourCategory.Agents,

			ToolBehaviour.AddonPackEdit or ToolBehaviour.PromptEdit or ToolBehaviour.ScriptEdit => ToolBehaviourCategory.Scriptable,

			ToolBehaviour.MCP or ToolBehaviour.Scriptable or ToolBehaviour.AdHoc => ToolBehaviourCategory.Source,

			_ => ToolBehaviourCategory.None
		};
	}

	/// <summary>
	/// Returns the set of categories spanned by the given tool behaviour flags.
	/// </summary>
	/// <param name="behaviourFlags">The tool behaviour flags to classify.</param>
	/// <returns>
	/// A combination of <see cref="ToolBehaviourCategory"/> values covering all categories
	/// of the set flags, or <see cref="ToolBehaviourCategory.None"/> when
	/// <paramref name="behaviourFlags"/> is <see cref="ToolBehaviour.None"/>.
	/// </returns>
	public static ToolBehaviourCategory GetCategories(ToolBehaviour behaviourFlags)
	{
		if (behaviourFlags is ToolBehaviour.None)
		{
			return ToolBehaviourCategory.None;
		}

		var categories = ToolBehaviourCategory.None;
		foreach (var flag in ToolBehaviours.AllValues)
		{
			if (behaviourFlags.HasFlag(flag))
			{
				categories |= GetCategory(flag);
			}
		}
		return categories;
	}
}
