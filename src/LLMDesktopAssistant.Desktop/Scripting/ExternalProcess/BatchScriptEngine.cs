using System.Text;
using LLMDesktopAssistant.Desktop.Execution;
using LLMDesktopAssistant.Desktop.Scripting.Batch;
using LLMDesktopAssistant.LLM.Settings;
using LLMDesktopAssistant.Scripting;
using LLMDesktopAssistant.Services;

namespace LLMDesktopAssistant.Desktop.Scripting.ExternalProcess
{
	/// <summary>
	/// The batch language of the out-of-process family.
	/// </summary>
	/// <remarks>
	/// Batch is the only language here that lives on one platform, so the descriptor claims its extensions on Windows
	/// only and off Windows the engine has nothing to offer. A batch file cannot be handed to CreateProcess at all -
	/// cmd has to be the process - and it is launched through <c>/c</c> with a doubled quote pair, because cmd strips
	/// the outer pair and an unquoted temp path with a space would be split.
	/// </remarks>
	[Service(typeof(IExternalProcessScriptEngine))]
	public sealed class BatchScriptEngine(IProcessLauncher processLauncher, IBatchInterpreterLocator interpreterLocator)
		: ExternalProcessScriptEngineBase(processLauncher)
	{
		/// <inheritdoc/>
		public override ScriptLanguageType Language => ScriptLanguageType.Batch;

		/// <inheritdoc/>
		public override IScriptEngineDescriptor Descriptor { get; } = new BatchScriptEngineDescriptor();

		/// <inheritdoc/>
		protected override Encoding ScriptEncoding => new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

		/// <inheritdoc/>
		protected override ProcessLaunchParameters CreateLaunchParameters(ChatEnvironmentSettings environment,
			string scriptPath, ExternalProcessScriptTerminalParameters parameters, bool runInTerminal)
		{
			var interpreter = interpreterLocator.Resolve();
			if (!interpreter.Found)
				throw new InvalidOperationException(interpreter.Problem);

			return new ProcessLaunchParameters
			{
				ProcessName = parameters.ProcessName ?? "External script",
				FileName = interpreter.Path!,
				// /d skips the machine-wide AutoRun, /s fixes cmd's quote stripping to the documented "old" behaviour,
				// and the doubled pair leaves one quoted path for cmd to run.
				Arguments = ["/d", "/s", "/c", $"\"\"{scriptPath}\"\""],
				VerbatimArguments = true,
				RunInTerminal = runInTerminal,
				WorkingDirectory = environment.GetEffectiveWorkingDirectories().GetWorkingDirectory()
			};
		}

		/// <inheritdoc/>
		protected override string BuildScript(ExternalProcessScriptTerminalParameters parameters,
			ScriptInputMode inputMode, string workingDirectory)
			=> BatchScriptBuilder.Build(parameters);
	}

	/// <summary>
	/// The descriptor of the batch language: <c>.bat</c> and <c>.cmd</c>, a <c>REM</c>-prefixed frontmatter block, no
	/// structures and no platform other than Windows.
	/// </summary>
	public sealed class BatchScriptEngineDescriptor : IScriptEngineDescriptor
	{
		/// <inheritdoc/>
		public ScriptLanguageType Language => ScriptLanguageType.Batch;

		/// <inheritdoc/>
		/// <remarks>
		/// Empty off Windows: claiming the extensions there would offer an addon that can never run, while a language
		/// that does not exist on the platform is simply absent from the catalogue, exactly like a file of another
		/// kind's format.
		/// </remarks>
		public string[] Extensions => OperatingSystem.IsWindows() ? [".bat", ".cmd"] : [];

		/// <inheritdoc/>
		public string FrontmatterStart => "---";

		/// <inheritdoc/>
		public string FrontmatterEnd => "---";

		/// <inheritdoc/>
		public string? FrontmatterLinePrefix => "REM ";

		/// <inheritdoc/>
		public ScriptEngineCapabilities Capabilities =>
			ScriptEngineCapabilities.JsonOutput | ScriptEngineCapabilities.Terminal;
	}
}
