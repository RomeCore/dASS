using System.Reflection;
using System.Text.Json.Nodes;
using DocumentFormat.OpenXml.Bibliography;
using LLMDesktopAssistant.Tools;
using RCLargeLanguageModels.Json;
using RCLargeLanguageModels.Json.Schema;

namespace LLMDesktopAssistant.Agents.Tasks
{
	public class DelegateAgentTool : AgentTool
	{
		private static readonly JsonSchemaMethodGenerator _schemaGenerator = new();

		private readonly object? _methodTarget;
		private readonly MethodInfo _method;
		private readonly ParameterInfo[] _parameters;
		private readonly ImmutableDictionary<JsonMemberAccessor, int> _parameterMappings;
		private readonly int _ctMapping = -1;

		public override string Name { get; }

		public override string DisplayName { get; }

		public override string Description { get; }

		public override JsonObject ArgumentSchema { get; }

		public override ToolSource Source { get; }

		public DelegateAgentTool(string name, string? displayName, string description, ToolSource source, Delegate executor)
			: this(name, displayName, description, source, executor.Target, executor.Method)
		{
		}

		public DelegateAgentTool(string name, string? displayName, string description, ToolSource source, object? methodTarget, MethodInfo method)
		{
			ArgumentNullException.ThrowIfNull(method);
			if (methodTarget == null && !method.IsStatic)
				throw new ArgumentException("If method target is null, method must be static.", nameof(method));

			var ret = method.ReturnType;
			if (
				ret != typeof(AgentToolCallResult) &&
				ret != typeof(Task<AgentToolCallResult>)
				)
				throw new ArgumentException("Return type must be AgentToolCallResult or Task<AgentToolCallResult>.", nameof(method));

			var requiredSchemaProperties = new JsonArray();
			var schemaProperties = new JsonObject();
			var argumentSchema = new JsonObject
			{
				["type"] = "object",
				["properties"] = schemaProperties,
				["additionalProperties"] = false
			};

			var methodAccessor = new JsonMemberAccessor(method);
			var mappingsBuilder = ImmutableDictionary.CreateBuilder<JsonMemberAccessor, int>();

			var parameters = method.GetParameters();
			for (int paramIndex = 0; paramIndex < parameters.Length; paramIndex++)
			{
				var parameter = parameters[paramIndex];

				if (parameter.ParameterType == typeof(CancellationToken))
				{
					if (_ctMapping != -1)
						throw new ArgumentException("Multiple parameters of type CancellationToken are not supported.", nameof(method));
					_ctMapping = parameter.Position;
				}
				else
				{
					var parameterAccessor = new JsonMemberAccessor(parameter);
					if (!parameterAccessor.Include)
						continue;

					var parameterSchema = JsonSchemaGenerator.Generate(parameterAccessor);
					schemaProperties.Add(parameterAccessor.Name, parameterSchema);
					if (parameterAccessor.Required)
						requiredSchemaProperties.Add(parameterAccessor.Name);

					mappingsBuilder.Add(parameterAccessor, parameter.Position);
				}
			}

			if (requiredSchemaProperties.Count > 0)
				argumentSchema["required"] = requiredSchemaProperties;

			_methodTarget = methodTarget;
			_method = method;
			_parameters = parameters;
			_parameterMappings = mappingsBuilder.ToImmutable();

			Name = name;
			DisplayName = displayName ?? name;
			Description = description;
			ArgumentSchema = argumentSchema;
			Source = source;
		}

		public override async Task<AgentToolCallResult> ExecuteAsync(JsonNode? arguments, object? sharedContext, CancellationToken cancellationToken = default)
		{
			var objArgs = arguments as JsonObject ?? [];
			var inParams = new object?[_parameters.Length];

			try
			{
				for (int i = 0; i < _parameters.Length; i++)
					if (_parameters[i].HasDefaultValue)
						inParams[i] = _parameters[i].DefaultValue!;

				foreach (var (accessor, paramIndex) in _parameterMappings)
				{
					if (objArgs.ContainsKey(accessor.Name))
					{
						var arg = objArgs[accessor.Name];
						var type = _parameters[paramIndex].ParameterType;
						inParams[paramIndex] = ToolArgsJsonNodeConverter.Convert(arg, type, accessor.Name)!;
					}
					else
					{
						if (accessor.HasDefaultValue)
							inParams[paramIndex] = accessor.DefaultValue;
						else
							throw new ArgumentException($"Missing required parameter '{accessor.Name}'.", nameof(arguments));
					}
				}

				if (_ctMapping != -1)
					inParams[_ctMapping] = cancellationToken;
			}
			catch (Exception ex)
			{
				return new AgentToolCallResult
				{
					Success = false,
					Content = $"Failed to deserialize arguments: {ex.Message}"
				};
			}

			try
			{
				var result = _method.Invoke(_methodTarget, inParams);
				return result switch
				{
					AgentToolCallResult callResult => callResult,
					Task<AgentToolCallResult> task => await task,
					_ => new AgentToolCallResult
					{
						Success = true,
						Content = "Tool was executed successfully."
					}
				};
			}
			catch (Exception ex)
			{
				return new AgentToolCallResult
				{
					Success = false,
					Content = $"Error executing tool: {ex.Message}"
				};
			}
		}
	}
}
