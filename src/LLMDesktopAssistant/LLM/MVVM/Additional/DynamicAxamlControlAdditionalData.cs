using System.ComponentModel;
using System.Globalization;
using AsyncLua.Values;
using LiteDB;
using LLMDesktopAssistant.MVVM;
using LLMDesktopAssistant.MVVM.Dynamic;
using LLMDesktopAssistant.Scripting.Lua;
using LLMDesktopAssistant.StructuredValues;
using LLMDesktopAssistant.StructuredValues.Converters;
using LLMDesktopAssistant.Utils;
using Microsoft.Extensions.DependencyInjection;

namespace LLMDesktopAssistant.LLM.MVVM.Additional
{
	/// <summary>
	/// Additional data of a tool call that renders a dynamic AXAML fragment bound to a dynamic view model.
	/// Produced by the Lua API <c>dass.ui.create_control()</c> + <c>dass.tool.result.append_data()</c>.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The live Lua view model is not serializable and is ignored by BSON. Instead the view model data is
	/// exposed as a computed <see cref="Data"/> BSON value, so the UI survives a chat reload. Lua functions
	/// are serialized into marker strings by the same conversion, and revived on load as
	/// <see cref="LuaNativeFunction"/>s - without their upvalues, which do not outlive their frame. A
	/// restored widget is therefore Lua-backed again, so a command still receives the data table as <c>self</c>.
	/// </para>
	/// <para>
	/// Without the chat's service scope there is no Lua context to revive the functions into, and the data
	/// falls back to a <see cref="NodeValueDynamicViewModel"/> with no commands (the functions stay markers, so
	/// the next save writes them back unchanged).
	/// </para>
	/// <para>
	/// Only top-level view model changes are observed (the dynamic view model raises them itself); nested
	/// changes do not propagate and are not persisted until a top-level change happens.
	/// </para>
	/// </remarks>
	[ViewModelFor(typeof(DynamicAxamlControlAdditionalDataView))]
	public class DynamicAxamlControlAdditionalData : AdditionalChatData, IChatServicesAttachable
	{
		private string _axaml = string.Empty;
		/// <summary>
		/// Gets or sets the AXAML markup rendered by this additional data.
		/// </summary>
		public string Axaml
		{
			get => _axaml;
			set => SetProperty(ref _axaml, value);
		}

		private BsonValue? _loadedBson;
		/// <summary>
		/// Gets or sets the persisted view model data.
		/// The getter is computed from the live view model when present, and falls back to the value loaded
		/// from the database otherwise. The setter is used by the database loader only.
		/// </summary>
		[ChangeTracker.Untracked]
		public BsonValue Data
		{
			get => _vm is null ? (_loadedBson ?? BsonValue.Null) : BuildBsonFromViewModel(_vm);
			set => _loadedBson = value;
		}

		[BsonIgnore]
		private DynamicViewModel? _vm;

		[BsonIgnore]
		private bool _ownsViewModel;

		[BsonIgnore]
		private IServiceProvider? _services;

		/// <inheritdoc/>
		public void AttachChatServices(IServiceProvider services)
		{
			ArgumentNullException.ThrowIfNull(services);
			_services = services;

			// A view model built before the scope was known is node-backed and carries no commands: rebuild it,
			// so the restored widget gets its functions back. A live (not owned) view model is already Lua-backed.
			if (!_ownsViewModel || _vm is not NodeValueDynamicViewModel)
				return;

			_vm.PropertyChanged -= OnViewModelPropertyChanged;
			(_vm as IDisposable)?.Dispose();
			_vm = null;
			RaisePropertyChanged(nameof(ViewModel));
		}

