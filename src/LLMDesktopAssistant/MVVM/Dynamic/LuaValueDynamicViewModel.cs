using System.Globalization;
using System.Runtime.CompilerServices;
using AsyncLua;
using AsyncLua.Values;
using CommunityToolkit.Mvvm.Input;
using Serilog;

namespace LLMDesktopAssistant.MVVM.Dynamic
{
	/// <summary>
	/// A dynamic view model over a Lua table. String keys become members, the array part is exposed
	/// through the indexer and the <see cref="StructuredDynamicViewModel.Items"/> view, and Lua
	/// functions become asynchronous commands.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The input table is the single source of truth: the view model reads and writes it directly.
	/// On construction two helper functions are injected into the table:
	/// <c>data:set(name, value)</c> writes a value (and fires <c>__changed</c>) and
	/// <c>data:notify(name)</c> re-raises a change for a value the script modified itself.
	/// </para>
	/// <para>
	/// Reactivity towards Lua is driven by the optional <c>__changed(self, name, old, new)</c>
	/// function stored in the table. It is invoked (via the captured <see cref="LuaCallingContext"/>)
	/// whenever the view model itself writes a value, but never for <c>notify</c>.
	/// </para>
	/// <para>
	/// Lua functions exposed as commands are invoked with method semantics: the table itself is
	/// passed as <c>self</c> (matching colon-call syntax), followed by the command parameter when
	/// one is supplied. A function is thus expected to be declared like <c>function(self, ...)</c>.
	/// </para>
	/// </remarks>
	public class LuaValueDynamicViewModel : StructuredDynamicViewModel
	{
		/// <summary>The name of the Lua callback invoked when a value is changed by the view model.</summary>
		public const string ChangedKey = "__changed";

		/// <summary>The name of the helper function injected into the table to write a value.</summary>
		public const string SetKey = "set";

		/// <summary>The name of the helper function injected into the table to re-raise a change.</summary>
		public const string NotifyKey = "notify";

		private static readonly IReadOnlySet<string> ServiceKeys =
			new HashSet<string>(StringComparer.Ordinal) { ChangedKey, SetKey, NotifyKey };

		private readonly ConditionalWeakTable<LuaFunction, IAsyncRelayCommand<object?>> _commandCache = new();

		/// <summary>
		/// Initializes a new instance of the <see cref="LuaValueDynamicViewModel"/> class.
		/// </summary>
		/// <param name="data">The Lua table exposed as a view model.</param>
		/// <param name="context">The calling context used to invoke Lua callbacks and commands.</param>
		public LuaValueDynamicViewModel(LuaTable data, LuaCallingContext context)
		{
			Data = data ?? throw new ArgumentNullException(nameof(data));
			Context = context ?? throw new ArgumentNullException(nameof(context));
			InjectHelpers();
		}

		/// <summary>Gets the underlying Lua table.</summary>
		public LuaTable Data { get; }

		/// <summary>Gets the calling context used to invoke Lua callbacks.</summary>
		public LuaCallingContext Context { get; }

		/// <inheritdoc/>
		protected override IReadOnlySet<string> HiddenKeys => ServiceKeys;

		/// <inheritdoc/>
		protected override IEnumerable<string> EnumerateDataKeys()
		{
			foreach (var key in Data.Keys)
			{
				if (key is LuaString text && !ServiceKeys.Contains(text.Value))
					yield return text.Value;
			}
		}

		/// <inheritdoc/>
		protected override int CountEntries()
		{
			// TODO: Count currently reports the total number of data entries (service keys excluded).
			// Perhaps it should report the array length instead? Revisit once real usage shows up.
			var count = Data.Count;
			foreach (var hidden in ServiceKeys)
			{
				if (Data.ContainsKey(new LuaString(hidden)))
					count--;
			}

			return count;
		}

		/// <inheritdoc/>
		protected override bool TryGetRawValue(string key, out object? raw)
		{
			var value = TryParseIndex(key, out var index) ? Data.Get(index + 1) : Data.Get(key);
			if (value is LuaNil)
			{
				raw = null;
				return false;
			}

			raw = value;
			return true;
		}

		/// <inheritdoc/>
		protected override void SetRawValue(string key, object? raw)
		{
			var value = raw as LuaValue ?? LuaNil.Instance;
			if (TryParseIndex(key, out var index))
				Data.Set(index + 1, value);
			else
				Data.Set(key, value);
		}

