using System.Text.Json.Nodes;
using AsyncLua;
using AsyncLua.Values;
using LLMDesktopAssistant.StructuredValues.Converters;
using LLMDesktopAssistant.Utils.Rpc;

namespace LLMDesktopAssistant.Scripting.Lua.Rpc
{
	/// <summary>
	/// Routes the messages a peer initiates into a Lua table that the script owns.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The table is the source of truth and is read on every message, not copied at connect time, so a
	/// script can subscribe to a notification later by assigning the key. A numeric key <c>[1]</c> is the
	/// fallback and receives every method no named key covers.
	/// </para>
	/// <para>
	/// Every handler also receives the connection the message arrived on, so one routing table can serve
	/// many peers: <c>function(params, connection)</c> by name, <c>function(method, params, connection)</c>
	/// for the fallback.
	/// </para>
	/// <para>
	/// A gate is passed by listeners only: the handlers of one listener share a single chat Lua state, and
	/// two peers must not interleave in it. A client handle has a peer that is already serialized by its
	/// own read loop, so it passes none.
	/// </para>
	/// </remarks>
	public sealed class LuaRpcRouter(
		LuaTable routing,
		LuaCallingContext context,
		SemaphoreSlim? gate = null) : IJsonRpcMessageHandler
	{
		/// <summary>
		/// Gets the routing table this router reads.
		/// </summary>
		public LuaTable Routing => routing;

		/// <summary>
		/// Gets or sets the connection this router answers on.
		/// </summary>
		/// <remarks>
		/// Assigned after construction: a handle owns its router and the router has to hand that same handle
		/// to Lua, so one of the two has to come second.
		/// </remarks>
		public LuaRpcHandle? Connection { get; set; }

		/// <inheritdoc/>
		public async Task<JsonNode?> HandleRequestAsync(
			IJsonRpcPeer peer,
			string method,
			JsonNode? parameters,
			CancellationToken cancellationToken = default)
		{
			var handler = Resolve(method, out var isFallback)
				?? throw new JsonRpcException(RpcErrorCodes.MethodNotFound, $"No handler is routed for '{method}'.");

			try
			{
				var returned = await InvokeAsync(handler, method, parameters, isFallback, cancellationToken)
					.ConfigureAwait(false);

				return ToJson(returned.Count > 0 ? returned[0] : LuaNil.Instance);
			}
			catch (LuaRuntimeException ex)
			{
				throw Translate(ex);
			}
		}

		/// <inheritdoc/>
		public async Task HandleNotificationAsync(
			IJsonRpcPeer peer,
			string method,
			JsonNode? parameters,
			CancellationToken cancellationToken = default)
		{
			var handler = Resolve(method, out var isFallback)
				?? throw new JsonRpcException(RpcErrorCodes.MethodNotFound, $"No handler is routed for '{method}'.");

			try
			{
				await InvokeAsync(handler, method, parameters, isFallback, cancellationToken).ConfigureAwait(false);
			}
			catch (LuaRuntimeException ex)
			{
				throw Translate(ex);
			}
		}

		/// <summary>
		/// Finds the handler for a method: its own key first, the numeric fallback second.
		/// </summary>
		/// <param name="method">The method name.</param>
		/// <param name="isFallback">Whether the fallback was chosen.</param>
		/// <returns>The handler, or <see langword="null"/> when the table covers neither.</returns>
		private LuaFunction? Resolve(string method, out bool isFallback)
		{
			isFallback = false;

			if (routing.Get(method) is LuaFunction named)
				return named;

			if (routing.Get(1) is LuaFunction fallback)
			{
				isFallback = true;
				return fallback;
			}

			return null;
		}

		/// <summary>
		/// Calls a handler: <c>function(params, connection)</c> by name,
		/// <c>function(method, params, connection)</c> for the fallback.
		/// </summary>
		private async Task<LuaTuple> InvokeAsync(
			LuaFunction handler,
			string method,
			JsonNode? parameters,
			bool isFallback,
			CancellationToken cancellationToken)
		{
			var arguments = parameters.ToConstNodeValue().ToLuaValue();
			LuaValue connection = Connection is null ? LuaNil.Instance : LuaRpcHandle.Wrap(Connection);
			var args = isFallback
				? new LuaValue[] { new LuaString(method), arguments, connection }
				: [arguments, connection];

			if (gate is null)
			{
				// The call's own token belongs to that one execution and is dead by now.
				return await context.WithCancellation(cancellationToken).CallAsync(handler, args).ConfigureAwait(false);
			}

			await gate.WaitAsync(cancellationToken).ConfigureAwait(false);

			try
			{
				return await context.WithCancellation(cancellationToken).CallAsync(handler, args).ConfigureAwait(false);
			}
			finally
			{
				gate.Release();
			}
		}

		private static JsonNode? ToJson(LuaValue value)
		{
			return value.ToConstNodeValue(tolerant: true)?.ToJsonNode();
		}

		/// <summary>
		/// Turns a Lua error into the JSON-RPC error the peer receives.
		/// </summary>
		/// <param name="exception">The Lua error.</param>
		/// <returns>The error to answer with.</returns>
		/// <remarks>
		/// A script raises a shaped error by throwing a table: <c>{ code = -32601, message = "...", data = {...} }</c>.
		/// Anything else becomes an internal error that keeps the thrown text.
		/// </remarks>
		private static JsonRpcException Translate(LuaRuntimeException exception)
		{
			if (exception.ErrorValue is not LuaTable table)
				return new JsonRpcException(RpcErrorCodes.InternalError, exception.Message);

			var code = table.Get("code") is LuaNumber number ? (int)number.Value : RpcErrorCodes.InternalError;
			var message = table.Get("message") is LuaString text ? text.Value : exception.Message;
			var data = table.Get("data").ToConstNodeValue(tolerant: true)?.ToJsonNode();

			return new JsonRpcException(code, message, data);
		}
	}
}