		/// <summary>
		/// Gets the dynamic view model rendered against <see cref="Axaml"/>.
		/// Created lazily from the persisted <see cref="Data"/> when no live view model is attached.
		/// </summary>
		[ChangeTracker.Untracked]
		[BsonIgnore]
		public DynamicViewModel ViewModel
		{
			get
			{
				if (_vm is null)
				{
					_vm = CreateViewModel();
					_ownsViewModel = true;
					_vm.PropertyChanged += OnViewModelPropertyChanged;
				}
				return _vm;
			}
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="DynamicAxamlControlAdditionalData"/> class.
		/// Used by the database loader.
		/// </summary>
		public DynamicAxamlControlAdditionalData()
		{
		}

		/// <summary>
		/// Initializes a new live instance bound to the given AXAML and view model.
		/// </summary>
		/// <param name="axaml">The AXAML markup to render.</param>
		/// <param name="viewModel">The live dynamic view model (shared between multiple appends of the same handle).</param>
		public DynamicAxamlControlAdditionalData(string axaml, DynamicViewModel viewModel)
		{
			Axaml = axaml;
			_vm = viewModel;
			_ownsViewModel = false;
			_vm.PropertyChanged += OnViewModelPropertyChanged;
		}

		private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
		{
			// Shallow change: refresh every tracked member so the database synchronizer persists a new snapshot.
			RaisePropertyChanged(null);
		}

		private DynamicViewModel CreateViewModel()
		{
			if (_loadedBson is null)
				return new DictionaryDynamicViewModel();

			var node = _loadedBson.ToReactiveNodeValue();
			if (node is null)
				return new DictionaryDynamicViewModel();

			if (TryCreateLuaViewModel(node) is { } luaViewModel)
				return luaViewModel;

			return new NodeValueDynamicViewModel(node);
		}

		/// <summary>
		/// Rebuilds the persisted data as a Lua-backed view model, reviving the serialized commands.
		/// </summary>
		/// <param name="node">The persisted view model data.</param>
		/// <returns>The Lua view model, or <see langword="null"/> when no chat scope is attached.</returns>
		/// <remarks>
		/// A restored command is invoked with the data table as <c>self</c>, and that table is the only state a
		/// revived function still has - so the widget has to be Lua-backed, not merely node-backed.
		/// </remarks>
		private LuaValueDynamicViewModel? TryCreateLuaViewModel(INodeValue node)
		{
			var luaService = _services?.GetService<LuaService>();
			if (luaService is null)
				return null;

			if (node.ToLuaValue() is not LuaTable table)
				return null;

			var context = luaService.GetState().CreateContext();
			return new LuaValueDynamicViewModel(table, context);
		}

		private static BsonValue BuildBsonFromViewModel(DynamicViewModel viewModel)
		{
			return viewModel switch
			{
				LuaValueDynamicViewModel lua => lua.Data.ToReactiveNodeValue(tolerant: true).ToBsonValue(),
				NodeValueDynamicViewModel node => node.Root.ToBsonValue(),
				DictionaryDynamicViewModel dictionary => BuildBsonFromDictionary(dictionary.GetSnapshot()),
				_ => BsonValue.Null,
			};
		}

		private static BsonValue BuildBsonFromDictionary(IReadOnlyDictionary<string, object?> items)
		{
			var document = new BsonDocument();
			foreach (var (key, value) in items)
				document[key] = ClrToBson(value);
			return document;
		}

		private static BsonValue BuildBsonFromList(System.Collections.IEnumerable items)
		{
			var array = new BsonArray();
			foreach (var item in items)
				array.Add(ClrToBson(item));
			return array;
		}

		private static BsonValue ClrToBson(object? value) => value switch
		{
			null => BsonValue.Null,
			bool boolean => new BsonValue(boolean),
			string text => new BsonValue(text),
			sbyte or byte or short or ushort or int or uint or long or ulong or float or double or decimal
				=> new BsonValue(Convert.ToDouble(value, CultureInfo.InvariantCulture)),
			IReadOnlyDictionary<string, object?> map => BuildBsonFromDictionary(map),
			System.Collections.IEnumerable list when value is not string => BuildBsonFromList(list),
			_ => new BsonValue(value.ToString() ?? string.Empty),
		};

		protected override void Dispose(bool disposing)
		{
			base.Dispose(disposing);

			if (disposing && _vm is not null)
			{
				_vm.PropertyChanged -= OnViewModelPropertyChanged;
				if (_ownsViewModel)
					(_vm as IDisposable)?.Dispose();
				_vm = null;
			}
		}
	}
}