		/// <inheritdoc/>
		protected override ValueKind Classify(object? raw)
		{
			return raw switch
			{
				null or LuaNil => ValueKind.Null,
				LuaTable => ValueKind.Container,
				LuaFunction => ValueKind.Function,
				_ => ValueKind.Scalar,
			};
		}

		/// <inheritdoc/>
		protected override object? ScalarToClr(object? raw)
		{
			return ((LuaValue)raw!).ToClrObject(typeof(object));
		}

		/// <inheritdoc/>
		protected override object? ClrToRaw(object? value)
		{
			return LuaValueConverter.ToLuaValue(value);
		}

		/// <inheritdoc/>
		protected override bool RawEquals(object? left, object? right)
		{
			if (left is null)
				return right is null or LuaNil;
			if (right is null)
				return left is LuaNil;
			return Equals(left, right);
		}

		/// <inheritdoc/>
		protected override StructuredDynamicViewModel CreateChildViewModel(object? raw)
		{
			return new LuaValueDynamicViewModel((LuaTable)raw!, Context);
		}

		/// <inheritdoc/>
		protected override object? CreateCommandFor(object? raw)
		{
			return raw is LuaFunction function ? _commandCache.GetValue(function, CreateCommand) : null;
		}

		/// <inheritdoc/>
		protected override bool TryGetArrayRawItem(int index, out object? raw)
		{
			if (index < 0 || index >= Data.Length)
			{
				raw = null;
				return false;
			}

			var value = Data.Get(index + 1);
			if (value is LuaNil)
			{
				raw = null;
				return false;
			}

			raw = value;
			return true;
		}

		/// <inheritdoc/>
		protected override int GetArrayLength() => Data.Length;

		/// <inheritdoc/>
		protected override void OnMemberWritten(string key, object? oldRaw, object? newRaw)
		{
			var handler = Data.Get(ChangedKey);
			if (handler is not LuaFunction function)
				return;

			_ = InvokeChangedAsync(
				function,
				KeyToLua(key),
				oldRaw as LuaValue ?? LuaNil.Instance,
				newRaw as LuaValue ?? LuaNil.Instance);
		}

		private void InjectHelpers()
		{
			Data.Set(SetKey, new LuaCallbackFunction((_, args) =>
			{
				if (args.Length >= 3)
					SetDynamicMember(NameToKey(args[1]), args[2]);
				return LuaTuple.Empty;
			}, SetKey));

			Data.Set(NotifyKey, new LuaCallbackFunction((_, args) =>
			{
				if (args.Length >= 2)
					NotifyMemberChanged(NameToKey(args[1]));
				return LuaTuple.Empty;
			}, NotifyKey));
		}

		private IAsyncRelayCommand<object?> CreateCommand(LuaFunction function)
		{
			return new AsyncRelayCommand<object?>(async parameter =>
			{
				try
				{
					// Method semantics: pass the owner table as `self`, then the command parameter when present.
					var args = parameter is null
						? new LuaValue[] { Data }
						: new LuaValue[] { Data, LuaValueConverter.ToLuaValue(parameter) };
					await function.InvokeAsync(Context, args).ConfigureAwait(false);
				}
				catch (Exception ex)
				{
					Log.Error(ex, "Lua command '{Name}' failed", function);
				}
			});
		}

		private async Task InvokeChangedAsync(LuaFunction function, LuaValue name, LuaValue oldValue, LuaValue newValue)
		{
			try
			{
				await function.InvokeAsync(Context, Data, name, oldValue, newValue).ConfigureAwait(false);
			}
			catch (Exception ex)
			{
				Log.Error(ex, "Lua '__changed' callback failed");
			}
		}

		private static bool TryParseIndex(string key, out int index)
		{
			return int.TryParse(key, NumberStyles.None, CultureInfo.InvariantCulture, out index);
		}

		private static string NameToKey(LuaValue name)
		{
			return name switch
			{
				LuaString text => text.Value,
				LuaNumber number when number.Value >= 1 && number.Value == Math.Truncate(number.Value)
					=> ((int)number.Value - 1).ToString(CultureInfo.InvariantCulture),
				_ => name.ToString(),
			};
		}

		private static LuaValue KeyToLua(string key)
		{
			return TryParseIndex(key, out var index)
				? new LuaNumber(index + 1)
				: new LuaString(key);
		}
	}
}
