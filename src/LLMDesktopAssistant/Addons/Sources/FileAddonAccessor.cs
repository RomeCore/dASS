using System.Collections.Specialized;
using LLMDesktopAssistant.Addons.Management;
using LLMDesktopAssistant.Utils;

namespace LLMDesktopAssistant.Addons.Sources
{
	public class FileAddonAccessor<T> : Disposable, IFileAddonAccessor<T>
	{
		private readonly RangeObservableCollection<T> _addons = [];
		private readonly IReactiveAddonLoader<T>[] _loaders;
		private readonly IAddonManagerInvalidator _invalidator;
		private readonly AddonKind _kind;

		public ReadOnlyObservableCollection<T> Addons
		{
			get
			{
				_invalidator.ReloadIfInvalid(_kind);
				return field ??= new(_addons);
			}
		}

		public FileAddonAccessor(IEnumerable<IReactiveAddonLoader<T>> loaders,
			IAddonManagerInvalidator invalidator, IEnumerable<IAddonTypeDescriptor> descriptors)
		{
			_loaders = [.. loaders];
			_invalidator = invalidator;
			_kind = descriptors.First(d => d.ClrType == typeof(T)).Kind;

			foreach (var loader in _loaders)
			{
				loader.Addons.CollectionChanged += Addons_CollectionChanged;
				Addons_CollectionChanged(null, new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Add, loader.Addons));
			}
		}

		protected override void Dispose(bool disposing)
		{
			base.Dispose(disposing);

			if (disposing)
			{
				foreach (var loader in _loaders)
				{
					loader.Addons.CollectionChanged -= Addons_CollectionChanged;
				}
				_addons.Clear();
			}
		}

		private void Addons_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
		{
			if (e.Action is NotifyCollectionChangedAction.Move)
				return;

			if (e.OldItems is not null)
				foreach (var _item in e.OldItems)
					if (_item is T item)
						_addons.Remove(item);

			if (e.NewItems is not null)
				foreach (var _item in e.NewItems)
					if (_item is T item)
						_addons.Add(item);
		}
	}
}
