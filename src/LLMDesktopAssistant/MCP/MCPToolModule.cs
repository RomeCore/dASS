using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using LLMDesktopAssistant.Localization;
using LLMDesktopAssistant.Tools;
using LLMDesktopAssistant.Tools.Execution.Domain;
using LLMDesktopAssistant.Tools.Execution.Executors;
using LLMDesktopAssistant.Tools.Execution.Support;
using LLMDesktopAssistant.Utils;
using ModelContextProtocol.Client;
using ModelContextProtocol.Protocol;
using Serilog;

namespace LLMDesktopAssistant.MCP
{
	/// <summary>
	/// A tool module that integrates with an MCP server to retrieve and execute tools defined on the server.
	/// It connects to the MCP server, lists available tools, converts them into a format compatible with the LLM assistant,
	/// and provides execution logic to call the tools on the server when invoked by the assistant.
	/// </summary>
	public partial class MCPToolModule : ToolModule, IAsyncDisposable
	{
		private readonly MCPConnection _connection;
		private readonly IAsyncDisposable _listChangedSub;

		private MCPToolModule(MCPConnection connection, IEnumerable<ToolInfo> tools)
		{
			_connection = connection;
			_listChangedSub = _connection.Client.RegisterNotificationHandler("tools/list_changed", ToolsChanged);
			ReplaceTools(tools);
		}

		private async ValueTask ToolsChanged(JsonRpcNotification notification, CancellationToken cancellationToken)
		{
			var tools = await ListToolsAsync(_connection, cancellationToken);
			ReplaceTools(tools);
		}

		public async ValueTask DisposeAsync()
		{
			await _listChangedSub.DisposeAsync();
		}

		/// <summary>
		/// Asynchronously creates an instance of the <see cref="MCPToolModule"/> by connecting to the
		/// MCP server using the provided connection, retrieving the list of tools from the server,
		/// converting them into the appropriate format, and initializing the module with these tools.
		/// </summary>
		/// <param name="connection"></param>
		/// <param name="cancellationToken"></param>
		/// <returns></returns>
		public static async Task<MCPToolModule> CreateAsync(MCPConnection connection,
			CancellationToken cancellationToken = default)
		{
			return new MCPToolModule(connection, await ListToolsAsync(connection, cancellationToken));
		}

		private static async Task<IEnumerable<ToolInfo>> ListToolsAsync(MCPConnection connection, CancellationToken cancellationToken = default)
		{
			var tools = await connection.Client.ListToolsAsync(cancellationToken: cancellationToken);
			return tools.Select(t => ConvertTool(connection, t));
		}

		private static readonly Regex _toolNameInvalidFilterRegex = ToolNameFilterRegex();
		[GeneratedRegex(@"[^a-zA-Z0-9_-]+", RegexOptions.Compiled)]
		private static partial Regex ToolNameFilterRegex();

		private static string ConvertToolPrefix(string serverName)
		{
			serverName = serverName.Replace(" ", "_").ToLower();
			serverName = _toolNameInvalidFilterRegex.Replace(serverName, ""); // Remove invalid characters for tool names
			return serverName;
		}

		private static ToolInfo ConvertTool(MCPConnection connection, McpClientTool mcpTool)
		{
			var argSchema = JsonObject.Create(mcpTool.JsonSchema)
				?? throw new InvalidOperationException($"Unsupported JSON schema type: {mcpTool.JsonSchema.ValueKind}");

			// Use connection name as prefix
			var name = ConvertToolPrefix(connection.Info.Name) + '-' + mcpTool.Name;
			var description = mcpTool.Description;

			var toolInfo = new ToolInfo
			{
				Name = name,
				DescriptionGetter = _ => description,
				ArgumentSchema = argSchema,
				OutputSchema = mcpTool.ReturnJsonSchema?.ToNodeSafe() as JsonObject,
				Main = CreateExecutor(connection, mcpTool),
				ToolSource = ToolSource.MCP,
				// Optimistic on purpose: an MCP server is opaque, and claiming a write would serialize
				// every remote tool against the whole disk. A read claim lets them run in parallel while
				// still waiting out any local write.
				StaticSyncKeys = [new(ToolSyncKeyKind.AllFiles, string.Empty, ToolSyncAccess.Read)],
				DefaultExpectedBehaviour = ToolBehaviour.MCP,
				NameKey = Locale.GetConstKey(mcpTool.Title ?? mcpTool.Name),
				CategoryKey = Locale.GetConstKey(connection.Info.Name)
			};
			return toolInfo;
		}

		private static IToolMainExecutor CreateExecutor(MCPConnection connection, McpClientTool mcpTool)
		{
			return new DelegateMainExecutor(async (args, ctx, cancellationToken) =>
			{
				try
				{
					var dictArgs = args.Deserialize<Dictionary<string, object?>>();
					var result = await mcpTool.CallAsync(dictArgs, cancellationToken: cancellationToken);

					var contents = string.Join("\n\n", result.Content.Select(c =>
					{
						if (c is TextContentBlock textCb)
							return textCb.Text;
						Log.Warning("MCP Tool Executor: content type {Type} is not supported.", c.Type);
						return "";
					}));

					ctx.Call.SetStructuredResult(result.StructuredContent?.ToNodeSafe());

					if (result.IsError == true)
					{
						if (string.IsNullOrWhiteSpace(contents))
							ctx.Call.SetResult("Tool executed with an error.");
						else
							ctx.Call.SetResult(contents);
						return ToolExecutionResult.Failed;
					}

					if (string.IsNullOrWhiteSpace(contents))
						ctx.Call.SetResult("Tool executed successfully with no output.");
					else
						ctx.Call.SetResult(contents);
					return ToolExecutionResult.Succeeded;
				}
				catch (Exception ex)
				{
					ctx.Call.SetResult($"Error calling MCP '{connection.Info.Name}' tool: {ex.Message}");
					return ToolExecutionResult.Failed;
				}
			});
		}
	}
}