using System.Collections.Immutable;
using System.Reflection;
using System.Text.Json.Nodes;
using LLMDesktopAssistant.Services;
using LLMDesktopAssistant.Tools.Execution.Domain;
using LLMDesktopAssistant.Tools.Specifiers;
using RCLargeLanguageModels.Json;
using RCLargeLanguageModels.Json.Schema;
using RCLargeLanguageModels.Messages.Attachments;
using RCLargeLanguageModels.Tools;
using Serilog;
using IToolCall = LLMDesktopAssistant.Tools.Execution.Domain.IToolCall;
using DomainPreviewResult = LLMDesktopAssistant.Tools.Execution.Domain.PreviewToolExecutionResult;

namespace LLMDesktopAssistant.Tools.Execution.Executors
{
	/// <summary>
	/// Builds the six carried executors from ordinary methods. This is the single authoring binder: the
	/// old <c>ToolExecutorCreator</c>, <c>PreviewToolExecutorCreator</c>,
	/// <c>StreamingToolArgumentAnalyzerCreator</c> and <c>SpecifierToolAnalyzerCreator</c> families all
	/// collapse onto it.
	/// </summary>
	/// <remarks>
	/// The argument schema and the parameter metadata are built once from the main method and reused by
	/// the other capabilities, so a preview or specifier method sees the same defaults and conversions
	/// as the main one. Special parameters are recognised by type and attribute:
	/// <list type="bullet">
	/// <item><description><see cref="IToolExecutionContext"/> — the invocation context;</description></item>
	/// <item><description><see cref="IToolCall"/> — the call, as a shorthand for <c>ctx.Call</c>;</description></item>
	/// <item><description><c>[OriginalArgs] JsonNode</c> — the raw argument node;</description></item>
	/// <item><description><c>[SharedContext]</c> — the tool's own channel between stages;</description></item>
	/// <item><description><see cref="CancellationToken"/> — where the capability is asynchronous;</description></item>
	/// <item><description><see cref="Specifier"/> — only in a specifier analyzer;</description></item>
	/// <item><description><c>[Inject]</c> — resolved from <see cref="IToolExecutionContext.Services"/>;</description></item>
	/// <item><description>everything else — a JSON argument.</description></item>
	/// </list>
	/// </remarks>
	public static class ToolMethodBinder
	{
		/// <summary>
		/// Describes the main method: builds its argument schema and the parameter metadata the other
		/// capabilities reuse.
		/// </summary>
		/// <param name="executor">The main method delegate.</param>
		/// <returns>The schema and the argument metadata.</returns>
		public static (JsonObject Schema, IReadOnlyDictionary<string, JsonMemberAccessor> Parameters) Describe(Delegate executor)
		{
			ArgumentNullException.ThrowIfNull(executor);
			return Describe(executor.Target, executor.Method);
		}

		/// <summary>
		/// Describes the main method: builds its argument schema and the parameter metadata the other
		/// capabilities reuse.
		/// </summary>
		/// <param name="target">The instance the method is invoked on, or <see langword="null"/> for a static method.</param>
		/// <param name="method">The main tool method.</param>
		/// <returns>The schema and the argument metadata.</returns>
		public static (JsonObject Schema, IReadOnlyDictionary<string, JsonMemberAccessor> Parameters) Describe(
			object? target, MethodInfo method)
		{
			var binder = new Binder(target, method, Capability.Main, null);
			return binder.Describe();
		}

		/// <summary>
		/// Builds the JSON argument schema of the main method.
		/// </summary>
		/// <param name="executor">The main method delegate.</param>
		/// <returns>The argument schema.</returns>
		public static JsonObject BuildArgumentSchema(Delegate executor) => Describe(executor).Schema;

		/// <summary>
		/// Binds the main executor.
		/// </summary>
		/// <param name="target">The instance the method is invoked on, or <see langword="null"/> for a static method.</param>
		/// <param name="method">The main method.</param>
		/// <returns>The main executor.</returns>
		public static IToolMainExecutor BindMain(object? target, MethodInfo method)
		{
			var binder = new Binder(target, method, Capability.Main, null);
			return new DelegateMainExecutor(async (args, ctx, ct) =>
			{
				var value = binder.Invoke(args, ctx, ct, null);
				return await MapMain(value, ctx);
			});
		}

