using System.Text.Json;
using LiteDB;
using LLMDesktopAssistant.Controls.Icons;
using Material.Icons;

// LiteDB also exposes a JsonSerializer type.
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace LLMDesktopAssistant.Tests.Controls.Icons;

/// <summary>
/// Tests for the pack-agnostic icon reference: parsing, serialization and resolution.
/// </summary>
public class VisualIconKindTests
{
	private static readonly JsonSerializerOptions _json = new();

	[Theory]
	[InlineData("material:Account", IconPackKind.Material, "Account")]
	[InlineData("Material:Account", IconPackKind.Material, "Account")]
	[InlineData("Account", IconPackKind.Material, "Account")]        // bare name -> material
	[InlineData("path:M0 0 L24 24", IconPackKind.Path, "M0 0 L24 24")]
	[InlineData("material:", IconPackKind.Material, "")]
	[InlineData("lucide:home", IconPackKind.Unknown, "lucide:home")]  // unknown pack kept verbatim
	[InlineData(":x", IconPackKind.Unknown, ":x")]
	public void Parse_SplitsPackAndData(string input, IconPackKind pack, string data)
	{
		var kind = VisualIconKind.Parse(input);

		Assert.Equal(pack, kind.Pack);
		Assert.Equal(data, kind.Data);
	}

	[Theory]
	[InlineData("")]
	[InlineData("   ")]
	public void Parse_EmptyInput_IsNone(string input)
		=> Assert.True(VisualIconKind.Parse(input).IsNone);

	[Fact]
	public void Parse_Null_Throws()
		=> Assert.Throws<FormatException>(() => VisualIconKind.Parse(null));

	[Fact]
	public void TryParse_Null_ReturnsFalse()
		=> Assert.False(VisualIconKind.TryParse(null, out _));

	[Fact]
	public void Default_IsNone()
		=> Assert.True(default(VisualIconKind).IsNone);

	[Fact]
	public void Parse_BareName_IsNormalizedToMaterial()
		=> Assert.Equal("material:Plus", VisualIconKind.Parse("Plus").ToString());

	[Theory]
	[InlineData("material:Account")]
	[InlineData("Account")]
	[InlineData("path:M0 0 L24 24")]
	[InlineData("lucide:home")]
	public void ToString_RoundTrips(string input)
	{
		var first = VisualIconKind.Parse(input);
		var second = VisualIconKind.Parse(first.ToString());

		Assert.Equal(first, second);
	}

	[Fact]
	public void ToString_None_IsEmpty()
		=> Assert.Equal(string.Empty, VisualIconKind.None.ToString());

	[Fact]
	public void ImplicitConversion_FromMaterialIconKind()
	{
		VisualIconKind kind = MaterialIconKind.Account;

		Assert.Equal(IconPackKind.Material, kind.Pack);
		Assert.Equal("Account", kind.Data);
	}

	[Fact]
	public void ImplicitConversion_LiftsOverNullable()
	{
		MaterialIconKind? material = MaterialIconKind.Account;
		VisualIconKind? lifted = material;

		Assert.Equal(VisualIconKind.Parse("material:Account"), lifted);
	}

	[Fact]
	public void Json_None_WritesNull()
		=> Assert.Equal("null", JsonSerializer.Serialize(VisualIconKind.None, _json));

	[Fact]
	public void Json_Deserialize_Null_IsNone()
		=> Assert.True(JsonSerializer.Deserialize<VisualIconKind>("null", _json).IsNone);

	[Theory]
	[InlineData("material:Account", "\"material:Account\"")]
	[InlineData("path:M0 0 L24 24", "\"path:M0 0 L24 24\"")]
	[InlineData("lucide:home", "\"lucide:home\"")]
	public void Json_RoundTrips(string input, string expectedJson)
	{
		var kind = VisualIconKind.Parse(input);

		var json = JsonSerializer.Serialize(kind, _json);
		Assert.Equal(expectedJson, json);
		Assert.Equal(kind, JsonSerializer.Deserialize<VisualIconKind>(json, _json));
	}

	[Fact]
	public void Json_Nullable_RoundTrips()
	{
		VisualIconKind? kind = VisualIconKind.Parse("material:Account");

		var json = JsonSerializer.Serialize(kind, _json);
		Assert.Equal("\"material:Account\"", json);
		Assert.Equal(kind, JsonSerializer.Deserialize<VisualIconKind?>(json, _json));
	}

