using System.Text.Json.Nodes;
using LLMDesktopAssistant.Addons;
using LLMDesktopAssistant.LLM.Services;
using LLMDesktopAssistant.LLM.Settings;
using Microsoft.Extensions.DependencyInjection;
using LLMDesktopAssistant.Agents;
using LLMDesktopAssistant.SlashCommands;
using LLMDesktopAssistant.SlashCommands.Arguments;
using LLMDesktopAssistant.SlashCommands.Providers;
using LLMDesktopAssistant.Tools;
using LLMDesktopAssistant.Tools.Execution.Domain;

namespace LLMDesktopAssistant.Tests.SlashCommands
{
	/// <summary>
	/// The tool-command source: the provider that derives <c>/tool:&lt;name&gt;</c>, the JSON-Schema mapping of a
	/// tool's arguments onto the keyed command arguments, and the JSON-literal format provider.
	/// </summary>
	public class ToolCommandSourceTests
	{
		private sealed class FakeCollector<T>(IEnumerable<T> addons) : IAddonSetCollector<T>
			where T : AddonBase<T>
		{
			private readonly AddonSet<T> _set = new(addons);

			public AddonSet<T> GetAvailableAddons() => _set;

			public AddonSet<T> GetAddonsForChat() => _set;

			public AddonSet<T> GetAddonsForAgent(ChatAgentDescriptor agent) => _set;
		}

		private static ToolInfo Tool(string name, string? schema = null, AddonPackInfo? pack = null,
			AddonDiagnostic? diagnostic = null)
		{
			var tool = new ToolInfo
			{
				Name = name,
				Description = $"{name} description",
				Arguments = ToolArgumentsDescriptor.FromSchema(JsonNode.Parse(schema ??
					"""{"type":"object","properties":{},"additionalProperties":false}""")!.AsObject()),
				SourcePack = pack
			};

			if (diagnostic is not null)
				tool.Diagnostic = diagnostic;

			return tool;
		}

		/// <summary>
		/// The services a tool command's path completion resolves the working directory from.
		/// </summary>
		private static readonly IServiceProvider Services = new ServiceCollection()
			.AddSingleton<IWorkingDirectoryAccessService>(new StubWorkingDirectory("/work"))
			.BuildServiceProvider();

		/// <summary>
		/// A fixed working directory: the path completion of a tool command resolves against it.
		/// </summary>
		private sealed class StubWorkingDirectory(string root) : IWorkingDirectoryAccessService
		{
			public string AccessPath(string path, DirectoryAccessMode mode) => Path.Combine(root, path);

			public string CheckedAccessPath(string path, DirectoryAccessMode mode, out bool isAccessed)
			{
				isAccessed = true;
				return Path.Combine(root, path);
			}

			public string GetWorkingDirectory() => root;

			public string? TryAccessPath(string path, DirectoryAccessMode mode) => Path.Combine(root, path);
		}

		private static SlashCommandArgumentSchema Schema(string schema) => ToolArgumentSchemaMapper.Create(
			ToolArgumentsDescriptor.FromSchema(JsonNode.Parse(schema)!.AsObject()), Services);

		// ==============================
		// === Provider               ===
		// ==============================

		[Fact]
		public void Provider_DerivesOneCommandPerTool()
		{
			var provider = new ToolSlashCommandProvider(new FakeCollector<ToolInfo>(
				[Tool("fs-read"), Tool("web-search")]), Services);

			var commands = provider.Addons.ToList();

			Assert.Equal(new[] { "fs-read", "web-search" }, commands.Select(c => c.Name));
			Assert.All(commands, c => Assert.Equal(SlashCommandSource.Tool, c.SourceKind));
			Assert.All(commands, c => Assert.Equal(SlashCommandOrderTiers.Derived, c.OverrideOrder));
			Assert.All(commands, c => Assert.Equal(new[] { "tool" }, c.Namespaces));
			Assert.All(commands, c => Assert.IsType<ToolCommandExecutor>(c.Executor));
		}

		[Fact]
		public void Provider_AddsThePackNamespace()
		{
			var pack = new AddonPackInfo
			{
				Name = "authoring",
				Path = "/packs/authoring",
				Source = AddonPackSource.Scanned,
				IsConfigurable = true
			};

			var provider = new ToolSlashCommandProvider(
				new FakeCollector<ToolInfo>([Tool("fs-read", pack: pack)]), Services);

			var command = Assert.Single(provider.Addons);

			Assert.Equal(new[] { "tool", "authoring" }, command.Namespaces);
			Assert.Same(pack, command.SourcePack);
		}

		[Fact]
		public void Provider_SkipsInvalidTools()
		{
			var provider = new ToolSlashCommandProvider(new FakeCollector<ToolInfo>(
			[
				Tool("broken", diagnostic: new AddonDiagnostic { IsFatal = true }),
				Tool("ok")
			]), Services);

			var command = Assert.Single(provider.Addons);

			Assert.Equal("ok", command.Name);
		}

		// ==============================
		// === Schema mapping         ===
		// ==============================

		[Fact]
		public void Schema_IsKeyedOnly()
		{
			var schema = Schema("""
				{
				  "type": "object",
				  "properties": {
				    "path": { "type": "string", "description": "File to read" },
				    "limit": { "type": "integer" }
				  },
				  "required": ["path"]
				}
				""");

			Assert.Empty(schema.Positionals);
			Assert.Null(schema.RestPositional);
			Assert.False(schema.HasRestPositional);
			Assert.Equal(new[] { "limit", "path" }, schema.Keyed.Keys.OrderBy(k => k, StringComparer.Ordinal));
		}