		/// <summary>
		/// Binds the preview executor.
		/// </summary>
		/// <param name="target">The instance the method is invoked on, or <see langword="null"/> for a static method.</param>
		/// <param name="method">The preview method.</param>
		/// <param name="mainParameters">The main method's metadata, when available, so defaults stay shared.</param>
		/// <returns>The preview executor.</returns>
		public static IToolPreviewExecutor BindPreview(object? target, MethodInfo method,
			IReadOnlyDictionary<string, JsonMemberAccessor>? mainParameters = null)
		{
			var binder = new Binder(target, method, Capability.Preview, mainParameters);
			return new PreviewAdapter(async (args, ctx, ct) =>
			{
				var value = binder.Invoke(args, ctx, ct, null);
				return await MapPreview(value);
			});
		}

		/// <summary>
		/// Binds the streaming analyzer.
		/// </summary>
		/// <param name="target">The instance the method is invoked on, or <see langword="null"/> for a static method.</param>
		/// <param name="method">The streaming analyzer method.</param>
		/// <param name="mainParameters">The main method's metadata, when available.</param>
		/// <returns>The streaming analyzer.</returns>
		public static IToolStreamingAnalyzer BindStreaming(object? target, MethodInfo method,
			IReadOnlyDictionary<string, JsonMemberAccessor>? mainParameters = null)
		{
			var binder = new Binder(target, method, Capability.Streaming, mainParameters);
			return new StreamingAdapter((args, ctx) =>
			{
				try
				{
					return MapStreaming(binder.Invoke(args, ctx, default, null));
				}
				catch (Exception ex)
				{
					// Partial arguments are expected to be unparseable or incomplete; a streaming analyzer
					// must never break the run because of one bad update.
					Log.Debug(ex, "Streaming analysis failed for tool '{ToolName}'.", ctx.Call.ToolName);
					return StreamingToolAnalysisResult.Continue;
				}
			});
		}

		/// <summary>
		/// Binds the specifier analyzer.
		/// </summary>
		/// <param name="target">The instance the method is invoked on, or <see langword="null"/> for a static method.</param>
		/// <param name="method">The specifier analyzer method.</param>
		/// <param name="mainParameters">The main method's metadata, when available.</param>
		/// <returns>The specifier analyzer.</returns>
		public static IToolSpecifierAnalyzer BindSpecifier(object? target, MethodInfo method,
			IReadOnlyDictionary<string, JsonMemberAccessor>? mainParameters = null)
		{
			var binder = new Binder(target, method, Capability.Specifier, mainParameters);
			return new SpecifierAdapter((specifier, args, ctx) =>
			{
				var value = binder.Invoke(args, ctx, default, specifier);
				return value switch
				{
					SpecifierMatchResult result => result,
					null => SpecifierMatchResult.NoMatch,
					_ => throw new ArgumentException(
						$"Specifier analyzer returned an unsupported type: {value.GetType()}.")
				};
			});
		}

		/// <summary>
		/// Binds the synchronization analyzer.
		/// </summary>
		/// <param name="target">The instance the method is invoked on, or <see langword="null"/> for a static method.</param>
		/// <param name="method">The synchronization analyzer method.</param>
		/// <param name="mainParameters">The main method's metadata, when available.</param>
		/// <returns>The synchronization analyzer.</returns>
		public static IToolSynchronizationAnalyzer BindSyncAnalyzer(object? target, MethodInfo method,
			IReadOnlyDictionary<string, JsonMemberAccessor>? mainParameters = null)
		{
			var binder = new Binder(target, method, Capability.Sync, mainParameters);
			return new SyncAdapter((args, ctx) =>
			{
				var value = binder.Invoke(args, ctx, default, null);
				return value switch
				{
					IEnumerable<ToolSyncKey> keys => [.. keys],
					null => [],
					_ => throw new ArgumentException(
						$"Synchronization analyzer returned an unsupported type: {value.GetType()}.")
				};
			});
		}

		/// <summary>
		/// Binds the approval provider.
		/// </summary>
		/// <param name="target">The instance the method is invoked on, or <see langword="null"/> for a static method.</param>
		/// <param name="method">The approval method.</param>
		/// <param name="requestedDataSchema">The schema of the data the approval surface may request, or <see langword="null"/>.</param>
		/// <param name="mainParameters">The main method's metadata, when available.</param>
		/// <returns>The approval provider.</returns>
		public static IToolApprovalProvider BindApproval(object? target, MethodInfo method,
			JsonObject? requestedDataSchema = null,
			IReadOnlyDictionary<string, JsonMemberAccessor>? mainParameters = null)
		{
			var binder = new Binder(target, method, Capability.Approval, mainParameters);
			return new ApprovalAdapter(requestedDataSchema, async (args, ctx, ct) =>
			{
				var value = binder.Invoke(args, ctx, ct, null);
				return value switch
				{
					Task<ToolApprovalResult> task => await task,
					ToolApprovalResult result => result,
					_ => throw new ArgumentException(
						$"Approval provider returned an unsupported type: {value?.GetType().ToString() ?? "null"}.")
				};
			});
		}

