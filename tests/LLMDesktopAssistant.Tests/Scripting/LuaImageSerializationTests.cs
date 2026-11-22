using AsyncLua;
using AsyncLua.Values;
using LLMDesktopAssistant.Scripting.Lua;
using LLMDesktopAssistant.Scripting.Lua.Converters;
using LLMDesktopAssistant.MVVM.Dynamic;
using LLMDesktopAssistant.Scripting.Lua.Store;
using LLMDesktopAssistant.StructuredValues.Const;
using LLMDesktopAssistant.StructuredValues.Converters;
using LLMDesktopAssistant.Utils;

namespace LLMDesktopAssistant.Tests.Scripting;

public class LuaImageSerializationTests
{
	private static LuaImage NewImage() => LuaImage.Create(4, 3, "#ff0000");

	private static LuaTable TableWithImage(out LuaImage image)
	{
		image = NewImage();
		return new LuaTable { ["img"] = LuaValueConverter.ToLuaValue(image) };
	}

	[Fact]
	public void Image_IsDropped_WithoutConverters()
	{
		var table = TableWithImage(out _);

		var node = table.ToConstNodeValue(tolerant: true, converters: [])!;
		var dictionary = Assert.IsType<ConstNodeDictionaryValue>(node);

		Assert.False(dictionary.Items.ContainsKey("img"));
	}

	[Fact]
	public void Image_SurvivesRoundTrip()
	{
		var conv = new LuaImageNodeConverter();
		var table = TableWithImage(out var original);

		var node = table.ToReactiveNodeValue(tolerant: true, converters: [conv])!;
		var dumped = Assert.IsType<ConstNodeStringValue>(
			Assert.IsType<ConstNodeDictionaryValue>(node.ToConstNodeValue()).Items["img"]);
		Assert.StartsWith(LuaImageNodeConverter.ImageMarker, dumped.Value!);

		var revivedTable = Assert.IsType<LuaTable>(node.ToLuaValue(converters: [conv]));
		var revived = Assert.IsType<LuaUserData>(revivedTable.Get("img"));
		var image = Assert.IsType<LuaImage>(revived.Target);

		Assert.Equal(original.Width, image.Width);
		Assert.Equal(original.Height, image.Height);
		Assert.Equal(original.Format, image.Format);
	}

	[Fact]
	public void Image_DumpIsStable_UntilThePixelsChange()
	{
		var image = NewImage();
		var first = LuaImageNodeConverter.DumpImage(image);

		Assert.Equal(first, LuaImageNodeConverter.DumpImage(image));

		image.Resize(2, 2);

		Assert.NotEqual(first, LuaImageNodeConverter.DumpImage(image));
	}

	[Fact]
	public void StoreSnapshot_KeepsImages_AndRevivesThem()
	{
		var table = TableWithImage(out _);

		var dropped = new AppendOnlyList<string>();
		var snapshot = LuaStoreValueConverter.ToSnapshot(table, dropped);

		Assert.Empty(dropped);
		Assert.IsType<ConstNodeStringValue>(snapshot.Items["img"]);

		var restored = LuaStoreValueConverter.Restore(snapshot);
		var revived = Assert.IsType<LuaUserData>(restored.Get("img"));

		Assert.IsType<LuaImage>(revived.Target);
	}

	[Fact]
	public void WidgetSnapshot_KeepsAnImageArray_AndRevivesIt()
	{
		var state = new LuaState().LoadDefaultLibraries();
		var table = TableWithImage(out _);
		var gallery = LuaExtensions.CreateArrayTable();
		for (var i = 1; i <= 3; i++)
			gallery.Set(i, LuaValueConverter.ToLuaValue(NewImage()));
		table.Set("gallery", gallery);

		// Materializing the nested view model injects its helpers into the array table, which used to turn the
		// list into a dictionary in the snapshot (and back into an empty ItemsControl after a reload).
		var live = new LuaValueDynamicViewModel(table, state.CreateContext());
		using var liveGallery = Assert.IsAssignableFrom<StructuredDynamicViewModel>(live.GetDynamicMember("gallery"));
		Assert.Equal(3, liveGallery.Items.Count);

		var bson = table.ToReactiveNodeValue(tolerant: true).ToBsonValue();
		var restored = Assert.IsType<LuaTable>(bson.ToReactiveNodeValue()!.ToLuaValue());

		var galleryBack = Assert.IsType<LuaTable>(restored.Get("gallery"));
		Assert.Equal(3, galleryBack.Length);

		using var galleryVm = Assert.IsAssignableFrom<StructuredDynamicViewModel>(
			new LuaValueDynamicViewModel(restored, state.CreateContext()).GetDynamicMember("gallery"));
		Assert.Equal(3, galleryVm.Items.Count);
	}

	[Fact]
	public void UnreadablePayload_StaysAString()
	{
		var conv = new LuaImageNodeConverter();
		var text = LuaImageNodeConverter.ImageMarker + "png:not base64";
		var node = new ConstNodeStringValue { Value = text };

		var value = Assert.IsType<LuaString>(node.ToLuaValue(converters: [conv]));
		Assert.Equal(text, value.Value);
	}

}
