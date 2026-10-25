using System.Text;
using LLMDesktopAssistant.Desktop.Execution;
using LLMDesktopAssistant.Desktop.Scripting.PowerShell;
using LLMDesktopAssistant.LLM.Settings;
using LLMDesktopAssistant.Scripting;
using LLMDesktopAssistant.Services;

namespace LLMDesktopAssistant.Desktop.Scripting.ExternalProcess
{
	/// <summary>
	/// The PowerShell language of the out-of-process family.
	/// </summary>
	/// <remarks>
	/// One engine serves both lines and every platform: <c>pwsh</c> 7 on Windows, Linux and macOS, and Windows
	/// PowerShell 5.1 where 7 is not installed. The argument list is deliberately the same everywhere -
	/// <c>-ExecutionPolicy</c> is ignored off Windows - and there is no <c>-NonInteractive</c>, because a script that
	/// runs in a terminal has to be able to prompt.
	/// </remarks>
	[Service(typeof(IExternalProcessScriptEngine))]
	public sealed class PowerShellScriptEngine(IProcessLauncher processLauncher,
		IPowerShellInterpreterLocator interpreterLocator) : ExternalProcessScriptEngineBase(processLauncher)
	{
		/// <inheritdoc/>
		public override ScriptLanguageType Language => ScriptLanguageType.PowerShell;

		/// <inheritdoc/>
		public override IScriptEngineDescriptor Descriptor { get; } = new PowerShellScriptEngineDescriptor();

		/// <inheritdoc/>
		/// <remarks>
		/// With a BOM: Windows PowerShell 5.1 reads a BOM-less <c>.ps1</c> as ANSI and turns Cyrillic into mojibake,
		/// while PowerShell 7 accepts the BOM and needs nothing from it.
		/// </remarks>
		protected override Encoding ScriptEncoding => new UTF8Encoding(encoderShouldEmitUTF8Identifier: true);

		/// <inheritdoc/>
		protected override ProcessLaunchParameters CreateLaunchParameters(ChatEnvironmentSettings environment,
			string scriptPath, ExternalProcessScriptTerminalParameters parameters, bool runInTerminal)
		{
			var configured = environment.GetEffectiveAdditionalSettings()
				.OfType<PowerShellEnvironmentConfiguration>()
				.FirstOrDefault()?.ExecutablePath;

			var interpreter = interpreterLocator.Resolve(configured);
			if (!interpreter.Found)
				throw new InvalidOperationException(interpreter.Problem);

			return new ProcessLaunchParameters
			{
				ProcessName = parameters.ProcessName ?? "External script",
				FileName = interpreter.Path!,
				// The profile is skipped so a machine-wide alias cannot change what an addon does; -File must stay
				// last, because everything after it would become a script parameter.
				Arguments = ["-NoLogo", "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", scriptPath],
				RunInTerminal = runInTerminal,
				WorkingDirectory = environment.GetEffectiveWorkingDirectories().GetWorkingDirectory()
			};
		}

		/// <inheritdoc/>
		protected override string BuildScript(ExternalProcessScriptTerminalParameters parameters,
			ScriptInputMode inputMode, string workingDirectory)
			=> PowerShellScriptBuilder.Build(parameters, inputMode);
	}

	/// <summary>
	/// The descriptor of the PowerShell language: <c>.ps1</c>, a <c>#</c>-prefixed frontmatter block, structures by
	/// round-trip and the terminal.
	/// </summary>
	public sealed class PowerShellScriptEngineDescriptor : IScriptEngineDescriptor
	{
		/// <inheritdoc/>
		public ScriptLanguageType Language => ScriptLanguageType.PowerShell;

		/// <inheritdoc/>
		public string[] Extensions => [".ps1"];

		/// <inheritdoc/>
		public string FrontmatterStart => "---";

		/// <inheritdoc/>
		public string FrontmatterEnd => "---";

		/// <inheritdoc/>
		public string? FrontmatterLinePrefix => "#";

		/// <inheritdoc/>
		public ScriptEngineCapabilities Capabilities =>
			ScriptEngineCapabilities.StructuredInput | ScriptEngineCapabilities.JsonOutput |
			ScriptEngineCapabilities.Terminal;
	}
}
