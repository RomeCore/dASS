using System.Diagnostics.CodeAnalysis;
using LLMDesktopAssistant.Utils;

namespace LLMDesktopAssistant.LLM.MVVM.Additional
{
	public class AdditionalChatDataCollection : RangeObservableOrderedCollection<AdditionalChatData>
	{
		internal static bool RaiseInUIThreadGlobal { get; set; } = true;

		public AdditionalChatDataCollection()
		{
			RaiseInUIThread = RaiseInUIThreadGlobal;
			Comparer = AdditionalViewModelComparer.Instance;
		}

		/// <summary>
		/// Tries to get the first additional view model of a specific type.
		/// </summary>
		/// <typeparam name="T">The type of the additional view model to get.</typeparam>
		/// <returns>The first additional view model of the specified type, or null if none is found.</returns>
		public bool Has<T>() where T : AdditionalChatData
		{
			lock (SyncRoot)
				for (int i = 0; i < Items.Count; i++)
					if (Items[i] is T)
						return true;
			return false;
		}

		/// <summary>
		/// Tries to get the first additional view model of a specific type.
		/// </summary>
		/// <typeparam name="T">The type of the additional view model to get.</typeparam>
		/// <returns>The first additional view model of the specified type, or null if none is found.</returns>
		public T? TryGet<T>() where T : AdditionalChatData
		{
			lock (SyncRoot)
				for (int i = 0; i < Items.Count; i++)
					if (Items[i] is T viewModel)
						return viewModel;
			return null;
		}

		/// <summary>
		/// Tries to get the first additional view model of a specific type.
		/// </summary>
		/// <typeparam name="T">The type of the additional view model to get.</typeparam>
		/// <param name="viewModel">The output parameter that will contain the first additional view model of the specified type, or null if none is found.</param>
		/// <returns>True if the first additional view model of the specified type was found and assigned to the output parameter, otherwise false.</returns>
		public bool TryGet<T>([NotNullWhen(true)] out T viewModel) where T : AdditionalChatData
		{
			lock (SyncRoot)
				for (int i = 0; i < Items.Count; i++)
					if (Items[i] is T _viewModel)
					{
						viewModel = _viewModel;
						return true;
					}
			viewModel = null!;
			return false;
		}

		/// <summary>
		/// Gets all additional view models of a specific type.
		/// </summary>
		/// <typeparam name="T">The type of the additional view models to get.</typeparam>
		/// <returns>A collection of all additional view models of the specified type.</returns>
		public List<T> GetAll<T>() where T : AdditionalChatData
		{
			var list = new List<T>();
			lock (SyncRoot)
				for (int i = 0; i < Items.Count; i++)
					if (Items[i] is T viewModel)
						list.Add(viewModel);
			return list;
		}

		/// <summary>
		/// Gets the message parts that are restored into the input draft when the message is edited:
		/// every <see cref="AdditionalMessagePart"/> whose <see cref="AdditionalMessagePart.IsRestorable"/> is set.
		/// </summary>
		/// <returns>The restorable message parts, in collection order.</returns>
		public IEnumerable<AdditionalMessagePart> GetRestorableParts()
		{
			return this.OfType<AdditionalMessagePart>().Where(part => part.IsRestorable);
		}

		/// <summary>
		/// Replaces the first additional view model of a specific type with a new one.
		/// If not found, the new view model will be added to the collection.
		/// </summary>
		/// <typeparam name="T">The type of the additional view model to replace.</typeparam>
		/// <param name="viewModel">The new additional view model to replace the existing one with.</param>
		public void TryReplace<T>(T viewModel) where T : AdditionalChatData
		{
			int replaceIndex = -1;

			lock (SyncRoot)
			{
				for (int i = 0; i < Items.Count; i++)
					if (Items[i] is T oldItem)
					{
						replaceIndex = i;
						break;
					}
			}

			if (replaceIndex != -1)
				Set(replaceIndex, viewModel);
			else
				Add(viewModel);
		}
	}
}