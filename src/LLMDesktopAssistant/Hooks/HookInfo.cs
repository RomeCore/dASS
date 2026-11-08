using LLMDesktopAssistant.Scripting;
using LLMDesktopAssistant.Utils;
using LLMDesktopAssistant.Utils.Patterns;

namespace LLMDesktopAssistant.Hooks
{
	public class HookInfo : ScriptableAddonBase<HookInfo, HookChange>
	{
		/// <summary>
		/// The glob pattern string that matches the hook types which this hook can be applied to.
		/// </summary>
		public string HookType
		{
			get;
			set => SetProperty(ref field, value);
		} = string.Empty;

		/// <summary>
		/// The parsed glob pattern that describes the hook types which this hook can be applied to.
		/// This is a cached value and should not be set manually.
		/// </summary>
		[System.Text.Json.Serialization.JsonIgnore]
		[LiteDB.BsonIgnore]
		[YamlDotNet.Serialization.YamlIgnore]
		public GlobTextPattern HookTypePattern => field ??= GlobTextPattern.Parse(HookType, GlobOptions.IgnoreCase);

		/// <summary>
		/// The glob pattern string that matches the target of an event - usually a tool name or a command name -
		/// which this hook can be applied to. Leave null to apply to every target.
		/// </summary>
		public string? HookTarget
		{
			get;
			set => SetProperty(ref field, value);
		}

		[System.Text.Json.Serialization.JsonIgnore]
		[LiteDB.BsonIgnore]
		[YamlDotNet.Serialization.YamlIgnore]
		public GlobTextPattern? HookTargetPattern => string.IsNullOrEmpty(HookTarget) ? null :
			(field ??= GlobTextPattern.Parse(HookTarget, GlobOptions.IgnoreCase));

		/// <summary>
		/// The execution flow of the hook, which determines how the hook will be dispatched.
		/// Null means the caller's default execution flow.
		/// </summary>
		public HookExecutionFlow? ExecutionFlow
		{
			get;
			set => SetProperty(ref field, value);
		}

		/// <summary>
		/// The time the dispatcher gives this hook to run, in milliseconds. Null means the dispatcher's
		/// default for an awaited hook and no bound at all for a fire-and-forget one. The dispatcher caps a
		/// declared value at its own ceiling, except for fire-and-forget hooks, which nobody waits for.
		/// </summary>
		public int? TimeoutMs
		{
			get;
			set => SetProperty(ref field, value);
		}

		[System.Text.Json.Serialization.JsonIgnore]
		[LiteDB.BsonIgnore]
		[YamlDotNet.Serialization.YamlIgnore]
		public IHookExecutor Executor
		{
			get;
			set => SetProperty(ref field, value);
		} = null!;

		protected override void ValidatePropertiesCore(AppendOnlyList<string> errors)
		{
			base.ValidatePropertiesCore(errors);

			if (string.IsNullOrEmpty(HookType))
				errors.Add("HookType is required.");
			if (TimeoutMs is <= 0)
				errors.Add("The timeout must be a positive number of milliseconds.");
			if (Executor is null)
				errors.Add("Executor is required.");
		}
	}
}
