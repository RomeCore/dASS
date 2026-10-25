using System.Text;
using LLMDesktopAssistant.Desktop.Execution;
using LLMDesktopAssistant.Desktop.Scripting.Bash;
using LLMDesktopAssistant.LLM.Settings;
using LLMDesktopAssistant.Scripting;
using LLMDesktopAssistant.Services;

namespace LLMDesktopAssistant.Desktop.Scripting.ExternalProcess
{
	/// <summary>
	/// The bash language of the out-of-process family.
	/// </summary>
	/// <remarks>
	/// Bash holds no structures, so every variable arrives as a JSON string the script decodes itself with
	/// <c>jq</c> or whatever else it has. There is no <c>cd</c> in the preamble: the launcher sets the working
	/// directory, and unlike python there is no module resolution that a working directory would fix.
	/// </remarks>
	[Service(typeof(IExternalProcessScriptEngine))]
	public sealed class BashScriptEngine(IProcessLauncher processLauncher, IBashInterpreterLocator interpreterLocator)
		: ExternalProcessScriptEngineBase(processLauncher)
	{
		/// <inheritdoc/>
		public override ScriptLanguageType Language => ScriptLanguageType.Bash;

		/// <inheritdoc/>
		public override IScriptEngineDescriptor Descriptor { get; } = new BashScriptEngineDescriptor();

		/// <inheritdoc/>
		protected override Encoding ScriptEncoding => new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

		/// <inheritdoc/>
		protected override ProcessLaunchParameters CreateLaunchParameters(ChatEnvironmentSettings environment,
			string scriptPath, ExternalProcessScriptTerminalParameters parameters, bool runInTerminal)
		{
			var configured = environment.GetEffectiveAdditionalSettings()
				.OfType<BashEnvironmentConfiguration>()
				.FirstOrDefault()?.ExecutablePath;

			var interpreter = interpreterLocator.Resolve(configured);
			if (!interpreter.Found)
				throw new InvalidOperationException(interpreter.Problem);

			return new ProcessLaunchParameters
			{
				ProcessName = parameters.ProcessName ?? "External script",
				FileName = interpreter.Path!,
				Arguments = [scriptPath],
				RunInTerminal = runInTerminal,
				WorkingDirectory = environment.GetEffectiveWorkingDirectories().GetWorkingDirectory()
			};
		}

		/// <inheritdoc/>
		protected override string BuildScript(ExternalProcessScriptTerminalParameters parameters,
			ScriptInputMode inputMode, string workingDirectory)
			=> BashScriptBuilder.Build(parameters);
	}

	/// <summary>
	/// The descriptor of the bash language: <c>.sh</c>, a <c>#</c>-prefixed frontmatter block and no structures.
	/// </summary>
	public sealed class BashScriptEngineDescriptor : IScriptEngineDescriptor
	{
		/// <inheritdoc/>
		public ScriptLanguageType Language => ScriptLanguageType.Bash;

		/// <inheritdoc/>
		public string[] Extensions => [".sh"];

		/// <inheritdoc/>
		public string FrontmatterStart => "---";

		/// <inheritdoc/>
		public string FrontmatterEnd => "---";

		/// <inheritdoc/>
		public string? FrontmatterLinePrefix => "#";

		/// <inheritdoc/>
		public ScriptEngineCapabilities Capabilities =>
			ScriptEngineCapabilities.JsonOutput | ScriptEngineCapabilities.Terminal;
	}
}
