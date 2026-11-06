using System.Text.Json;
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
			TypeInfoResolver = new DefaultJsonTypeInfoResolver()
		};

		extension(IHookDispatcher dispatcher)
		{
			public void ExecuteHooksFnF(INodeValue? payload,
				string hookType, string? hookName, HookExecutionFlow defaultFlow,
				CancellationToken cancellationToken = default)
			{
				_ = dispatcher.ExecuteHooksAsync(payload, hookType, hookName, defaultFlow,
					forcedFlow: HookExecutionFlow.FireAndForget, cancellationToken);
			}

			public void ExecuteHooksFnF(object? payload,
				string hookType, string? hookName, HookExecutionFlow defaultFlow,
				CancellationToken cancellationToken = default)
			{
				var serializedPayload = JsonSerializer.SerializeToNode(payload, _jsonSerializerOptions).ToConstNodeValue();
				_ = dispatcher.ExecuteHooksAsync(serializedPayload, hookType, hookName, defaultFlow,
					forcedFlow: HookExecutionFlow.FireAndForget, cancellationToken);
			}

			public async Task<T?> ExecuteHooksAsync<T>(object? payload,
				string hookType, string? hookName, HookExecutionFlow defaultFlow,
				HookExecutionFlow? forcedFlow = null, CancellationToken cancellationToken = default)
			{
				var serializedPayload = JsonSerializer.SerializeToNode(payload, _jsonSerializerOptions).ToConstNodeValue();
				var hookResult = await dispatcher.ExecuteHooksAsync(serializedPayload, hookType, hookName, defaultFlow, forcedFlow, cancellationToken);
				try
				{
					return JsonSerializer.Deserialize<T>(hookResult.ToJsonNode(), _jsonSerializerOptions);
				}
				catch (Exception ex)
				{
					Log.Warning(ex, "Error to gather return payload from hook of type {HookType} on key {HookKey}: {Error}",
						hookType, hookName, ex.Message);
					return default;
				}
			}
		}
	}
}
