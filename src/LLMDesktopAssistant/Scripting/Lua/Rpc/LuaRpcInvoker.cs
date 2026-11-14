using System.Text.Json.Nodes;
using AsyncLua;
using AsyncLua.Values;
using LLMDesktopAssistant.StructuredValues.Converters;
using LLMDesktopAssistant.Utils.Rpc;

namespace LLMDesktopAssistant.Scripting.Lua.Rpc
{
	/// <summary>
	/// The <c>rpc_invoker</c> userdata a handle exposes as <c>invoke</c> and <c>notify</c>: it turns a method
	/// name chosen at call time into a request or a notification.
	/// </summary>
	/// <remarks>
	/// The same object serves <c>invoke</c> and <c>notify</c>, and it is a userdata rather than a table so
	/// that both spellings work: <c>h.invoke:method(x)</c> and <c>h.invoke["a.b"](x)</c> index the proxy,
	/// <c>h.invoke("a.b", x)</c> calls it. Every spelling receives the proxy as its first argument and
	/// drops it, so the handler signature is the same. No state lives in the proxy: the method name is
	/// captured by the closure the index returns.
	/// </remarks>
	public sealed class LuaRpcInvoker(LuaRpcHandle handle, bool isNotification = false)
	{
		private static LuaTable? _metatable;

		/// <summary>
		/// Gets the handle every call on this proxy is sent through.
		/// </summary>
		public LuaRpcHandle Handle => handle;

		/// <summary>
		/// Gets a value indicating whether calls on this proxy are sent as notifications rather than requests.
		/// </summary>
		public bool IsNotification => isNotification;

		/// <summary>
		/// Wraps the proxy into an <c>rpc_invoker</c> userdata.
		/// </summary>
		/// <param name="invoker">The proxy to wrap.</param>
		/// <returns>The userdata, sharing one metatable across every proxy.</returns>
		[LuaHidden]
		public static LuaUserData Wrap(LuaRpcInvoker invoker)
			=> new(invoker, invoker.IsNotification ? "rpc_notifier" : "rpc_invoker") { Metatable = GetMetatable() };

		private static LuaTable GetMetatable()
		{
			// Two threads may build one each; the last wins and both are equivalent.
			return _metatable ??= BuildMetatable();
		}

		private static LuaTable BuildMetatable()
		{
			var metatable = new LuaTable();
			metatable.Set("__index", new LuaCallbackFunction(Index));
			metatable.Set("__call", new LuaCallbackFunction(CallAsync));
			return metatable;
		}

		/// <summary>
		/// Handles <c>h.invoke:method(x)</c> and <c>h.invoke["a.b"](x)</c>.
		/// </summary>
		/// <remarks>
		/// Returns a function rather than the result: the index metamethod cannot await, and Lua calls the
		/// value it returns with the proxy as its first argument.
		/// </remarks>
		private static LuaTuple Index(LuaCallingContext ctx, LuaValue[] args)
		{
			if (args.Length < 2 ||
				args[0] is not LuaUserData { Target: LuaRpcInvoker invoker } ||
				args[1] is not LuaString methodValue)
			{
				return LuaTuple.Empty;
			}

			var method = methodValue.Value;

			return new LuaTuple(new LuaCallbackFunction(
				(callCtx, callArgs) => InvokeAsync(invoker, method, 1, callArgs, callCtx)));
		}

		/// <summary>
		/// Handles <c>h.invoke("a.b", x)</c>.
		/// </summary>
		private static Task<LuaTuple> CallAsync(LuaCallingContext ctx, LuaValue[] args)
		{
			if (args.Length < 2 ||
				args[0] is not LuaUserData { Target: LuaRpcInvoker invoker } ||
				args[1] is not LuaString methodValue)
			{
				throw new LuaRuntimeException(
					"rpc invoke(method, [params]): the first argument must be a string (method name).");
			}

			// The proxy sits at 1, the method name at 2, the parameters at 3.
			return InvokeAsync(invoker, methodValue.Value, 2, args, ctx);
		}

		/// <summary>
		/// Sends the request and turns its failures into Lua errors.
		/// </summary>
		/// <param name="invoker">The proxy the call went through.</param>
		/// <param name="method">The method name.</param>
		/// <param name="parametersIndex">Where the parameters sit: 1 for the indexed closure form, 2 for the call form.</param>
		/// <param name="args">The call arguments, always starting with the proxy.</param>
		/// <param name="ctx">The calling context, whose token cancels the call.</param>
		private static async Task<LuaTuple> InvokeAsync(
			LuaRpcInvoker invoker,
			string method,
			int parametersIndex,
			LuaValue[] args,
			LuaCallingContext ctx)
		{
			var parameters = args.Length > parametersIndex && args[parametersIndex] is not LuaNil
				? args[parametersIndex].ToConstNodeValue(tolerant: true)?.ToJsonNode()
				: null;

			try
			{
				if (invoker.IsNotification)
				{
					await invoker.Handle
						.NotifyAsync(method, parameters, ctx.CancellationToken)
						.ConfigureAwait(false);

					return LuaTuple.Empty;
				}

				var result = await invoker.Handle
					.InvokeAsync(method, parameters, ctx.CancellationToken)
					.ConfigureAwait(false);

				return new LuaTuple(result.ToConstNodeValue().ToLuaValue());
			}
			catch (JsonRpcException ex)
			{
				throw ToLuaError(ex);
			}
			catch (TimeoutException ex)
			{
				throw new LuaRuntimeException($"rpc invoke('{method}'): {ex.Message}");
			}
			catch (OperationCanceledException) when (ctx.CancellationToken.IsCancellationRequested)
			{
				throw;
			}
			catch (Exception ex)
			{
				throw new LuaRuntimeException($"rpc invoke('{method}'): {ex.Message}");
			}
		}

		/// <summary>
		/// Turns a JSON-RPC error into a Lua error whose value is a table, so <c>catch e</c> can read
		/// <c>e.code</c> and <c>e.data</c>.
		/// </summary>
		/// <param name="exception">The error received from the peer.</param>
		internal static LuaRuntimeException ToLuaError(JsonRpcException exception)
		{
			var table = new LuaTable
			{
				["code"] = new LuaNumber(exception.Code),
				["message"] = new LuaString(exception.Message)
			};

			if (exception.Data is { } data)
				table["data"] = data.ToConstNodeValue().ToLuaValue();

			return new LuaRuntimeException(table, addPosition: false);
		}
	}
}
