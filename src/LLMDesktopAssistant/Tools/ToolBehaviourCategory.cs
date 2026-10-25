namespace LLMDesktopAssistant.Tools;

/// <summary>
/// Groups <see cref="ToolBehaviour"/> flags into broad categories
/// used for UI grouping and policy management.
/// </summary>
[Flags]
public enum ToolBehaviourCategory
{
	None = 0,
	Filesystem = 1 << 0,
	SemanticMemory = 1 << 1,
	Database = 1 << 2,
	Security = 1 << 3,
	Network = 1 << 4,
	Performance = 1 << 5,
	Execution = 1 << 6,
	UserInteraction = 1 << 7,
	Agents = 1 << 8,
	Scriptable = 1 << 9,
	Source = 1 << 10,
}
