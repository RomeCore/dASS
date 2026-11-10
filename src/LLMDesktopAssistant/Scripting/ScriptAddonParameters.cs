using LLMDesktopAssistant.Addons;
using LLMDesktopAssistant.StructuredValues;
using LLMDesktopAssistant.StructuredValues.Parameterization;

namespace LLMDesktopAssistant.Scripting
{
	/// <summary>
	/// Resolves the parameter values an addon declares with <c>params-schema</c> into the shape a script reads.
	/// </summary>
	public static class ScriptAddonParameters
	{
		/// <summary>
		/// Returns the effective parameter value - the stored one with the schema defaults applied - or
		/// <see langword="null"/> when the addon declares no schema.
		/// </summary>
		/// <remarks>
		/// The fixed value is written back to the change, so a card opened later shows the values the script
		/// actually ran with, and a script that mutates nothing still sees every declared default.
		/// </remarks>
		/// <param name="schema">The addon's parameter schema, or <see langword="null"/>.</param>
		/// <param name="change">The addon's change entry, or <see langword="null"/> when nothing was overridden.</param>
		public static INodeValue? Resolve(ParameterSchema? schema, AddonChangeBase? change)
		{
			if (schema is null)
				return null;

			var value = schema.Root.CreateOrFixValue(change?.Parameters, []);
			if (change is not null)
				change.Parameters = value;

			return value;
		}
	}
}