	[Fact]
	public void Json_NoneNullable_RoundTrips()
	{
		VisualIconKind? kind = VisualIconKind.None;

		// A nullable "no icon" is written as null; reading it back yields null, which is the
		// nullable spelling of "no icon" (equivalent to None).
		var json = JsonSerializer.Serialize(kind, _json);
		Assert.Equal("null", json);
		Assert.Null(JsonSerializer.Deserialize<VisualIconKind?>(json, _json));
	}

	private sealed class BsonHolder
	{
		public int Id { get; set; }

		public VisualIconKind? Icon { get; set; }

	}

	[Fact]
	public void Bson_RoundTrips()
	{
		var mapper = new BsonMapper();
		VisualIconKindBsonSerializer.Register(mapper);

		using var db = new LiteDatabase(new MemoryStream(), mapper);
		var collection = db.GetCollection<BsonHolder>("holders");
		collection.Insert(new BsonHolder { Icon = VisualIconKind.Parse("material:Account") });
		collection.Insert(new BsonHolder { Icon = VisualIconKind.Parse("path:M0 0 L24 24") });
		collection.Insert(new BsonHolder { Icon = null });

		var all = collection.FindAll().OrderBy(h => h.Id).ToList();

		Assert.Equal(IconPackKind.Material, all[0].Icon!.Value.Pack);
		Assert.Equal("Account", all[0].Icon!.Value.Data);

		// Path data must survive BSON whitespace handling untouched.
		Assert.Equal(IconPackKind.Path, all[1].Icon!.Value.Pack);
		Assert.Equal("M0 0 L24 24", all[1].Icon!.Value.Data);

		Assert.Null(all[2].Icon);
	}

	[Fact]
	public void Resolve_Null_IsNone()
		=> Assert.Equal(VisualIconStatus.None, VisualIconDataHandler.Resolve((VisualIconKind?)null).Status);

	[Fact]
	public void Resolve_NoneKind_IsNone()
		=> Assert.Equal(VisualIconStatus.None, VisualIconDataHandler.Resolve(VisualIconKind.None).Status);

	[Fact]
	public void Resolve_Material_Ok()
	{
		var data = VisualIconDataHandler.Resolve(VisualIconKind.Parse("material:Account"));

		Assert.Equal(VisualIconStatus.Ok, data.Status);
		Assert.False(string.IsNullOrEmpty(data.SvgPath));
	}

	[Fact]
	public void Resolve_Material_BareName_Ok()
		=> Assert.True(VisualIconDataHandler.Resolve(VisualIconKind.Parse("Account")).IsOk);

	[Fact]
	public void Resolve_Material_UnknownName_IsInvalidData()
		=> Assert.Equal(VisualIconStatus.InvalidData, VisualIconDataHandler.Resolve(VisualIconKind.Parse("material:NotARealIcon")).Status);

	[Fact]
	public void Resolve_Material_EmptyData_IsInvalidData()
		=> Assert.Equal(VisualIconStatus.InvalidData, VisualIconDataHandler.Resolve(VisualIconKind.Parse("material:")).Status);

	[Fact]
	public void Resolve_UnknownPack_IsInvalidPack()
		=> Assert.Equal(VisualIconStatus.InvalidPack, VisualIconDataHandler.Resolve(VisualIconKind.Parse("lucide:home")).Status);

	[Fact]
	public void Resolve_UnknownPack_ProvidesFallbackGlyph()
		=> Assert.False(string.IsNullOrEmpty(VisualIconDataHandler.Resolve(VisualIconKind.Parse("lucide:home")).SvgPath));

	[Fact]
	public void Resolve_Path_Ok()
	{
		var data = VisualIconDataHandler.Resolve(VisualIconKind.FromPath("M0 0 L24 24"));

		Assert.Equal(VisualIconStatus.Ok, data.Status);
		Assert.Equal("M0 0 L24 24", data.SvgPath);
	}

	[Fact]
	public void Resolve_Path_Garbage_IsInvalidData()
		=> Assert.Equal(VisualIconStatus.InvalidData, VisualIconDataHandler.Resolve(VisualIconKind.Parse("path:definitely not a path")).Status);
}
