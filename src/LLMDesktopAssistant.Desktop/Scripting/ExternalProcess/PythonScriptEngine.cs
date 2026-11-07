using System.Text;
using LLMDesktopAssistant.Desktop.Execution;
using LLMDesktopAssistant.Desktop.Scripting.Python;
using LLMDesktopAssistant.LLM.Settings;
using LLMDesktopAssistant.Scripting;
using LLMDesktopAssistant.Services;
using LLMDesktopAssistant.StructuredValues;
using LLMDesktopAssistant.StructuredValues.Const;
using LLMDesktopAssistant.StructuredValues.Converters;

namespace LLMDesktopAssistant.Desktop.Scripting.ExternalProcess
{
	/// <summary>
	/// The Python language of the out-of-process family.
	/// </summary>
	[Service(typeof(IExternalProcessScriptEngine))]
	public sealed class PythonScriptEngine(IProcessLauncher processLauncher, PythonHelperService pythonHelperService,
		IPythonInterpreterLocator pythonInterpreterLocator) : ExternalProcessScriptEngineBase(processLauncher)
	{
		private static readonly HashSet<string> _reservedVariableNames = new(StringComparer.Ordinal) { "os", "sys" };

		/// <inheritdoc/>
		public override IScriptEngineDescriptor Descriptor { get; } = new PythonScriptEngineDescriptor();

		/// <inheritdoc/>
		protected override Encoding ScriptEncoding => new UTF8Encoding(encoderShouldEmitUTF8Identifier: false);

		/// <inheritdoc/>
		protected override IReadOnlySet<string> ReservedVariableNames => _reservedVariableNames;

		/// <inheritdoc/>
		protected override ProcessLaunchParameters CreateLaunchParameters(ChatEnvironmentSettings environment,
			string scriptPath, ExternalProcessScriptTerminalParameters parameters, bool runInTerminal)
			=> pythonHelperService.CreateLaunchParameters(environment, $"python \"{scriptPath}\"",
				parameters.ProcessName ?? "External script", runInTerminal, useAddonEnvironment: true);

		/// <inheritdoc/>
		protected override string BuildScript(ExternalProcessScriptTerminalParameters parameters,
			ScriptInputMode inputMode, string workingDirectory)
		{
			var builder = new StringBuilder();
			builder.AppendLine("import sys, os");
			builder.AppendLine("sys.stdout.reconfigure(encoding=\"utf-8\")");
			builder.AppendLine("sys.stderr.reconfigure(encoding=\"utf-8\")");
			builder.AppendLine($"os.chdir({PythonLiteral(workingDirectory)})");

			foreach (var (name, value) in parameters.Variables)
				builder.AppendLine($"{name} = {Serialize(value, inputMode)}");

			builder.AppendLine(parameters.Body);
			return builder.ToString();
		}

		/// <inheritdoc/>
		/// <remarks>
		/// There is no executable to locate - the shell picks one inside the venv - so the locator, which runs
		/// <c>python -V</c>, is the probe.
		/// </remarks>
		public override async Task<ScriptEngineAvailability> CheckAvailabilityAsync(
			ChatEnvironmentSettings settings, bool useAddonEnvironment = true,
			CancellationToken cancellationToken = default)
		{
			var resolution = await pythonInterpreterLocator
				.ResolveAsync(settings, useAddonEnvironment, cancellationToken)
				.ConfigureAwait(false);

			return resolution.Found
				? new ScriptEngineAvailability(true, resolution.Version, null, resolution.ExecutablePath)
				: new ScriptEngineAvailability(false, null, resolution.Problem);
		}

		private static string PythonLiteral(string value)
			=> new ConstNodeStringValue { Value = value }.ToPythonString();

		/// <summary>
		/// Serialises one variable: a native literal, or a JSON string the script parses itself.
		/// </summary>
		private static string Serialize(INodeValue? value, ScriptInputMode inputMode)
		{
			if (inputMode is not ScriptInputMode.Json)
				return value.ToPythonString();

			var json = value.ToJsonNode()?.ToJsonString() ?? "null";
			return new ConstNodeStringValue { Value = json }.ToPythonString();
		}
	}

	/// <summary>
	/// The descriptor of the Python language.
	/// </summary>
	public sealed class PythonScriptEngineDescriptor : IScriptEngineDescriptor
	{
		/// <inheritdoc/>
		public ScriptLanguageType Language => ScriptLanguageType.Python;

		/// <inheritdoc/>
		public string[] Extensions => [".py"];

		/// <inheritdoc/>
		public string FrontmatterStart => @"""""""";

		/// <inheritdoc/>
		public string FrontmatterEnd => @"""""""";

		/// <inheritdoc/>
		public string? FrontmatterLinePrefix => null;

		/// <inheritdoc/>
		public ScriptEngineCapabilities Capabilities =>
			ScriptEngineCapabilities.StructuredInput | ScriptEngineCapabilities.JsonOutput |
			ScriptEngineCapabilities.Terminal;
	}
}
