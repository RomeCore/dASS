using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using LLMDesktopAssistant.StructuredValues;
using LLMDesktopAssistant.StructuredValues.Converters;
using Serilog;

namespace LLMDesktopAssistant.Hooks
{
	public static class HookDispatcherExtensions
	{
		private static readonly JsonSerializerOptions _jsonSerializerOptions = new()
		{
			Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
			PropertyNameCaseInsensitive = true,

			// Scripts already receive `hook_type` and `hook_target`, so their documents are snake_case too.
			PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
			TypeInfoResolver = new DefaultJsonTypeInfoResolver()
		};

		static HookDispatcherExtensions()
		{
			_jsonSerializerOptions.Converters.Add(new JsonStringEnumConverter());
		}

		extension(IHookDispatcher dispatcher)
		{
			public void ExecuteHooksFnF(INodeValue? payload,
				string hookType, string? hookTarget, CancellationToken cancellationToken = default)
			{
				_ = dispatcher.ExecuteHooksAsync(payload, hookType, hookTarget, HookExecutionFlow.FireAndForget,
					isFlowForced: true, cancellationToken);
			}

			public void ExecuteHooksFnF(object? payload,
				string hookType, string? hookTarget, CancellationToken cancellationToken = default)
			{
				var serializedPayload = JsonSerializer.SerializeToNode(payload, _jsonSerializerOptions).ToConstNodeValue();
				_ = dispatcher.ExecuteHooksAsync(serializedPayload, hookType, hookTarget, HookExecutionFlow.FireAndForget,
					isFlowForced: true, cancellationToken);
			}

			public async Task<HookExecutionResult<T?>> ExecuteHooksAsync<T>(object? payload,
				string hookType, string? hookTarget, HookExecutionFlow defaultFlow,
				bool isFlowForced = false, CancellationToken cancellationToken = default)
			{
				var serializedPayload = JsonSerializer.SerializeToNode(payload, _jsonSerializerOptions).ToConstNodeValue();
				var hookResult = await dispatcher.ExecuteHooksAsync(serializedPayload, hookType, hookTarget, defaultFlow,
					isFlowForced, cancellationToken);

				// A blocking hook produces a message and no value: deserializing the absent value would throw
				// and take the message with it, turning a veto into "no opinion".
				if (hookResult.Value.ToJsonNode() is not { } node)
					return new HookExecutionResult<T?>(default, hookResult.BlockingMessage);

				try
				{
					return new HookExecutionResult<T?>(JsonSerializer.Deserialize<T>(node, _jsonSerializerOptions),
						hookResult.BlockingMessage);
				}
				catch (Exception ex)
				{
					Log.Warning(ex, "Error to gather return payload from hook of type {HookType} on target {HookTarget}: {Error}",
						hookType, hookTarget, ex.Message);
					return default;
				}
			}
		}
	}
}
