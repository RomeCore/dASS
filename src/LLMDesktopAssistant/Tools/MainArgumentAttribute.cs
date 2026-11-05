namespace LLMDesktopAssistant.Tools
{
	/// <summary>
	/// Marks the executor parameter that is the main argument of the tool: the value an unnamed
	/// specifier literal is matched against, and the value a bare (non-object) argument set would carry.
	/// At most one parameter of a tool method may carry it.
	/// </summary>
	[AttributeUsage(AttributeTargets.Parameter)]
	public sealed class MainArgumentAttribute : Attribute
	{
	}
}
