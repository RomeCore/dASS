using LLMDesktopAssistant.MCP;
using LLMDesktopAssistant.Tools;

namespace LLMDesktopAssistant.LLM.Services
{
	public interface IMCPManagementService
	{
		/// <summary>
		/// Occurs when the set of connected MCP servers changes after
		/// <see cref="EnsureCurrentMCPConnectionsAsync"/> has completed.
		/// </summary>
		event EventHandler? ConnectionsChanged;

		/// <summary>
		/// Checks if chat has any MCP in their configuration.
		/// </summary>
		/// <returns>true if any chat has an MCP configuration; otherwise, false.</returns>
		bool HasMCPConnections();

		/// <summary>
		/// Ensures that all current MCP (Message Control Protocol) connections are established and active asynchronously.
		/// </summary>
		/// <param name="cancellationToken">A cancellation token that can be used to cancel the operation.</param>
		/// <returns>A task that represents the asynchronous operation.</returns>
		Task EnsureCurrentMCPConnectionsAsync(CancellationToken cancellationToken = default);

		/// <summary>
		/// Retrieves an array of available MCP tool modules with their associated tool information.
		/// </summary>
		/// <returns>An array of <see cref="ToolInfo"/> objects representing the available MCP tool modules.</returns>
		MCPToolModule[] GetMCPTools();
	}
}