		private static async Task<ToolExecutionResult> MapMain(object? value, IToolExecutionContext ctx)
		{
			switch (value)
			{
				case Task<ToolExecutionResult> task:
					return await task;

				case ToolExecutionResult result:
					return result;

				case Task<ToolResult> task:
					return MapToolResult(await task, ctx);

				case ToolResult result:
					return MapToolResult(result, ctx);

				case Task<string> task:
					ctx.Call.ResultContent = await task;
					return ToolExecutionResult.Succeeded;

				case string content:
					ctx.Call.ResultContent = content;
					return ToolExecutionResult.Succeeded;

				case Task task:
					await task;
					return ToolExecutionResult.Succeeded;

				case null:
					return ToolExecutionResult.Succeeded;

				default:
					throw new ArgumentException($"Unsupported tool return type: {value.GetType()}.");
			}
		}

		private static ToolExecutionResult MapToolResult(ToolResult result, IToolExecutionContext ctx)
		{
			ctx.Call.ResultContent = result.Content;
			foreach (var attachment in result.Attachments)
				ProjectAttachment(ctx.Call, attachment);

			return result.Status == ToolResultStatus.Success
				? ToolExecutionResult.Succeeded
				: ToolExecutionResult.Failed;
		}

		/// <summary>
		/// Projects a native RCLLM attachment into the neutral call. The neutral kind is what the call
		/// turns back into the invoking world's native form.
		/// </summary>
		/// <param name="call">The call to add the attachment to.</param>
		/// <param name="attachment">The native attachment.</param>
		private static void ProjectAttachment(IToolCall call, IAttachment attachment)
		{
			switch (attachment)
			{
				case IImageAttachment image:
					call.AddAttachment(new ToolAttachment { Kind = ToolAttachmentKind.Image, Url = image.Url });
					break;
				case IAudioAttachment audio:
					call.AddAttachment(new ToolAttachment { Kind = ToolAttachmentKind.Audio, Url = audio.Url });
					break;
				case IVideoAttachment video:
					call.AddAttachment(new ToolAttachment { Kind = ToolAttachmentKind.Video, Url = video.Url });
					break;
			}
		}

		private static async Task<DomainPreviewResult> MapPreview(object? value)
		{
			switch (value)
			{
				case Task<DomainPreviewResult> task:
					return await task;

				case DomainPreviewResult result:
					return result;

				case Task task:
					await task;
					return DomainPreviewResult.Continue;

				case null:
					return DomainPreviewResult.Continue;

				default:
					throw new ArgumentException($"Unsupported preview return type: {value.GetType()}.");
			}
		}

		private static StreamingToolAnalysisResult MapStreaming(object? value) => value switch
		{
			StreamingToolAnalysisResult result => result,
			null => StreamingToolAnalysisResult.Continue,
			_ => throw new ArgumentException($"Unsupported streaming return type: {value.GetType()}.")
		};

		/// <summary>
		/// The capability whose parameter slots and return shape the binder validates.
		/// </summary>
		[Flags]
		private enum Capability
		{
			Main = 1 << 0,
			Preview = 1 << 1,
			Streaming = 1 << 2,
			Specifier = 1 << 3,
			Sync = 1 << 4,
			Approval = 1 << 5
		}

		private sealed class PreviewAdapter(
			Func<JsonNode?, IToolExecutionContext, CancellationToken, Task<DomainPreviewResult>> preview)
			: IToolPreviewExecutor
		{
			public Task<DomainPreviewResult> PreviewAsync(JsonNode? args, IToolExecutionContext ctx,
				CancellationToken ct) => preview(args, ctx, ct);
		}

		private sealed class StreamingAdapter(
			Func<JsonNode?, IToolExecutionContext, StreamingToolAnalysisResult> analyze) : IToolStreamingAnalyzer
		{
			public StreamingToolAnalysisResult AnalyzeArgs(JsonNode? partialArgs, IToolExecutionContext ctx) =>
				analyze(partialArgs, ctx);
		}

		private sealed class SpecifierAdapter(
			Func<Specifier, JsonNode?, IToolExecutionContext, SpecifierMatchResult> analyze) : IToolSpecifierAnalyzer
		{
			public SpecifierMatchResult Analyze(Specifier specifier, JsonNode? args, IToolExecutionContext ctx) =>
				analyze(specifier, args, ctx);
		}

