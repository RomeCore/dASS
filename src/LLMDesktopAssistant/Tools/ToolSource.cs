namespace LLMDesktopAssistant.Tools
{
	/// <summary>
	/// Enumerates the possible sources of tools within the application.
	/// </summary>
	public enum ToolSource
	{
		/// <summary>
		/// Indicates that the tool source is unknown.
		/// </summary>
		Unknown,

		/// <summary>
		/// Indicates that the tool is provided by the native application.
		/// </summary>
		Native,

		/// <summary>
		/// Indicates that the tool is a scriptable addon: a file the LLM created at runtime through scripting.
		/// </summary>
		Script,

		/// <summary>
		/// Indicates that the tool is provided by the MCP (Model Context Protocol) system.
		/// </summary>
		MCP,

		/// <summary>
		/// Indicates that the tool is an ad-hoc tool, created on-the-fly inside scripts or else.
		/// </summary>
		AdHoc
	}
}