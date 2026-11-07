using LiteDB;
using LLMDesktopAssistant.LLM.MVVM.Additional;
using LLMDesktopAssistant.Utils;

namespace LLMDesktopAssistant.Scripting.Lua.Store
{
	/// <summary>
	/// Holds the <c>store.chat</c> keys of one chat as a data-only additional chat data row.
	/// </summary>
	/// <remarks>
	/// The whole store lives in a single BSON document, so every change rewrites the row. Values that
	/// cannot be represented as BSON are dropped before they get here - see <see cref="LuaStoreValueConverter"/>.
	/// </remarks>
	public class LuaStoreChatData : AdditionalChatData
	{
		/// <summary>
		/// Initializes a new instance of the <see cref="LuaStoreChatData"/> class.
		/// Used by the database loader.
		/// </summary>
		public LuaStoreChatData()
		{
			IsVisible = false;
		}

		/// <summary>
		/// Gets or sets the persisted keys of the store.
		/// </summary>
		[ChangeTracker.Untracked]
		public BsonValue Values
		{
			get => field ??= new BsonDocument();
			set => SetProperty(ref field, value);
		}
	}
}
