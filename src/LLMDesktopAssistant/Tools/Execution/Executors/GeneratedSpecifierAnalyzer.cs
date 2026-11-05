using System.Text.Json.Nodes;
using LLMDesktopAssistant.LLM.Services;
using LLMDesktopAssistant.Tools.Execution.Domain;
using LLMDesktopAssistant.Tools.Specifiers;
using Microsoft.Extensions.DependencyInjection;

namespace LLMDesktopAssistant.Tools.Execution.Executors
{
	/// <summary>
	/// The specifier analyzer of a tool that declares none of its own. It reads the arguments the
	/// descriptor describes - the main argument and every named one - and matches them in the dialect
	/// each declares, which is what makes a rule work on a filesystem tool with no hand-written code:
	/// a path argument is matched as a filesystem pattern, everything else as text.
	/// </summary>
	/// <remarks>
	/// A tool overrides it with a handwritten analyzer only when the value a rule sees is not the raw
	/// argument: the shell, for one, splits a compound command into the parts a rule matches.
	/// </remarks>
	public sealed class GeneratedSpecifierAnalyzer : IToolSpecifierAnalyzer
	{
		/// <summary>
		/// The shared instance: the analyzer holds no state, because the arguments and the working
		/// directory both come from the invocation it is analysing.
		/// </summary>
		public static GeneratedSpecifierAnalyzer Instance { get; } = new();

		/// <inheritdoc/>
		public SpecifierMatchResult Analyze(Specifier specifier, JsonNode? args, IToolExecutionContext context)
		{
			ArgumentNullException.ThrowIfNull(context);

			return Analyze(specifier, args, context.Descriptor.Arguments, context.Services);
		}

		/// <summary>
		/// Analyses one rule: the values the call carries are matched against the arguments of the tool,
		/// in the dialect each argument declares.
		/// </summary>
		/// <param name="specifier">The parsed rule.</param>
		/// <param name="args">The effective arguments.</param>
		/// <param name="arguments">The arguments of the tool the call targets.</param>
		/// <param name="services">The services a path base directory is resolved from.</param>
		public static SpecifierMatchResult Analyze(Specifier specifier, JsonNode? args,
			ToolArgumentsDescriptor arguments, IServiceProvider services)
		{
			ArgumentNullException.ThrowIfNull(specifier);
			ArgumentNullException.ThrowIfNull(arguments);
			ArgumentNullException.ThrowIfNull(services);

			return SpecifierMatcher.Match(specifier, BuildTargets(args as JsonObject ?? [], arguments, services));
		}

		/// <summary>
		/// Builds the values the literals of a rule are matched against: the main argument first, then
		/// every argument a rule may name.
		/// </summary>
		private static IReadOnlyList<SpecifierTarget> BuildTargets(JsonObject args,
			ToolArgumentsDescriptor arguments, IServiceProvider services)
		{
			var targets = new List<SpecifierTarget>(arguments.Arguments.Length);
			string? baseDirectory = null;

			string BaseDirectory()
			{
				baseDirectory ??= ResolveWorkingDirectory(services) ?? throw new InvalidOperationException(
					"No working directory is available for a path specifier target.");

				return baseDirectory;
			}

			if (arguments.Main is { } main)
			{
				targets.Add(main.SpecifierKind == SpecifierTargetKind.Path
					? SpecifierTarget.Path(ValueOf(args, main), BaseDirectory())
					: SpecifierTarget.Text(ValueOf(args, main)));
			}

			foreach (var argument in arguments.SpecifierParameters)
			{
				var value = ValueOf(args, argument);
				targets.Add(argument.SpecifierKind == SpecifierTargetKind.Path
					? SpecifierTarget.Path(value, BaseDirectory(), argument.Name)
					: SpecifierTarget.Parameter(argument.Name, value));
			}

			return targets;
		}

		/// <summary>
		/// Reads the value the tool actually works with: the argument as sent, or the authored default when
		/// the model left it out. A string keeps its text, anything else keeps its JSON text, which is how
		/// a rule spells a boolean (<c>runTerminal:true</c>).
		/// </summary>
		private static string ValueOf(JsonObject args, ToolArgumentDescriptor argument)
		{
			var node = args[argument.Name] ?? argument.Default;

			return node switch
			{
				null => string.Empty,
				JsonValue value when value.TryGetValue<string>(out var text) => text,
				_ => node.ToJsonString()
			};
		}

		/// <summary>
		/// The base directory of a relative path argument and pattern: the working directory of the chat
		/// the invocation belongs to.
		/// </summary>
		private static string? ResolveWorkingDirectory(IServiceProvider services) =>
			services.GetService<IWorkingDirectoryAccessService>()?.GetWorkingDirectory();
	}
}
