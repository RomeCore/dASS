using System.Collections.Specialized;
using LLMDesktopAssistant.Data;
using LLMDesktopAssistant.Data.ChatModels;
using LLMDesktopAssistant.LLM.Domain;
using LLMDesktopAssistant.LLM.MVVM.Additional;
using LLMDesktopAssistant.Utils;

namespace LLMDesktopAssistant.LLM.Services.Storage
{
	/// <summary>
	/// Keeps one <see cref="AdditionalChatDataCollection"/> (of a message, tool call or chat)
	/// in sync with the rows of <see cref="ChatDatabase.AdditionalChatData"/> for a single parent:
	/// owns one <see cref="AdditionalChatDataSynchronizer"/> per item and manages their lifecycle.
	/// </summary>
	public class AdditionalChatDataCollectionSynchronizer : Disposable
	{
		private readonly ChatDatabase _database;
		private readonly AdditionalChatDataCollection _collection;
		private readonly ChatDataParentKind _parentKind;
		private readonly int _parentId;
		private readonly Dictionary<AdditionalChatData, AdditionalChatDataSynchronizer> _syncs = [];

		/// <summary>
		/// For a BRAND NEW parent: inserts rows for the items already in the collection
		/// and subscribes to all further changes.
		/// </summary>
		public static AdditionalChatDataCollectionSynchronizer FromTarget(ChatDatabase database, ChatObjectBase owner,
			ChatDataParentKind parentKind, int parentId)
		{
			var syncs = new Dictionary<AdditionalChatData, AdditionalChatDataSynchronizer>();

			foreach (var data in owner.AdditionalData)
				syncs[data] = AdditionalChatDataSynchronizer.FromTarget(database, data, parentKind, parentId);

			return new AdditionalChatDataCollectionSynchronizer(database, owner, parentKind, parentId, syncs);
		}

		/// <summary>
		/// For a parent LOADED from the database: loads its rows into the collection and subscribes.
		/// </summary>
		public static AdditionalChatDataCollectionSynchronizer FromOwnerModels(ChatDatabase database, ChatObjectBase owner,
			ChatDataParentKind parentKind, int parentId)
		{
			var loaded = new List<AdditionalChatData>();
			var syncs = new Dictionary<AdditionalChatData, AdditionalChatDataSynchronizer>();

			// Осторожно ребятки: если поменять оба выражения в Find местами, то будет пиздос
			// (x45 медленнее загрузка чата станет, а я с этим месяц жил и очень долго ждал).
			foreach (var model in database.AdditionalChatData
				.Find(d => d.ParentId == parentId && d.ParentKind == parentKind)
				.OrderBy(d => d.Order).ThenBy(d => d.Id))
			{
				var sync = AdditionalChatDataSynchronizer.FromModel(database, model);
				syncs[sync.Target] = sync;
				loaded.Add(sync.Target);
			}

			var synchronizer = new AdditionalChatDataCollectionSynchronizer(database, owner, parentKind, parentId, syncs);
			owner.AdditionalData.Reset(loaded);
			return synchronizer;
		}

		private AdditionalChatDataCollectionSynchronizer(ChatDatabase database, ChatObjectBase owner,
			ChatDataParentKind parentKind, int parentId, Dictionary<AdditionalChatData, AdditionalChatDataSynchronizer> syncs)
		{
			_database = database;
			_collection = owner.AdditionalData;
			_parentKind = parentKind;
			_parentId = parentId;
			_syncs = syncs;

			owner.AdditionalData.CollectionChanged += OnCollectionChanged;
		}

		private void OnCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
		{
			if (e.OldItems != null)
				foreach (AdditionalChatData oldData in e.OldItems)
					if (_syncs.Remove(oldData, out var sync))
						sync.Delete();

			if (e.NewItems != null)
				foreach (AdditionalChatData newData in e.NewItems)
					if (!_syncs.ContainsKey(newData)) // idempotent: collection events can arrive deferred
						_syncs[newData] = AdditionalChatDataSynchronizer.FromTarget(_database, newData, _parentKind, _parentId);

			int i = 0;
			foreach (var item in _collection)
				if (_syncs.TryGetValue(item, out var sync))
					sync.UpdateOrder(i++);
		}

		/// <summary>
		/// Deletes ALL rows of this parent and disposes this synchronizer.
		/// Called when the parent itself is deleted.
		/// </summary>
		public void DeleteAll()
		{
			ThrowIfDisposed();

			foreach (var sync in _syncs.Values)
				sync.Delete();
			_syncs.Clear();

			Dispose();
		}

		/// <summary>
		/// Removes all additional data rows of a parent that has no domain object / active synchronizer
		/// (for cascade deletes).
		/// </summary>
		public static void DeleteFromDatabase(ChatDatabase database, ChatDataParentKind parentKind, int parentId)
		{
			database.AdditionalChatData.DeleteMany(d => d.ParentKind == parentKind && d.ParentId == parentId);
		}

		protected override void Dispose(bool disposing)
		{
			base.Dispose(disposing);

			if (disposing)
			{
				_collection.CollectionChanged -= OnCollectionChanged;

				foreach (var sync in _syncs.Values)
					sync.Dispose();
				_syncs.Clear();
			}
		}
	}
}