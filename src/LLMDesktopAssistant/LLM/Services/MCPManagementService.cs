using LLMDesktopAssistant.LLM.Domain;
using LLMDesktopAssistant.MCP;
using LLMDesktopAssistant.Settings;
using RCLargeLanguageModels;

namespace LLMDesktopAssistant.LLM.Services
{
	[ChatService(typeof(IMCPManagementService))]
	public class MCPManagementService(
		IChatSettingsService chatSettings
		) : IMCPManagementService
	{
		private MCPConnectionInfo[] _usedConnections = [];

		/// <inheritdoc/>
		public event EventHandler? ConnectionsChanged;

		public bool HasMCPConnections()
		{
			if (!chatSettings.Settings.Mcp.EnableMcp)
				return false;

			var usedServerIds = chatSettings.Settings.Mcp.GetEffectiveUsedMcpServers()
				.Intersect(SettingsManager.Get<MCPConfiguration>().Servers.Select(s => s.Id));

			return usedServerIds.Any();
		}

		public async Task EnsureCurrentMCPConnectionsAsync(CancellationToken cancellationToken = default)
		{
			if (!chatSettings.Settings.Mcp.EnableMcp)
			{
				_usedConnections = [];
				ConnectionsChanged?.Invoke(this, EventArgs.Empty);
				return;
			}

			var usedServerIds = chatSettings.Settings.Mcp.GetEffectiveUsedMcpServers()
				.Intersect(SettingsManager.Get<MCPConfiguration>().Servers.Select(s => s.Id));

			var usedConnectionTasks = usedServerIds.Select(id => MCPManager.EnsureConnectionAsync(id, cancellationToken));
			_usedConnections = await Task.WhenAll(usedConnectionTasks);
			ConnectionsChanged?.Invoke(this, EventArgs.Empty);
		}

		public MCPToolModule[] GetMCPTools()
		{
			if (!chatSettings.Settings.Mcp.EnableMcp)
				return [];

			return _usedConnections.Select(c => c.ToolModule).ToArray();
		}
	}
}