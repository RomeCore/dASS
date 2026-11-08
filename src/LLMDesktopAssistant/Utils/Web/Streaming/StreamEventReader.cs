using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace LLMDesktopAssistant.Utils.Web.Streaming
{
	/// <summary>
	/// Turns a streaming HTTP body into a sequence of <see cref="StreamEvent"/> items.
	/// </summary>
	/// <remarks>
	/// The reader is deliberately transport-agnostic: it consumes a <see cref="TextReader"/> and knows
	/// nothing about HTTP or Lua, so every mode can be tested against a plain string.
	/// </remarks>
	public static class StreamEventReader
	{
		/// <summary>
		/// Reads the stream to its end, yielding one item per decoded event.
		/// </summary>
		/// <param name="reader">The source of lines.</param>
		/// <param name="options">The wire format and its error policy.</param>
		/// <param name="cancellationToken">The cancellation token.</param>
		/// <returns>The decoded events, in stream order.</returns>
		/// <exception cref="InvalidDataException">
		/// Thrown when the input cannot be decoded and the policy is
		/// <see cref="StreamParseErrorPolicy.Abort"/>.
		/// </exception>
		/// <exception cref="TimeoutException">Thrown when no line arrives within the idle timeout.</exception>
		public static IAsyncEnumerable<StreamEvent> ReadAsync(TextReader reader,
			StreamReadOptions options, CancellationToken cancellationToken = default)
		{
			return options.Mode switch
			{
				StreamMode.Sse => ReadSseAsync(reader, options, cancellationToken),
				StreamMode.JsonLines => ReadJsonLinesAsync(reader, options, cancellationToken),
				StreamMode.RawLines => ReadRawLinesAsync(reader, options, cancellationToken),
				_ => throw new ArgumentOutOfRangeException(nameof(options), options.Mode, "Unknown stream mode.")
			};
		}

		/// <summary>
		/// Decodes Server-Sent Events: <c>data</c> lines accumulate into a frame, the frame is dispatched
		/// on a blank line or at the end of the stream, and comment lines are emitted as their own records
		/// only when they are asked for.
		/// </summary>
		private static async IAsyncEnumerable<StreamEvent> ReadSseAsync(TextReader reader,
			StreamReadOptions options, [EnumeratorCancellation] CancellationToken cancellationToken)
		{
			using var idle = new IdleWatch(options.IdleTimeout, cancellationToken);

			var index = 0;
			var data = new StringBuilder();
			var raw = new List<string>();
			string? eventName = null;
			string? id = null;
			var hasData = false;

			while (true)
			{
				var line = await ReadLineAsync(reader, idle);

				if (line is null)
				{
					// A frame is normally dispatched by a blank line, but a stream may end right after its data.
					if (hasData && BuildSseEvent(index + 1, raw, data, eventName, id, options) is { } last)
						yield return last;

					yield break;
				}

				if (line.Length == 0)
				{
					if (hasData && BuildSseEvent(index + 1, raw, data, eventName, id, options) is { } dispatched)
					{
						index++;
						yield return dispatched;
					}

					data.Clear();
					raw.Clear();
					eventName = null;
					id = null;
					hasData = false;
					continue;
				}

				if (line[0] == ':')
				{
					if (options.Comments)
						yield return new StreamEvent
						{
							Index = ++index,
							Line = line,
							Comment = line.Length > 1 ? line[1..].TrimStart(' ') : string.Empty
						};

					continue;
				}

				raw.Add(line);

				var colon = line.IndexOf(':');
				var field = colon < 0 ? line : line[..colon];
				var value = colon < 0 ? string.Empty : line[(colon + 1)..];
				if (value.StartsWith(' '))
					value = value[1..];

				switch (field)
				{
					case "data":
						data.Append(value).Append('\n');
						hasData = true;
						break;
					case "event":
						eventName = value;
						break;
					case "id":
						id = value;
						break;
					case "retry":
						// Retry belongs to the transport that would reconnect, and nothing here reconnects.
						break;
					default:
						if (BuildError(index + 1, line, $"unknown field '{field}'", options.OnError) is { } unknown)
							yield return unknown;
						break;
				}
			}
		}

		/// <summary>
		/// Decodes one JSON document per line.
		/// </summary>
		private static async IAsyncEnumerable<StreamEvent> ReadJsonLinesAsync(TextReader reader,
			StreamReadOptions options, [EnumeratorCancellation] CancellationToken cancellationToken)
		{
			using var idle = new IdleWatch(options.IdleTimeout, cancellationToken);

			var index = 0;

			while (await ReadLineAsync(reader, idle) is { } line)
			{
				if (line.Length == 0)
					continue;

				if (!TryParseJson(line, out var node, out var error))
				{
					if (BuildError(index + 1, line, error!, options.OnError) is { } reported)
					{
						index++;
						yield return reported;
					}

					continue;
				}

				yield return new StreamEvent
				{
					Index = ++index,
					Line = line,
					Data = line,
					Json = node,
					IsJsonDecoded = true
				};
			}
		}

		/// <summary>
		/// Emits every line as it arrives, with no interpretation at all.
		/// </summary>
		private static async IAsyncEnumerable<StreamEvent> ReadRawLinesAsync(TextReader reader,
			StreamReadOptions options, [EnumeratorCancellation] CancellationToken cancellationToken)
		{
			using var idle = new IdleWatch(options.IdleTimeout, cancellationToken);

			var index = 0;

			while (await ReadLineAsync(reader, idle) is { } line)
				yield return new StreamEvent { Index = ++index, Line = line, Data = line };
		}

		/// <summary>
		/// Builds the event of a completed Server-Sent Events frame, or the error record that replaces it.
		/// </summary>
		private static StreamEvent? BuildSseEvent(int index, List<string> raw, StringBuilder data,
			string? eventName, string? id, StreamReadOptions options)
		{
			var payload = data.Length > 0 ? data.ToString(0, data.Length - 1) : string.Empty;
			var rawText = string.Join("\n", raw);

			JsonNode? node = null;
			if (options.ParseJson && !TryParseJson(payload, out node, out var error))
				return BuildError(index, rawText, error!, options.OnError);

			return new StreamEvent
			{
				Index = index,
				Line = rawText,
				Event = eventName,
				Id = id,
				Data = payload,
				Json = options.ParseJson ? node : null,
				IsJsonDecoded = options.ParseJson
			};
		}

		/// <summary>
		/// Turns a decode failure into the event the policy asks for, or throws under
		/// <see cref="StreamParseErrorPolicy.Abort"/>.
		/// </summary>
		private static StreamEvent? BuildError(int index, string line, string message, StreamParseErrorPolicy policy)
		{
			var text = $"stream decode error at event #{index}: {message}";

			return policy switch
			{
				StreamParseErrorPolicy.Ignore => null,
				StreamParseErrorPolicy.Report => new StreamEvent { Index = index, Line = line, Error = text },
				_ => throw new InvalidDataException(text)
			};
		}

		/// <summary>
		/// Reads one line through the idle deadline.
		/// </summary>
		private static async ValueTask<string?> ReadLineAsync(TextReader reader, IdleWatch idle)
		{
			try
			{
				return await reader.ReadLineAsync(idle.Arm());
			}
			catch (OperationCanceledException) when (!idle.IsOuterCancelled)
			{
				throw idle.CreateTimeout();
			}
		}

		/// <summary>
		/// Parses a payload as JSON, reporting the reason instead of throwing.
		/// </summary>
		private static bool TryParseJson(string payload, out JsonNode? node, out string? error)
		{
			try
			{
				node = JsonNode.Parse(payload);
				error = null;
				return true;
			}
			catch (JsonException ex)
			{
				node = null;
				error = ex.Message;
				return false;
			}
		}

		/// <summary>
		/// One linked cancellation source per stream, re-armed before every read instead of one per line.
		/// </summary>
		private sealed class IdleWatch(TimeSpan? timeout, CancellationToken outerCancellation) : IDisposable
		{
			private readonly CancellationTokenSource? _source = timeout is null
				? null
				: CancellationTokenSource.CreateLinkedTokenSource(outerCancellation);

			/// <summary>
			/// Whether the caller's own token fired, as opposed to the idle deadline.
			/// </summary>
			public bool IsOuterCancelled => outerCancellation.IsCancellationRequested;

			/// <summary>
			/// Starts the idle deadline and returns the token to read with.
			/// </summary>
			public CancellationToken Arm()
			{
				if (_source is null)
					return outerCancellation;

				_source.CancelAfter(timeout!.Value);
				return _source.Token;
			}

			/// <summary>
			/// Creates the timeout that describes an expired idle deadline.
			/// </summary>
			public TimeoutException CreateTimeout() => new($"no data for {timeout!.Value.TotalSeconds:0.#}s");

			/// <inheritdoc />
			public void Dispose() => _source?.Dispose();
		}
	}
}
