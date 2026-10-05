using LiteDB;

namespace LLMDesktopAssistant.Controls.Icons
{
	/// <summary>
	/// Registers the LiteDB/BSON representation of <see cref="VisualIconKind"/>: the canonical
	/// string form (<c>&lt;pack&gt;:&lt;data&gt;</c>), or BSON <c>null</c> for
	/// <see cref="VisualIconKind.None"/>.
	/// </summary>
	public static class VisualIconKindBsonSerializer
	{
		/// <summary>Registers the serializer on the given mapper.</summary>
		public static void Register(BsonMapper mapper)
		{
			mapper.RegisterType<VisualIconKind>(
				icon => icon.IsNone ? BsonValue.Null : new BsonValue(icon.ToString()),
				bson => bson.IsNull ? VisualIconKind.None : VisualIconKind.Parse(bson.AsString));
		}
	}
}