		private sealed class SyncAdapter(
			Func<JsonNode?, IToolExecutionContext, IReadOnlyList<ToolSyncKey>> analyze) : IToolSynchronizationAnalyzer
		{
			public IReadOnlyList<ToolSyncKey> AnalyzeSyncKeys(JsonNode? args, IToolExecutionContext ctx) =>
				analyze(args, ctx);
		}

		private sealed class ApprovalAdapter(
			JsonObject? requestedDataSchema,
			Func<JsonNode?, IToolExecutionContext, CancellationToken, Task<ToolApprovalResult>> awaitDecision)
			: IToolApprovalProvider
		{
			public JsonObject? RequestedDataSchema => requestedDataSchema;

			public Task<ToolApprovalResult> AwaitDecisionAsync(IToolExecutionContext ctx, CancellationToken ct) =>
				awaitDecision(ctx.Arguments.Effective, ctx, ct);
		}

		/// <summary>
		/// The parameter plan of one method: which slots it declares and how its JSON arguments are
		/// converted. Built once at descriptor-construction time.
		/// </summary>
		private sealed class Binder
		{
			private readonly object? _target;
			private readonly MethodInfo _method;
			private readonly ParameterInfo[] _parameters;
			private readonly Capability _capability;
			private readonly bool _requireArguments;

			private int _contextIndex = -1;
			private int _callIndex = -1;
			private int _originalArgsIndex = -1;
			private int _sharedContextIndex = -1;
			private int _cancellationTokenIndex = -1;
			private int _specifierIndex = -1;
			private readonly Dictionary<int, Type> _injections = [];
			private readonly Dictionary<JsonMemberAccessor, int> _arguments = [];
			private readonly Dictionary<string, JsonMemberAccessor> _metadata = [];

			public Binder(object? target, MethodInfo method, Capability capability,
				IReadOnlyDictionary<string, JsonMemberAccessor>? mainParameters)
			{
				ArgumentNullException.ThrowIfNull(method);
				if (target == null && !method.IsStatic)
					throw new ArgumentException("If the method target is null, the method must be static.", nameof(method));

				_target = target;
				_method = method;
				_capability = capability;
				_parameters = method.GetParameters();
				_requireArguments = capability != Capability.Streaming;
				ValidateReturnType(method.ReturnType);

				for (var index = 0; index < _parameters.Length; index++)
				{
					var parameter = _parameters[index];

					if (parameter.ParameterType == typeof(IToolExecutionContext))
					{
						RequireOnce(ref _contextIndex, index, $"{nameof(IToolExecutionContext)} can only be specified once.");
					}
					else if (parameter.ParameterType == typeof(IToolCall))
					{
						RequireOnce(ref _callIndex, index, $"{nameof(IToolCall)} can only be specified once.");
					}
					else if (parameter.ParameterType.IsAssignableTo(typeof(JsonNode)) &&
						parameter.IsDefined(typeof(OriginalArgsAttribute)))
					{
						RequireOnce(ref _originalArgsIndex, index, "[OriginalArgs] JsonNode can only be specified once.");
					}
					else if (parameter.IsDefined(typeof(SharedContextAttribute)))
					{
						RequireOnce(ref _sharedContextIndex, index, "[SharedContext] can only be specified once.");
					}
					else if (parameter.ParameterType == typeof(CancellationToken))
					{
						if (capability is Capability.Streaming or Capability.Specifier or Capability.Sync)
							throw new ArgumentException(
								$"A {capability} capability is synchronous and cannot take a CancellationToken.",
								nameof(method));
						RequireOnce(ref _cancellationTokenIndex, index,
							$"{nameof(CancellationToken)} can only be specified once.");
					}
					else if (parameter.ParameterType == typeof(Specifier))
					{
						if (capability != Capability.Specifier)
							throw new ArgumentException(
								$"{nameof(Specifier)} is only allowed in a specifier analyzer.", nameof(method));
						RequireOnce(ref _specifierIndex, index, $"{nameof(Specifier)} can only be specified once.");
					}
					else if (parameter.IsDefined(typeof(InjectAttribute)))
					{
						_injections[index] = parameter.ParameterType;
					}
					else
					{
						var name = parameter.Name
							?? throw new ArgumentException("The method contains a parameter without a name.", nameof(method));
						var accessor = mainParameters is not null && mainParameters.TryGetValue(name, out var shared)
							? shared
							: new JsonMemberAccessor(parameter);

						_metadata[name] = accessor;
						if (!accessor.Include)
							continue;

						_arguments[accessor] = index;
					}
				}
			}

