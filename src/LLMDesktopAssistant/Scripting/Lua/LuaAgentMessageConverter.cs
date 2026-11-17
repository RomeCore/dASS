using System.Text.Json.Nodes;
using AsyncLua;
using AsyncLua.Values;
using LLMDesktopAssistant.Agents.Tasks;
using LLMDesktopAssistant.LLM.Domain;
using LLMDesktopAssistant.StructuredValues.Converters;
using LLMDesktopAssistant.Utils;
using LLMDesktopAssistant.Utils.Attachments;

namespace LLMDesktopAssistant.Scripting.Lua
{
	/// <summary>
	/// Converts agent task messages, tool calls and usage statistics between the C# model and the Lua tables the
	/// scripting API hands out.
	/// </summary>
	/// <remarks>
	/// Shared by the launch (<c>dass.agent.execute_task</c>) and the task handle, so the two cannot drift: a message
	/// table produced by one is accepted by the other.
	/// </remarks>
	internal static class LuaAgentMessageConverter
	{
		/// <summary>
		/// Builds an agent message from a Lua table shaped as <c>{ role = ..., content = ... }</c>.
		/// </summary>
		/// <exception cref="LuaRuntimeException">The role is not one of <c>system</c>, <c>user</c>, <c>assistant</c>.</exception>
		public static AgentChatMessage FromLua(LuaTable messageTable)
		{
			var role = messageTable.Get("role").ToString();
			var content = messageTable.Get("content").ToString();

			switch (role)
			{
				case "system":
					return new AgentSystemMessage { Content = content };

				case "user":
					return new AgentUserMessage
					{
						Content = content,
						Attachments = [.. ReadAttachments(messageTable.Get("attachments"))]
					};

				case "assistant":
					return new AgentAssistantMessage
					{
						ReasoningSignature = messageTable.Get("reasoning_signature").ToString(),
						ReasoningContent = messageTable.Get("reasoning_content").ToString(),
						Content = content,
						Attachments = [.. ReadAttachments(messageTable.Get("attachments"))],
						ToolCalls = [.. ReadToolCalls(messageTable.Get("tool_calls"))]
					};

				default:
					throw new LuaRuntimeException($"agent task message: unknown role '{role}'.");
			}
		}

		/// <summary>
		/// Converts an agent message into the Lua table the scripting API documents.
		/// </summary>
		public static LuaTable ToLua(AgentChatMessage message)
		{
			switch (message)
			{
				case AgentSystemMessage systemMessage:
					var systemMessageTable = new LuaTable();
					systemMessageTable.Set("role", "system");
					systemMessageTable.Set("content", systemMessage.Content);
					return systemMessageTable;

				case AgentUserMessage userMessage:
					var userMessageTable = new LuaTable();
					userMessageTable.Set("role", "user");
					userMessageTable.Set("content", userMessage.Content);
					userMessageTable["attachments"] = ToLuaAttachments(userMessage.Attachments);
					return userMessageTable;

				case AgentAssistantMessage assistantMessage:
					var assistantMessageTable = new LuaTable();
					assistantMessageTable.Set("role", "assistant");
					assistantMessageTable.Set("reasoning_signature", assistantMessage.ReasoningSignature);
					assistantMessageTable.Set("reasoning_content", assistantMessage.ReasoningContent);
					assistantMessageTable.Set("content", assistantMessage.Content);
					assistantMessageTable["attachments"] = ToLuaAttachments(assistantMessage.Attachments);

					var toolCallsTable = LuaExtensions.CreateArrayTable();
					foreach (var toolCall in assistantMessage.ToolCalls)
						toolCallsTable.Append(ToolCallToLua(toolCall));
					assistantMessageTable["tool_calls"] = toolCallsTable;

					if (assistantMessage.UsageStatistics != null)
						assistantMessageTable["usage"] = UsageToLua(assistantMessage.UsageStatistics);

					return assistantMessageTable;

				default:
					throw new LuaRuntimeException($"agent task message: unsupported message '{message}'.");
			}
		}

