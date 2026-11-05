using System.ComponentModel;
using System.Text.Json.Nodes;
using LLMDesktopAssistant.Tools;
using LLMDesktopAssistant.Tools.Execution.Domain;
using LLMDesktopAssistant.Tools.Execution.Executors;
using LLMDesktopAssistant.Tools.Specifiers;

namespace LLMDesktopAssistant.Tests.Tools.Execution;

/// <summary>
/// The argument descriptor is the single description of a tool's arguments: what the schema, the
/// specifier dialect and the tools section read. These tests pin the projection, the metadata trim and
/// the hooks that used to live outside the model.
/// </summary>
public class ToolArgumentsDescriptorTests
{
	private static string ReadFile(
		[Description("The file to read.")] string path,
		int limit = 20,
		string[]? tags = null) => path;

	private static string DescribePath(
		[MainArgument] [PathArgument(File = true, Directory = false)] string path,
		[PathArgument(File = false, Directory = true)] string folder = ".") => path;

	private static ToolArgumentsDescriptor Describe(Delegate executor,
		Action<string, JsonObject>? modify = null) =>
		ToolMethodBinder.DescribeArguments(executor, modify);

	[Fact]
	public void DescribeArguments_KeepsDeclarationOrderAndRequired()
	{
		var arguments = Describe((Func<string, int, string[], string>)ReadFile);

		Assert.Equal(["path", "limit", "tags"], arguments.Arguments.Select(argument => argument.Name));
		Assert.Equal([0, 1, 2], arguments.Arguments.Select(argument => argument.Order));
		Assert.Equal(["path"], arguments.Arguments.Where(argument => argument.Required).Select(argument => argument.Name));
		Assert.Equal(["path", "limit"], arguments.SpecifierParameters.Select(argument => argument.Name));
		Assert.Null(arguments.MainName);
		Assert.Null(arguments.Main);
		Assert.Equal("The file to read.", arguments.Arguments[0].Description);
	}

	[Fact]
	public void DescribeArguments_ClassifiesKindsFromTheGeneratedType()
	{
		var arguments = Describe((Func<string, int, string[], string>)ReadFile);

		Assert.Equal(ToolArgumentKind.String, arguments.Arguments[0].Kind);
		Assert.Equal(ToolArgumentKind.Integer, arguments.Arguments[1].Kind);
		Assert.Equal(ToolArgumentKind.Array, arguments.Arguments[2].Kind);
		Assert.All(arguments.SpecifierParameters,
			argument => Assert.Equal(SpecifierTargetKind.Text, argument.SpecifierKind));
		Assert.Null(arguments.Arguments[2].SpecifierKind);
	}

	[Fact]
	public void DescribeArguments_ReadsMainAndPathAttributes_AndTrimsTheMetadata()
	{
		var arguments = Describe((Func<string, string, string>)DescribePath);
		var main = arguments.Main;
		var folder = arguments.Arguments.Single(argument => argument.Name == "folder");

		Assert.Equal("path", arguments.MainName);
		Assert.NotNull(main);
		Assert.True(main.IsMain);
		Assert.Equal(ToolArgumentKind.FilePath, main.Kind);
		Assert.Equal(SpecifierTargetKind.Path, main.SpecifierKind);
		Assert.True(main.OriginalSchema["main"]!.GetValue<bool>());
		Assert.Equal("file", main.OriginalSchema["kind"]!.GetValue<string>());
		Assert.False(main.Schema.ContainsKey("main"));
		Assert.False(main.Schema.ContainsKey("kind"));

		Assert.Equal(ToolArgumentKind.DirectoryPath, folder.Kind);
		Assert.Equal("directory", folder.OriginalSchema["kind"]!.GetValue<string>());
	}

	[Fact]
	public void ToSchema_BuildsTheObjectShapeInDeclarationOrder()
	{
		var schema = Describe((Func<string, int, string[], string>)ReadFile).ToSchema();

		Assert.Equal("object", schema["type"]!.GetValue<string>());
		Assert.False(schema["additionalProperties"]!.GetValue<bool>());
		Assert.Equal(["path", "limit", "tags"], schema["properties"]!.AsObject().Select(property => property.Key));
		Assert.Equal(["path"], schema["required"]!.AsArray().Select(name => name!.GetValue<string>()));
		Assert.Equal(20, schema["properties"]!["limit"]!["default"]!.GetValue<int>());
	}

