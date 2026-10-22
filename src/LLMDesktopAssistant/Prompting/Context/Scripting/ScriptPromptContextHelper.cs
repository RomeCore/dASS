using Serilog;

namespace LLMDesktopAssistant.Prompting.Context.Scripting
{
	/// <summary>
	/// Shared rules of the scriptable prompt contexts: the XML wrapper, the discriminator and the guarded
	/// execution of a script (timeout, failure isolation).
	/// </summary>
	public static class ScriptPromptContextHelper
	{
		/// <summary>The timeout applied when the addon does not declare one.</summary>
		public const int DefaultTimeoutMs = 2000;

		/// <summary>The hard ceiling of the per-script timeout.</summary>
		public const int MaxTimeoutMs = 10000;

		/// <summary>
		/// Gets the discriminator of a scriptable context: the <c>script:</c> namespace plus the addon name,
		/// so a script can never shadow a native section.
		/// </summary>
		public static string GetDiscriminator(string name) => $"script:{name}";

		/// <summary>
		/// Gets the XML element name used for the wrapper. An element name cannot start with a digit,
		/// so such addon names are prefixed with an underscore.
		/// </summary>
		public static string GetTagName(string name)
			=> name.Length > 0 && char.IsAsciiDigit(name[0]) ? $"_{name}" : name;

		/// <summary>
		/// Wraps the script output into the XML tag the model sees.
		/// </summary>
		public static string Wrap(string name, string content)
		{
			var tag = GetTagName(name);
			return $"<{tag}>\n{content}\n</{tag}>";
		}

		/// <summary>
		/// Runs a context body under the configured timeout and turns any failure into "no context":
		/// a script must never break the prompt build or leak its errors into the prompt.
		/// </summary>
		public static async Task<string?> RunAsync(IScriptablePromptContextEngine engine, IServiceProvider services,
			string body, string? previous, int timeoutMs, CancellationToken cancellationToken)
		{
			using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
			timeout.CancelAfter(Math.Clamp(timeoutMs, 1, MaxTimeoutMs));

			try
			{
				return await engine.RunAsync(services, body, previous, timeout.Token);
			}
			catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
			{
				Log.Warning("Prompt context script timed out after {Timeout} ms ({Language}).",
					timeoutMs, engine.Language);
				return null;
			}
			catch (Exception ex)
			{
				Log.Warning(ex, "Prompt context script failed ({Language}): {Message}",
					engine.Language, ex.Message);
				return null;
			}
		}
	}
}