		/// <summary>
		/// Converts usage statistics into the Lua table an assistant message and the task handle report.
		/// </summary>
		public static LuaTable UsageToLua(AgentUsageStatistics usage)
		{
			var usageTable = new LuaTable();
			usageTable.Set("input_tokens", usage.InputTokens);
			usageTable.Set("output_tokens", usage.OutputTokens);
			usageTable.Set("cache_create_tokens", usage.CacheCreateTokens);
			usageTable.Set("input_cache_hit_tokens", usage.InputCacheHitTokens);
			usageTable.Set("input_cache_miss_tokens", usage.InputCacheMissTokens);
			usageTable.Set("time_to_first_token", usage.TimeToFirstToken.TotalMilliseconds);
			usageTable.Set("inference_time", usage.InferenceTime.TotalMilliseconds);
			usageTable.Set("execution_time", usage.ExecutionTime.TotalMilliseconds);
			return usageTable;
		}

		private static List<AgentAttachment> ReadAttachments(LuaValue attachmentsValue)
		{
			List<AgentAttachment> attachments = [];
			foreach (var attachmentValue in (attachmentsValue as LuaTable)?.Values ?? [])
				attachments.Add(AttachmentFromLua(attachmentValue));
			return attachments;
		}

		private static List<AgentToolCall> ReadToolCalls(LuaValue toolCallsValue)
		{
			List<AgentToolCall> toolCalls = [];
			foreach (var toolCallValue in (toolCallsValue as LuaTable)?.Values ?? [])
			{
				if (toolCallValue is not LuaTable toolCallTable)
					throw new LuaRuntimeException("agent task message: every entry of 'tool_calls' must be a table.");

				toolCalls.Add(ToolCallFromLua(toolCallTable));
			}
			return toolCalls;
		}

		private static AgentAttachment AttachmentFromLua(LuaValue attachmentValue)
		{
			if (attachmentValue is LuaUserData { Target: LuaImage image })
				return AgentNativeAttachment.FromBase64(AttachmentKind.Image, image.Format, image.ToBase64(), image);

			throw new LuaRuntimeException("agent task message: unsupported attachment value " + attachmentValue);
		}

		private static LuaTable ToLuaAttachments(IEnumerable<AgentAttachment> attachments)
		{
			var attachmentsTable = LuaExtensions.CreateArrayTable();
			foreach (var attachment in attachments)
				if (AttachmentToLua(attachment) is { } attachmentValue)
					attachmentsTable.Append(attachmentValue);
			return attachmentsTable;
		}

		private static LuaValue? AttachmentToLua(AgentAttachment attachment)
		{
			// Only a Lua image remembers its original userdata; a model-produced attachment has no Lua form.
			if (attachment is AgentNativeAttachment { Type: AttachmentKind.Image, Source: LuaImage luaImage })
				return LuaValueConverter.ToLuaValue(luaImage);

			return null;
		}

		private static AgentToolCall ToolCallFromLua(LuaTable toolCallTable)
		{
			var toolName = toolCallTable.Get("tool_name").ToString();
			var toolCallId = toolCallTable.Get("tool_call_id").ToString();
			var arguments = toolCallTable.Get("arguments").ToConstNodeValue(tolerant: true).ToJsonNode();

			var resultSuccess = toolCallTable.Get("result_success");
			var resultContent = toolCallTable.Get("result_content").ToString();
			var attachments = ReadAttachments(toolCallTable.Get("result_attachments"));
			var success = resultSuccess is LuaNil || resultSuccess.ToBoolean();

			return new AgentToolCall
			{
				ToolCallId = toolCallId,
				ToolName = toolName,
				Arguments = arguments?.ToJsonString() ?? "{}",
				// The call was executed before the message was handed to us - leaving the default Pending would
				// report replayed history as a call that is still running.
				Status = success ? ToolStatus.Success : ToolStatus.Error,
				Result = new AgentToolCallResult
				{
					Success = success,
					Content = resultContent
				}
			};
		}

		private static LuaTable ToolCallToLua(AgentToolCall toolCall)
		{
			var toolCallTable = new LuaTable();
			toolCallTable.Set("tool_call_id", toolCall.ToolCallId);
			toolCallTable.Set("tool_name", toolCall.ToolName);
			toolCallTable.Set("arguments", TolerantJsonParser.Parse(toolCall.Arguments).ToConstNodeValue().ToLuaValue());
			toolCallTable.Set("result_success", toolCall.Result?.Success ?? true);
			toolCallTable.Set("result_content", toolCall.Result?.Content ?? "");
			toolCallTable["result_attachments"] = ToLuaAttachments(toolCall.Result?.Attachments ?? []);
			return toolCallTable;
		}
	}
}