		[Fact]
		public void Schema_CarriesNameDescriptionRequiredAndDefault()
		{
			var schema = Schema("""
				{
				  "type": "object",
				  "properties": {
				    "path": { "type": "string", "description": "File to read" },
				    "depth": { "type": "integer", "default": 2 },
				    "mode": { "type": "string", "default": "fast" }
				  },
				  "required": ["path"]
				}
				""");

			var path = schema.Keyed["path"];
			Assert.Equal("path", path.Name.Key);
			Assert.Equal("File to read", path.Description!.Key);
			Assert.True(path.Required);
			Assert.Null(path.Default);

			var depth = schema.Keyed["depth"];
			Assert.False(depth.Required);
			Assert.Equal("2", depth.Default);

			// A string default is a value, not a literal: the quotes stay off.
			Assert.Equal("fast", schema.Keyed["mode"].Default);
		}

		[Fact]
		public void Schema_PicksTheFormatByDeclaredType()
		{
			var schema = Schema("""
				{
				  "type": "object",
				  "properties": {
				    "flag": { "type": "boolean" },
				    "count": { "type": "integer" },
				    "ratio": { "type": "number" },
				    "paths": { "type": "array", "items": { "type": "string" } },
				    "options": { "type": "object" },
				    "mode": { "type": "string", "enum": ["fast", "full"] },
				    "anything": { "anyOf": [{ "type": "string" }, { "type": "number" }] },
				    "plain": { "type": "string" },
				    "untyped": { }
				  }
				}
				""");

			Assert.Same(SlashCommandBooleanFormatProvider.Instance, schema.Keyed["flag"].Format);
			Assert.Same(NumberArgumentFormatProvider.Instance, schema.Keyed["count"].Format);
			Assert.Same(NumberArgumentFormatProvider.Instance, schema.Keyed["ratio"].Format);
			Assert.Same(JsonArgumentFormatProvider.Instance, schema.Keyed["paths"].Format);
			Assert.Same(JsonArgumentFormatProvider.Instance, schema.Keyed["options"].Format);

			var choice = Assert.IsType<ChoiceArgumentFormatProvider>(schema.Keyed["mode"].Format);
			Assert.Equal(new[] { "fast", "full" }, choice.Choices);

			// Everything the schema cannot classify stays a plain, unvalidated string.
			Assert.Null(schema.Keyed["anything"].Format);
			Assert.Null(schema.Keyed["plain"].Format);
			Assert.Null(schema.Keyed["untyped"].Format);
		}

		[Fact]
		public void Schema_ReadsAUnionTypeFromItsFirstEntry()
		{
			var schema = Schema("""
				{
				  "type": "object",
				  "properties": { "count": { "type": ["integer", "null"] } }
				}
				""");

			Assert.Same(NumberArgumentFormatProvider.Instance, schema.Keyed["count"].Format);
		}

		[Fact]
		public void Schema_WithoutProperties_IsEmpty()
		{
			var schema = Schema("""{"type":"object","additionalProperties":false}""");

			Assert.Empty(schema.Keyed);
			Assert.Empty(schema.Positionals);
			Assert.Null(schema.RestPositional);
		}

		[Fact]
		public void Schema_OfAToolWithoutArguments_IsEmpty() =>
			Assert.Empty(ToolArgumentSchemaMapper.Create(ToolArgumentsDescriptor.Empty, Services).Keyed);

		[Fact]
		public void Schema_OfAPathArgument_GetsFileCompletion()
		{
			var schema = Schema("""
				{
					"type": "object",
					"properties": {
						"path": { "type": "string", "kind": "file" },
						"folder": { "type": "string", "kind": "directory" },
						"note": { "type": "string" }
					}
				}
				""");

			Assert.IsType<FileArgumentFormatProvider>(schema.Keyed["path"].Format);
			Assert.IsType<FileArgumentFormatProvider>(schema.Keyed["folder"].Format);
			Assert.Null(schema.Keyed["note"].Format);
		}

		[Fact]
		public void Schema_OfAMainArgument_BecomesTheRestPositional()
		{
			// The main argument is typed, not named: '/tool:shell-bash git status --short' is one value.
			var schema = Schema("""
				{
					"type": "object",
					"properties": {
						"command": { "type": "string", "main": true },
						"runTerminal": { "type": "boolean", "default": false }
					},
					"required": ["command"]
				}
				""");

			Assert.NotNull(schema.RestPositional);
			Assert.True(schema.RestPositional!.Required);
			Assert.False(schema.Keyed.ContainsKey("command"));
			Assert.True(schema.Keyed.ContainsKey("runTerminal"));
		}

		// ==============================
		// === JSON literal format    ===
		// ==============================

		[Fact]
		public void JsonFormat_ConvertsAValidLiteral()
		{
			var provider = JsonArgumentFormatProvider.Instance;

			Assert.True(provider.TryValidate("""["a","b"]""", out var error));
			Assert.Null(error);

			var array = Assert.IsType<JsonArray>(provider.Convert("""["a","b"]"""));
			Assert.Equal(2, array.Count);

			var objectNode = Assert.IsType<JsonObject>(provider.Convert("""{"depth":2}"""));
			Assert.Equal(2, objectNode["depth"]!.GetValue<int>());
		}

		[Theory]
		[InlineData("not json")]
		[InlineData("{broken")]
		[InlineData("null")]
		[InlineData("")]
		public void JsonFormat_RejectsEverythingThatIsNotALiteral(string raw)
		{
			var provider = JsonArgumentFormatProvider.Instance;

			Assert.False(provider.TryValidate(raw, out var error));
			Assert.Equal("command.error.invalid_argument", error!.Key);
		}
	}
}