	[Fact]
	public void ToSchema_LeavesNoMetadataBehind()
	{
		var schema = Describe((Func<string, string, string>)DescribePath).ToSchema();
		var path = schema["properties"]!["path"]!.AsObject();

		Assert.False(path.ContainsKey("main"));
		Assert.False(path.ContainsKey("kind"));
	}

	[Fact]
	public void ModifyArgumentSchema_RunsPerArgument_AndLandsInTheSchema()
	{
		var seen = new List<string>();

		var arguments = Describe((Func<string, int, string[], string>)ReadFile, (name, schema) =>
		{
			seen.Add(name);

			if (name == "limit")
				schema["enum"] = new JsonArray(JsonValue.Create(10), JsonValue.Create(20), JsonValue.Create(30));
		});

		Assert.Equal(["path", "limit", "tags"], seen);
		Assert.Equal([10, 20, 30],
			arguments.ToSchema()["properties"]!["limit"]!["enum"]!.AsArray()
				.Select(value => value!.GetValue<int>()));
	}

	[Fact]
	public void ModifyArgumentSchema_ChangingTheType_Throws()
	{
		Assert.Throws<ArgumentException>(() => Describe(
			(Func<string, int, string[], string>)ReadFile,
			(name, schema) =>
			{
				if (name == "limit")
					schema["type"] = "string";
			}));
	}

	[Fact]
	public void FromSchema_KeepsTheAuthoredRoot_AndTrimsTheProperties()
	{
		var schema = JsonNode.Parse("""
			{
				"type": "object",
				"title": "Third-party tool",
				"properties": {
					"path": { "type": "string", "description": "The file.", "kind": "file", "main": true },
					"count": { "type": "integer" }
				},
				"required": ["path"],
				"additionalProperties": true
			}
			""")!.AsObject();

		var arguments = ToolArgumentsDescriptor.FromSchema(schema);

		Assert.Equal("path", arguments.MainName);
		Assert.Equal(ToolArgumentKind.FilePath, arguments.Main!.Kind);
		Assert.Equal(["path", "count"], arguments.Arguments.Select(argument => argument.Name));
		Assert.True(arguments.Arguments[0].Required);
		Assert.False(arguments.Arguments[1].Required);

		var projected = arguments.ToSchema();
		Assert.Equal("Third-party tool", projected["title"]!.GetValue<string>());
		Assert.True(projected["additionalProperties"]!.GetValue<bool>());
		Assert.Equal(["path"], projected["required"]!.AsArray().Select(name => name!.GetValue<string>()));
		Assert.False(projected["properties"]!["path"]!.AsObject().ContainsKey("kind"));
		Assert.False(projected["properties"]!["path"]!.AsObject().ContainsKey("main"));
	}

	[Fact]
	public void FromSchema_BareRoot_IsOneMainArgument()
	{
		var schema = JsonNode.Parse(
			"""{ "type": "string", "kind": "file", "description": "A bare path." }""")!.AsObject();

		var arguments = ToolArgumentsDescriptor.FromSchema(schema);

		Assert.False(arguments.IsObject);
		var main = Assert.Single(arguments.Arguments);
		Assert.Same(main, arguments.Main);
		Assert.Equal(string.Empty, main.Name);
		Assert.Equal(ToolArgumentKind.FilePath, main.Kind);
		Assert.Equal(SpecifierTargetKind.Path, main.SpecifierKind);

		// The whole schema is the argument schema, with the metadata still trimmed from it.
		var projected = arguments.ToSchema();
		Assert.Equal("string", projected["type"]!.GetValue<string>());
		Assert.Equal("A bare path.", projected["description"]!.GetValue<string>());
		Assert.False(projected.ContainsKey("kind"));
	}

	[Fact]
	public void FromSchema_MoreThanOneMainArgument_Throws()
	{
		var schema = JsonNode.Parse("""
			{
				"type": "object",
				"properties": {
					"a": { "type": "string", "main": true },
					"b": { "type": "string", "main": true }
				}
			}
			""")!.AsObject();

		Assert.Throws<ArgumentException>(() => ToolArgumentsDescriptor.FromSchema(schema));
	}
}
