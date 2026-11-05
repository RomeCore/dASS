using System.Text.Json.Nodes;
using AsyncLua;
using AsyncLua.Values;
using LLMDesktopAssistant.Agents.Tasks;
using LLMDesktopAssistant.StructuredValues.Converters;
using LLMDesktopAssistant.Tools;

namespace LLMDesktopAssistant.Scripting.Lua
{
	/// <summary>
	/// A tool that allows agents to execute ad-hoc Lua functions.
	/// </summary>
	public class LuaAdHocAgentTool : AgentTool
	{
		public override string Name { get; }
		public override string DisplayName { get; }
		public override string Description { get; }
		public override JsonObject ArgumentSchema { get; }

		/// <summary>
		/// The calling context for the Lua script.
		/// </summary>
		public LuaCallingContext Context { get; }

		/// <summary>
		/// The Lua function to be called.
		/// </summary>
		public LuaFunction Function { get; }

		/// <summary>
		/// Initializes a new instance of the <see cref="LuaAdHocAgentTool"/> class.
		/// </summary>
		/// <param name="name">The name of the tool.</param>
		/// <param name="displayName">The display name of the tool.</param>
		/// <param name="description">The description of the tool.</param>
		/// <param name="argumentSchema">The schema for the arguments to be passed to the tool.</param>
		/// <param name="context">The calling context for the Lua script.</param>
		/// <param name="function">The Lua function to be called.</param>
		public LuaAdHocAgentTool(string name, string displayName, string description, JsonObject argumentSchema, LuaCallingContext context, LuaFunction function)
		{
			Name = name;
			DisplayName = displayName;
			Description = description;
			ArgumentSchema = argumentSchema;
			Context = context;
			Function = function;
		}

		public override async Task<AgentToolCallPreResult> PreExecuteAsync(JsonNode? arguments, CancellationToken cancellationToken = default)
		{
			return new AgentToolCallPreResult
			{
				ExpectedBehaviour = ToolBehaviour.AdHoc
			};
		}

		public override async Task<AgentToolCallResult> ExecuteAsync(JsonNode? arguments, object? sharedContext, CancellationToken cancellationToken = default)
		{
			try
			{
				var args = arguments.ToConstNodeValue().ToLuaValue();
				var result = await Function.InvokeAsync(Context, args);
				return new AgentToolCallResult
				{
					Success = true,
					Content = result.First.ToString()
				};
			}
			catch (Exception ex)
			{
				return new AgentToolCallResult
				{
					Success = false,
					Content = "Error executing ad-hoc tool: " + ex.Message
				};
			}
		}
	}
}