using AsyncLua;
using AsyncLua.Values;
using LLMDesktopAssistant.LLM.Services;
using LLMDesktopAssistant.Scripting.Lua;
using LLMDesktopAssistant.Services.Instances;
using LLMDesktopAssistant.Utils;
using Serilog;

namespace LLMDesktopAssistant.Scripting.Lua.Logs
{
	/// <summary>
	/// Owns the <c>dass.log</c> subscriptions of one chat and delivers log events into its Lua state.
	/// </summary>
	/// <remarks>
	/// The buffer is application-wide, so every chat subscribes to the same stream. The feed is chat-scoped and
	/// dies with the chat; <see cref="Reset"/> drops the registrations when the Lua state is rebuilt, because their
	/// functions belong to the state being thrown away. Delivery is synchronous on the emitting thread, after the
	/// event is buffered, and anything logged from inside a subscriber is not delivered again.
	/// </remarks>
	[ChatService]
	[ChatService(typeof(IChatClosingHandler))]
	public class LuaLogFeed : Disposable, IChatClosingHandler, ILuaCallbackOwner
	{
		private sealed record Subscription(LuaCallbackRegistration Registration, LuaLogFilter Filter);

		private readonly List<Subscription> _subscriptions = [];
		private readonly Lock _lock = new();
		private readonly Lock _gate = new();
		private readonly CancellationTokenSource _chatLifetime = new();
		private bool _delivering;

		/// <summary>
		/// Initialises the feed and hooks it to the buffer. The hook is never re-armed: the feed lives as long
		/// as its chat.
		/// </summary>
		public LuaLogFeed()
		{
			LogBuffer.Emitted += OnEmitted;
		}

		/// <summary>
		/// The token the deliveries are invoked with; cancels when the chat closes.
		/// </summary>
		public CancellationToken ChatLifetime => _chatLifetime.Token;

		/// <summary>
		/// How many subscriptions are armed right now.
		/// </summary>
		public int Count
		{
			get
			{
				lock (_lock)
					return _subscriptions.Count;
			}
		}

		/// <summary>
		/// Subscribes a function to the events passing the filter.
		/// </summary>
		public LuaCallbackRegistration Subscribe(LuaFunction function, LuaLogFilter filter, LuaCallingContext context)
		{
			var registration = new LuaCallbackRegistration(this, function, context);

			lock (_lock)
			{
				_subscriptions.Add(new Subscription(registration, filter));
			}

			return registration;
		}

		/// <summary>
		/// Checks whether a subscription is still armed.
		/// </summary>
		public bool Contains(LuaCallbackRegistration registration)
		{
			lock (_lock)
				return _subscriptions.Exists(subscription => subscription.Registration == registration);
		}

		/// <summary>
		/// Removes a subscription.
		/// </summary>
		/// <returns><see langword="true"/> when it was still armed.</returns>
		public bool Unregister(LuaCallbackRegistration registration)
		{
			lock (_lock)
				return _subscriptions.RemoveAll(subscription => subscription.Registration == registration) > 0;
		}

		/// <summary>
		/// Drops every subscription, keeping the chat alive; called when the Lua state is rebuilt.
		/// </summary>
		public void Reset()
		{
			lock (_lock)
				_subscriptions.Clear();
		}

		/// <summary>
		/// Stops delivering and drops every subscription.
		/// </summary>
		public void OnChatClosing()
		{
			lock (_lock)
			{
				_subscriptions.Clear();
			}

			try
			{
				_chatLifetime.Cancel();
			}
			catch (ObjectDisposedException)
			{
			}
		}

		/// <summary>
		/// Delivers one buffered event to every subscriber it passes.
		/// </summary>
		private void OnEmitted(RenderedLogEvent entry)
		{
			// The gate serialises deliveries across threads; it is reentrant, so a subscriber logging would
			// come back here on the same thread, where _delivering turns it away.
			lock (_gate)
			{
				if (_delivering)
					return;

				Subscription[] snapshot;

				lock (_lock)
				{
					if (_subscriptions.Count == 0)
						return;

					snapshot = [.. _subscriptions];
				}

				_delivering = true;

				try
				{
					var value = entry.ToLua();

					foreach (var subscription in snapshot)
					{
						if (!subscription.Filter.Matches(entry))
							continue;

						try
						{
							// The registering call is long over: its own token would abort the callback on the
							// first instruction, so the context carries the chat lifetime instead.
							subscription.Registration.Function.Invoke(
								subscription.Registration.Context.WithCancellation(ChatLifetime), [value]);
						}
						catch (Exception ex)
						{
							Log.Warning(ex, "A dass.log subscription failed: {Error}", ex.Message);
						}
					}
				}
				finally
				{
					_delivering = false;
				}
			}
		}

		protected override void Dispose(bool disposing)
		{
			base.Dispose(disposing);

			if (!disposing)
				return;

			LogBuffer.Emitted -= OnEmitted;
			_chatLifetime.Dispose();
		}
	}
}
