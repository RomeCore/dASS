using System.Text.RegularExpressions;

namespace LLMDesktopAssistant.Utils
{
	public static partial class KebabEnumNames<TEnum>
		where TEnum : struct, Enum
	{
		[GeneratedRegex(@"(?<=[a-z0-9])(?=[A-Z])|(?<=[A-Z])(?=[A-Z][a-z])", RegexOptions.Compiled)]
		private static partial Regex GetKebabCaseRegex();
		private static readonly Regex _kebabCaseRegex = GetKebabCaseRegex();

		public static readonly ImmutableDictionary<TEnum, string> Names = Enum.GetValues<TEnum>()
			.Select(value => (value, KebabEnumNames<TEnum>.ToKebabCase(value.ToString())))
			.ToImmutableDictionary(k => k.value, v => v.Item2);

		private static string ToKebabCase(string name)
		{
			return _kebabCaseRegex.Replace(name, "-").ToLowerInvariant();
		}

		public static TEnum Deserialize(string str, TEnum defaultValue = default)
		{
			var normalized = str.Trim().ToLowerInvariant().Replace('_', '-');

			foreach (var (level, name) in Names)
				if (name == normalized)
					return level;

			if (Enum.TryParse<TEnum>(str, ignoreCase: true, out var parsed))
				return parsed;

			return defaultValue;
		}

		public static TEnum DeserializeFlags(params string[]? strs)
		{
			ulong result = 0;
			foreach (var str in strs ?? [])
				result |= Convert.ToUInt64(Deserialize(str));
			return (TEnum)Enum.ToObject(typeof(TEnum), result);
		}

		public static string? Serialize(TEnum value)
		{
			foreach (var (itemValue, name) in Names)
				if (Equals(itemValue, value))
					return name;
			return null;
		}

		public static string[]? SerializeFlags(TEnum value)
		{
			ulong ivalue = Convert.ToUInt64(value);
			if (ivalue == 0)
				return null;

			var result = new List<string>();
			foreach (var (flag, name) in Names)
				if (value.HasFlag(flag))
					result.Add(name);
			return [.. result];
		}
	}
}