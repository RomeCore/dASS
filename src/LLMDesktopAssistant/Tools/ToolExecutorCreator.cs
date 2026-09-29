using System.Reflection;
using System.Text.Json.Nodes;
using LLMDesktopAssistant.Services;
using RCLargeLanguageModels.Json;
using RCLargeLanguageModels.Json.Schema;
using RCLargeLanguageModels.Tools;
using Serilog;

namespace LLMDesktopAssistant.Tools
{
	public static class ToolExecutorCreator
	{
		public static (
			JsonObject ArgumentSchema,
			Func<JsonNode?, ToolExecutionContext, CancellationToken, Task<ReactiveToolResult>> Executor,
			IDictionary<string, JsonMemberAccessor> Parameters)

			Create(Delegate executor)
		{
			return Create(executor.Target, executor.Method);
		}

		public static (
			JsonObject ArgumentSchema,
			Func<JsonNode?, ToolExecutionContext, CancellationToken, Task<ReactiveToolResult>> Executor,
			IDictionary<string, JsonMemberAccessor> Parameters)

			Create(object? target, MethodInfo method)
		{
			if (method == null)
				throw new ArgumentNullException(nameof(method));
			if (target == null && !method.IsStatic)
				throw new ArgumentException("If method target is null, method must be static.", nameof(method));

			var ret = method.ReturnType;
			if (
				ret != typeof(ReactiveToolResult) &&
				ret != typeof(Task<ReactiveToolResult>) &&
				ret != typeof(ToolResult) &&
				ret != typeof(Task<ToolResult>) &&
				ret != typeof(string) &&
				ret != typeof(Task<string>) &&
				ret != typeof(void) &&
				ret != typeof(Task)
			)
				throw new ArgumentException("Return type must be one of:\n" +
					"ReactiveToolResult, Task<ReactiveToolResult>,\n" +
					"ToolResult, Task<ToolResult>,\n" +
					"string, Task<string>,\n" +
					"void or Task.", nameof(method));

			var parameters = method.GetParameters();
			var parameterMappings = new Dictionary<JsonMemberAccessor, int>();
			var parameterMetaInfos = new Dictionary<string, JsonMemberAccessor>();

			int toolExecutionContextMapping = -1;
			int originalArgsMapping = -1;
			int sharedContextMapping = -1;
			int cancellationTokenMapping = -1;
			int preparedResultMapping = -1;
			var serviceMappings = new Dictionary<int, Type>();

			var requiredSchemaProperties = new JsonArray();
			var schemaProperties = new JsonObject();
			var argumentSchema = new JsonObject
			{
				["type"] = "object",
				["properties"] = schemaProperties,
				["additionalProperties"] = false
			};

			for (int paramIndex = 0; paramIndex < parameters.Length; paramIndex++)
			{
				var parameter = parameters[paramIndex];

				if (parameter.ParameterType == typeof(ToolExecutionContext))
				{
					if (toolExecutionContextMapping != -1)
						throw new ArgumentException("ToolExecutionContext can only be specified once.", nameof(method));
					toolExecutionContextMapping = paramIndex;
				}
				else if (parameter.ParameterType.IsAssignableTo(typeof(JsonNode)) && parameter.IsDefined(typeof(OriginalArgsAttribute)))
				{
					if (originalArgsMapping != -1)
						throw new ArgumentException("[OriginalArgs] JsonNode can only be specified once.", nameof(method));
					originalArgsMapping = paramIndex;
				}
				else if (parameter.IsDefined(typeof(SharedContextAttribute)))
				{
					if (sharedContextMapping != -1)
						throw new ArgumentException("[SharedContext] can only be specified once.", nameof(method));
					sharedContextMapping = paramIndex;
				}
				else if (parameter.ParameterType == typeof(CancellationToken))
				{
					if (cancellationTokenMapping != -1)
						throw new ArgumentException("CancellationToken can only be specified once.", nameof(method));
					cancellationTokenMapping = paramIndex;
				}
				else if (parameter.ParameterType == typeof(ReactiveToolResult))
				{
					if (preparedResultMapping != -1)
						throw new ArgumentException("ReactiveToolResult can only be specified once.", nameof(method));
					preparedResultMapping = paramIndex;
				}
				else if (parameter.IsDefined(typeof(InjectAttribute)))
				{
					serviceMappings[paramIndex] = parameter.ParameterType;
				}
				else
				{
					var parameterAccessor = new JsonMemberAccessor(parameter);
					parameterMetaInfos.Add(parameter.Name ??
						throw new ArgumentException("Method contains parameter without a name.", nameof(method)), parameterAccessor);
					if (!parameterAccessor.Include)
						continue;

					var parameterSchema = JsonSchemaGenerator.Generate(parameterAccessor);
					schemaProperties.Add(parameterAccessor.Name, parameterSchema);
					if (parameterAccessor.Required)
						requiredSchemaProperties.Add(parameterAccessor.Name);

					parameterMappings.Add(parameterAccessor, paramIndex);
				}
			}

			if (preparedResultMapping != -1 && !ret.IsAssignableTo(typeof(Task)))
				throw new ArgumentException("When using prepared result, the return type expected to be Task.", nameof(method));

			if (requiredSchemaProperties.Count > 0)
				argumentSchema["required"] = requiredSchemaProperties;

			async Task<ReactiveToolResult> Func(JsonNode? args, ToolExecutionContext context, CancellationToken cancellationToken)
			{
				var inParams = new object?[parameters.Length];
				var objArgs = args as JsonObject ?? [];
				var preparedResult = preparedResultMapping != -1 ? new ReactiveToolResult() : null;

				try
				{
					for (int i = 0; i < parameters.Length; i++)
						if (parameters[i].HasDefaultValue)
							inParams[i] = parameters[i].DefaultValue!;

					foreach (var (i, serviceType) in serviceMappings)
						inParams[i] = context.Chat.Services.GetService(serviceType);

					foreach (var (accessor, paramIndex) in parameterMappings)
					{
						if (objArgs.ContainsKey(accessor.Name))
						{
							var arg = objArgs[accessor.Name];
							var type = parameters[paramIndex].ParameterType;
							inParams[paramIndex] = ToolArgsJsonNodeConverter.Convert(arg, type, accessor.Name)!;
						}
						else
						{
							if (accessor.HasDefaultValue)
								inParams[paramIndex] = accessor.DefaultValue;
							else
								throw new ArgumentException($"Missing required parameter '{accessor.Name}'.", nameof(args));
						}
					}

					if (toolExecutionContextMapping != -1)
						inParams[toolExecutionContextMapping] = context;
					if (originalArgsMapping != -1)
						inParams[originalArgsMapping] = args;
					if (sharedContextMapping != -1)
						inParams[sharedContextMapping] = context.SharedContext;
					if (cancellationTokenMapping != -1)
						inParams[cancellationTokenMapping] = cancellationToken;
					if (preparedResultMapping != -1)
						inParams[preparedResultMapping] = preparedResult;
				}
				catch (Exception ex)
				{
					throw new ArgumentException($"Failed to deserialize arguments: {ex.Message}", ex);
				}

				var value = method.Invoke(target, inParams)!;

				if (preparedResult != null)
				{
					_ = Task.Run(async () =>
					{
						var task = (Task)value;
						try
						{
							await task;
							preparedResult.TryCompleteWithSuccess();
						}
						catch (Exception ex)
						{
							Log.Error(ex, "Unhandled exception during tool execution: {Error}", ex);
							if (string.IsNullOrEmpty(preparedResult.ResultContent))
								preparedResult.ResultContent = "An error occurred during tool execution: " + ex.Message;
							preparedResult.TryCompleteWithError();
						}
					}, CancellationToken.None);
					return preparedResult;
				}
				else
				{
					switch (value)
					{
						case Task<ReactiveToolResult> _1:
							return await _1;

						case ReactiveToolResult _2:
							return _2;

						case Task<ToolResult> _3:
							return ReactiveToolResult.CreateFromResult(await _3);

						case ToolResult _4:
							return ReactiveToolResult.CreateFromResult(_4);

						case Task<string> _5:
							return ReactiveToolResult.CreateSuccess(await _5);

						case string _6:
							return ReactiveToolResult.CreateSuccess(_6);

						case Task _7:
							await _7;
							return ReactiveToolResult.CreateSuccess("");

						default: // void or null
							return ReactiveToolResult.CreateSuccess("");
					}
				}
			}

			return (argumentSchema, Func, parameterMetaInfos);
		}
	}
}