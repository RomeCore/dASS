using LLMDesktopAssistant.Addons.Sources;
using LLMDesktopAssistant.LLM.Services;

namespace LLMDesktopAssistant.Tools
{
	/// <summary>
	/// Contributes the tools of the MCP servers connected for the chat.
	/// </summary>
	/// <remarks>
	/// The connection set changes only when <see cref="IMCPManagementService.EnsureCurrentMCPConnectionsAsync"/> runs,
	/// so the source refreshes on <see cref="IMCPManagementService.ConnectionsChanged"/> instead of subscribing to a
	/// reactive connection collection.
	/// </remarks>
	[ChatService(typeof(IAdditionalAddonSource<ToolInfo>))]
	public class McpToolAddonSource : ObservableAddonSourceBase<ToolInfo>
	{
		private readonly IMCPManagementService _mcp;

		/// <summary>
		/// Initializes a new instance of the <see cref="McpToolAddonSource"/> class.
		/// </summary>
		public McpToolAddonSource(IMCPManagementService mcp)
		{
			_mcp = mcp;

			mcp.ConnectionsChanged += OnConnectionsChanged;
			Refresh();
		}

		/// <inheritdoc/>
		protected override void Dispose(bool disposing)
		{
			base.Dispose(disposing);

			if (disposing)
				_mcp.ConnectionsChanged -= OnConnectionsChanged;
		}

		private void OnConnectionsChanged(object? sender, EventArgs e) => Refresh();

		private void Refresh()
			=> Items.Reset(_mcp.GetMCPTools().SelectMany(module => module.GetTools()));
	}
}
