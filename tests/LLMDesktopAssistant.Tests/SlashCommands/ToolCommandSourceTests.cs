using System.Text.Json.Nodes;
using LLMDesktopAssistant.Addons;
using LLMDesktopAssistant.Agents;
using LLMDesktopAssistant.SlashCommands;
using LLMDesktopAssistant.SlashCommands.Arguments;
using LLMDesktopAssistant.SlashCommands.Providers;
using LLMDesktopAssistant.Tools;

namespace LLMDesktopAssistant.Tests.SlashCommands
{
	/// <summary>
	/// The tool-command source: the provider that derives <c>/tool:&lt;name&gt;</c>, the JSON-Schema mapping of a
	/// tool's arguments onto the keyed command arguments, and the JSON-literal format provider.
	/// </summary>
	public class ToolCommandSourceTests
	{
		private sealed class FakeCollector<T>(IEnumerable<T> addons) : IAddonSetCollector<T>
		{
			public IEnumerable<T> GetAvailableAddons() => addons;

			public IEnumerable<T> GetAddonsForChat() => addons;

			public IEnumerable<T> GetAddonsForAgent(ChatAgentDescriptor agent) => addons;
		}

		private static ToolInfo Tool(string name, string? schema = null, AddonPackInfo? pack = null,
			AddonDiagnostic? diagnostic = null)
		{
			var tool = new ToolInfo
			{
				Name = name,
				Description = $"{name} description",
				ArgumentSchema = JsonNode.Parse(schema ?? """{"type":"object","properties":{},"additionalProperties":false}""")!.AsObject(),
				SourcePack = pack
			};

			if (diagnostic is not null)
				tool.Diagnostic = diagnostic;

			return tool;
		}

		private static SlashCommandArgumentSchema Schema(string schema) => ToolArgumentSchemaMapper.Create(
			JsonNode.Parse(schema)!.AsObject());

		// ==============================
		// === Provider               ===
		// ==============================

		[Fact]
		public void Provider_DerivesOneCommandPerTool()
		{
			var provider = new ToolSlashCommandProvider(new FakeCollector<ToolInfo>(
				[Tool("fs-read"), Tool("web-search")]));

			var commands = provider.GetCommands().ToList();

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

			var provider = new ToolSlashCommandProvider(new FakeCollector<ToolInfo>([Tool("fs-read", pack: pack)]));

			var command = Assert.Single(provider.GetCommands());

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
			]));

			var command = Assert.Single(provider.GetCommands());

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
		public void Schema_OfANullSchema_IsEmpty() => Assert.Empty(ToolArgumentSchemaMapper.Create(null).Keyed);

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