			public (JsonObject Schema, IReadOnlyDictionary<string, JsonMemberAccessor> Parameters) Describe()
			{
				var properties = new JsonObject();
				var required = new JsonArray();

				foreach (var accessor in _metadata.Values)
				{
					if (!accessor.Include)
						continue;

					properties[accessor.Name] = JsonSchemaGenerator.Generate(accessor);
					if (accessor.Required)
						required.Add(accessor.Name);
				}

				var schema = new JsonObject
				{
					["type"] = "object",
					["properties"] = properties,
					["additionalProperties"] = false
				};

				if (required.Count > 0)
					schema["required"] = required;

				return (schema, _metadata);
			}

			/// <summary>
			/// Binds the parameters of one invocation and invokes the method.
			/// </summary>
			/// <param name="args">The effective arguments.</param>
			/// <param name="ctx">The invocation context.</param>
			/// <param name="ct">The cancellation token, where the capability is asynchronous.</param>
			/// <param name="specifier">The specifier being analysed, for the specifier capability.</param>
			/// <returns>The raw return value of the method.</returns>
			public object? Invoke(JsonNode? args, IToolExecutionContext ctx, CancellationToken ct, Specifier? specifier)
			{
				var inParams = new object?[_parameters.Length];
				var argumentObject = args as JsonObject ?? [];

				try
				{
					for (var index = 0; index < _parameters.Length; index++)
						if (_parameters[index].HasDefaultValue)
							inParams[index] = _parameters[index].DefaultValue;

					foreach (var (index, serviceType) in _injections)
						inParams[index] = ctx.Services.GetService(serviceType);

					foreach (var (accessor, index) in _arguments)
					{
						if (argumentObject.ContainsKey(accessor.Name))
						{
							inParams[index] = ToolArgsJsonNodeConverter.Convert(argumentObject[accessor.Name],
								_parameters[index].ParameterType, accessor.Name);
						}
						else if (accessor.HasDefaultValue)
						{
							inParams[index] = accessor.DefaultValue;
						}
						else if (_requireArguments)
						{
							throw new ArgumentException($"Missing required parameter '{accessor.Name}'.");
						}
					}

					if (_contextIndex != -1)
						inParams[_contextIndex] = ctx;
					if (_callIndex != -1)
						inParams[_callIndex] = ctx.Call;
					if (_originalArgsIndex != -1)
						inParams[_originalArgsIndex] = args;
					if (_sharedContextIndex != -1)
						inParams[_sharedContextIndex] = ctx.SharedContext;
					if (_cancellationTokenIndex != -1)
						inParams[_cancellationTokenIndex] = ct;
					if (_specifierIndex != -1)
						inParams[_specifierIndex] = specifier;
				}
				catch (Exception ex)
				{
					throw new ArgumentException($"Failed to deserialize tool arguments: {ex.Message}", ex);
				}

				var value = _method.Invoke(_target, inParams);

				if (_sharedContextIndex != -1)
					ctx.SharedContext = inParams[_sharedContextIndex];

				return value;
			}

			private void RequireOnce(ref int slot, int index, string message)
			{
				if (slot != -1)
					throw new ArgumentException(message);
				slot = index;
			}

			private void ValidateReturnType(Type returnType)
			{
				var allowed = _capability switch
				{
					Capability.Main => returnType == typeof(ToolExecutionResult) ||
						returnType == typeof(Task<ToolExecutionResult>) ||
						returnType == typeof(ToolResult) ||
						returnType == typeof(Task<ToolResult>) ||
						returnType == typeof(string) ||
						returnType == typeof(Task<string>) ||
						returnType == typeof(void) ||
						returnType == typeof(Task),

					Capability.Preview => returnType == typeof(DomainPreviewResult) ||
						returnType == typeof(Task<DomainPreviewResult>) ||
						returnType == typeof(void) ||
						returnType == typeof(Task),

					Capability.Streaming => returnType == typeof(StreamingToolAnalysisResult) ||
						returnType == typeof(void),

					Capability.Specifier => returnType == typeof(SpecifierMatchResult) ||
						returnType == typeof(void),

					Capability.Sync => returnType == typeof(IReadOnlyList<ToolSyncKey>) ||
						returnType == typeof(IEnumerable<ToolSyncKey>) ||
						returnType == typeof(ToolSyncKey[]) ||
						returnType == typeof(ImmutableList<ToolSyncKey>) ||
						returnType == typeof(void),

					Capability.Approval => returnType == typeof(ToolApprovalResult) ||
						returnType == typeof(Task<ToolApprovalResult>),

					_ => false
				};

				if (!allowed)
					throw new ArgumentException(
						$"The return type '{returnType}' is not supported by the {_capability} capability.",
						_method.Name);
			}
		}
	}
}
