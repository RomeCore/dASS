using System.Text.Json.Nodes;
using LLMDesktopAssistant.Tools.Execution.Domain;
using LLMDesktopAssistant.Utils;

namespace LLMDesktopAssistant.Tools.Execution.Support
{
	/// <summary>
	/// The effective-arguments accessor: <c>ArgumentsOverride ?? parsed(call.Arguments)</c>, cached by
	/// the override reference and the raw argument string, so a stale parse tree is unrepresentable.
	/// </summary>
	/// <remarks>
	/// The cache key is a pair, not a single timestamp: the override is compared by reference (it is a
	/// node handed to the context once) and the raw string by value. Any change to either input
	/// produces a different key and a fresh parse; no invalidation call is needed anywhere.
	/// The accessor is not a snapshot - it always reflects the latest input.
	/// </remarks>
	/// <param name="call">The call whose raw arguments are the persisted channel.</param>
	/// <param name="overrideGetter">Reads the ephemeral override from the context.</param>
	public sealed class ToolArgumentsAccessor(IToolCall call, Func<JsonNode?> overrideGetter) : IToolArgumentsAccessor
	{
		private readonly Lock _lock = new();

		private bool _hasCache;
		private JsonNode? _cachedOverride;
		private string? _cachedRaw;
		private JsonNode? _cachedValue;

		/// <summary>
		/// The effective arguments: the override when it is set, otherwise the tolerant-parsed raw
		/// arguments. Recomputed when either input changes.
		/// </summary>
		public JsonNode? Effective
		{
			get
			{
				var overridden = overrideGetter();
				var raw = call.Arguments;

				lock (_lock)
				{
					if (_hasCache &&
						ReferenceEquals(_cachedOverride, overridden) &&
						string.Equals(_cachedRaw, raw, StringComparison.Ordinal))
					{
						return _cachedValue;
					}

					_cachedOverride = overridden;
					_cachedRaw = raw;
					_cachedValue = overridden ?? Parse(raw);
					_hasCache = true;
					return _cachedValue;
				}
			}
		}

		/// <summary>
		/// Parses raw arguments with the tolerant parser. Empty arguments produce
		/// <see langword="null"/> rather than an empty object, so a tool can distinguish "no arguments"
		/// from "an empty argument set".
		/// </summary>
		/// <param name="raw">The raw argument JSON, possibly partial.</param>
		/// <returns>The parsed node, or <see langword="null"/>.</returns>
		private static JsonNode? Parse(string? raw) =>
			string.IsNullOrWhiteSpace(raw) ? null : TolerantJsonParser.Parse(raw);
	}
}
