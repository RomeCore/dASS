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
		/// The glob pattern string that matches the hook names (or keys) which this hook can be applied to.
		/// This is usually tool names or command names. Leave null to apply to all hook names.
		/// </summary>
		public string? HookName
		{
			get;
			set => SetProperty(ref field, value);
		}

		[System.Text.Json.Serialization.JsonIgnore]
		[LiteDB.BsonIgnore]
		[YamlDotNet.Serialization.YamlIgnore]
		public GlobTextPattern? HookNamePattern => string.IsNullOrEmpty(HookName) ? null :
			(field ??= GlobTextPattern.Parse(HookName, GlobOptions.IgnoreCase));

		/// <summary>
		/// The execution flow of the hook, which determines how the hook will be dispatched.
		/// Null means the caller's default execution flow.
		/// </summary>
		public HookExecutionFlow? ExecutionFlow
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

			if (Executor is null)
				errors.Add("Executor is required.");
		}
	}
}
