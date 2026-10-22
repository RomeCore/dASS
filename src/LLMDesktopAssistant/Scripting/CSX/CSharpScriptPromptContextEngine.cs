using LLMDesktopAssistant.Prompting.Context.Scripting;
using LLMDesktopAssistant.Scripting;
using LLMDesktopAssistant.Services;

namespace LLMDesktopAssistant.Scripting.CSX
{
	/// <summary>
	/// C# script implementation of <see cref="IScriptablePromptContextEngine"/>.
	/// The body is compiled with Roslyn; the script's return value is the context text and the previous
	/// rendered text is exposed as the <c>Previous</c> global.
	/// </summary>
	[Service(typeof(IScriptablePromptContextEngine))]
	public class CSharpScriptPromptContextEngine(CSharpScriptService scriptService) : IScriptablePromptContextEngine
	{
		/// <inheritdoc/>
		public ScriptLanguageType Language => ScriptLanguageType.CSharpScript;

		/// <inheritdoc/>
		public IScriptablePromptContextEngineDescriptor Descriptor { get; } = new CSharpScriptPromptContextEngineDescriptor();

		/// <inheritdoc/>
		public async Task<string?> RunAsync(IServiceProvider services, string body, string? previous,
			CancellationToken cancellationToken = default)
		{
			var output = new CSharpScriptPromptContextOutput();

			await scriptService.RunAsync(body, new CSharpScriptPromptContextGlobals
			{
				Previous = previous,
				Output = output
			}, cancellationToken);

			return output.Text;
		}
	}

	/// <summary>
	/// The globals of a C# script prompt context: the previous text and the sink the script writes the
	/// context text into (a C# script has no captured stdout, and its return value is not the channel - the
	/// same way the tool and command engines use <c>Result.Write</c> / ignore the return value).
	/// </summary>
	public class CSharpScriptPromptContextGlobals
	{
		/// <summary>The previously injected text of this context, or null when there is none.</summary>
		public string? Previous { get; init; }

		/// <summary>The sink the script writes the context text into.</summary>
		public required CSharpScriptPromptContextOutput Output { get; init; }
	}

	/// <summary>The descriptor of the C# script prompt-context engine.</summary>
	public class CSharpScriptPromptContextEngineDescriptor : IScriptablePromptContextEngineDescriptor
	{
		/// <inheritdoc/>
		public ScriptLanguageType Language => ScriptLanguageType.CSharpScript;

		/// <inheritdoc/>
		public string[] Extensions => [".csx"];

		/// <inheritdoc/>
		public string FrontmatterStart => "/*";

		/// <inheritdoc/>
		public string FrontmatterEnd => "*/";
	}
}
