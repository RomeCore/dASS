using System.Globalization;
using Serilog.Core;
using Serilog.Events;

namespace LLMDesktopAssistant.Utils
{
	/// <summary>
	/// One log event as the buffer stores it: rendered once, stamped with its sequence and time.
	/// </summary>
	public record struct RenderedLogEvent(long Sequence, DateTimeOffset Timestamp, LogEventLevel Level,
		Exception? Exception, string Template, ImmutableDictionary<string, string> Properties, string Rendered);

	/// <summary>
	/// The in-memory ring of the most recent log events, shared by every chat and every subsystem.
	/// </summary>
	public static class LogBuffer
	{
		private class DelegatingLogEventSink(Action<LogEvent> onEmit) : ILogEventSink
		{
			public void Emit(LogEvent logEvent)
			{
				onEmit(logEvent);
			}
		}

		private static readonly List<RenderedLogEvent> _buffer = [];
		private static readonly Lock _lock = new();
		private static int _bufPointer;
		private static long _sequence;

		/// <summary>
		/// Raised after the event is stored and its sequence is assigned, outside the buffer lock,
		/// on the thread that emitted it.
		/// </summary>
		public static event Action<RenderedLogEvent>? Emitted;

		/// <summary>
		/// The <see cref="Serilog"/> event sink.
		/// </summary>
		public static ILogEventSink Sink { get; }

		/// <summary>
		/// The maximum size of internal log ring buffer.
		/// </summary>
		public const int MaxBufferSize = 5000;

		/// <summary>
		/// Total count of log events ever emitted, evicted ones included. Equal to the last sequence.
		/// </summary>
		public static long Count
		{
			get
			{
				lock (_lock)
					return _sequence;
			}
		}

		static LogBuffer()
		{
			Sink = new DelegatingLogEventSink(OnEmitCore);
		}

		private static void OnEmitCore(LogEvent ev)
		{
			RenderedLogEvent rendered;

			lock (_lock)
			{
				rendered = Render(ev, ++_sequence);

				if (_buffer.Count < MaxBufferSize)
					_buffer.Add(rendered);
				else
				{
					_buffer[_bufPointer] = rendered;
					_bufPointer = (_bufPointer + 1) % MaxBufferSize;
				}
			}

			Emitted?.Invoke(rendered);
		}

		/// <summary>
		/// The buffered events whose sequence is greater than the given one, oldest first.
		/// </summary>
		/// <param name="sequence">The last sequence the caller has already seen, or 0 for everything buffered.</param>
		public static List<RenderedLogEvent> GetAfter(long sequence)
		{
			lock (_lock)
			{
				var result = new List<RenderedLogEvent>(Math.Min(_buffer.Count, MaxBufferSize));

				// Once the ring wrapped, the write pointer is the oldest entry: order is [pointer, end) then [0, pointer).
				if (_buffer.Count >= MaxBufferSize)
				{
					for (int index = _bufPointer; index < _buffer.Count; index++)
						Take(_buffer[index]);
					for (int index = 0; index < _bufPointer; index++)
						Take(_buffer[index]);
				}
				else
				{
					for (int index = 0; index < _buffer.Count; index++)
						Take(_buffer[index]);
				}

				return result;

				void Take(RenderedLogEvent entry)
				{
					if (entry.Sequence > sequence)
						result.Add(entry);
				}
			}
		}

		private static RenderedLogEvent Render(LogEvent ev, long sequence)
		{
			return new RenderedLogEvent(sequence, ev.Timestamp, ev.Level, ev.Exception, ev.MessageTemplate.Text,
				ev.Properties.ToImmutableDictionary(k => k.Key, v => RenderProperty(v.Value)),
				ev.RenderMessage(CultureInfo.InvariantCulture));
		}

		/// <summary>
		/// Renders one property value without the quotes Serilog adds around strings in a rendered message.
		/// </summary>
		private static string RenderProperty(LogEventPropertyValue value)
			=> value is ScalarValue scalar
				? Convert.ToString(scalar.Value, CultureInfo.InvariantCulture) ?? "null"
				: value.ToString();
	}
}
