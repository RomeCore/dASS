using System.Text;
using AsyncLua;
using AsyncLua.Values;
using LLMDesktopAssistant.LLM.Services;
using Serilog;

namespace LLMDesktopAssistant.Scripting.Lua.Rpc
{
	/// <summary>
	/// Where the standard error of an RPC peer goes: the handle's own ring buffer, a file, a Lua table or
	/// a Lua callback.
	/// </summary>
	/// <remarks>
	/// Whatever the destination, the stream is always drained: a peer that fills its diagnostic pipe stops
	/// answering, and the failure looks like a protocol hang rather than a full pipe. Writing to a file or
	/// calling into Lua therefore happens on the drain task, never inside the read loop.
	/// </remarks>
	public sealed class LuaRpcStandardError : IDisposable
	{
		/// <summary>
		/// The default number of lines kept by a buffer or a table sink.
		/// </summary>
		public const int DefaultLimit = 1000;

		private readonly Lock _lock = new();
		private readonly Queue<string> _buffer = new();
		private readonly int _limit;
		private readonly string? _filePath;
		private readonly LuaTable? _table;
		private readonly LuaFunction? _callback;

		private StreamWriter? _writer;
		private int _dropped;

		private LuaRpcStandardError(int limit, string? filePath, LuaTable? table, LuaFunction? callback)
		{
			_limit = limit;
			_filePath = filePath;
			_table = table;
			_callback = callback;
		}

		/// <summary>
		/// Gets a value indicating whether this sink redirects the lines somewhere the handle cannot read.
		/// </summary>
		public bool IsRedirected => _filePath is not null || _table is not null || _callback is not null;

		/// <summary>
		/// Creates the sink described by the <c>stderr</c> option.
		/// </summary>
		/// <param name="option">The option value: <see langword="null"/> for the ring buffer, a path
		/// string, a table to append lines to, or a function called per line.</param>
		/// <param name="stderrLimit">The number of lines the sink keeps, or <c>0</c> for unlimited.</param>
		/// <param name="workingDirectory">Resolves a relative file path.</param>
		/// <returns>The sink.</returns>
		public static LuaRpcStandardError Create(LuaValue? option, int stderrLimit, IWorkingDirectoryAccessService workingDirectory)
		{
			var limit = stderrLimit <= 0 ? int.MaxValue : stderrLimit;

			return option switch
			{
				LuaNil or null => new LuaRpcStandardError(limit, null, null, null),
				LuaString path => new LuaRpcStandardError(limit, workingDirectory.GetFullPath(path.Value), null, null),
				LuaTable table => new LuaRpcStandardError(limit, null, table, null),
				LuaFunction callback => new LuaRpcStandardError(limit, null, null, callback),
				_ => throw new LuaRuntimeException(
					"rpc.connect(): options.stderr must be nil, a file path, a table or a function.")
			};
		}

		/// <summary>
		/// Records one line of standard error.
		/// </summary>
		/// <param name="line">The line, without its terminator.</param>
		/// <param name="context">The context used to call a Lua callback, with the handle's lifetime token.</param>
		/// <param name="cancellationToken">The token to cancel the write.</param>
		public async Task ReportAsync(string line, LuaCallingContext context, CancellationToken cancellationToken = default)
		{
			if (_filePath is not null)
			{
				await WriteFileAsync(line, cancellationToken).ConfigureAwait(false);
				return;
			}

			if (_table is not null)
			{
				lock (_lock)
				{
					if (_table.Length >= _limit)
					{
						_dropped++;
						return;
					}

					_table.Append(new LuaString(line));
				}

				return;
			}

			if (_callback is not null)
			{
				try
				{
					await context.CallAsync(_callback, new LuaString(line)).ConfigureAwait(false);
				}
				catch (Exception ex)
				{
					Log.Warning(ex, "A Lua stderr callback failed.");
				}

				return;
			}

			lock (_lock)
			{
				_buffer.Enqueue(line);

				while (_buffer.Count > _limit)
				{
					_buffer.Dequeue();
					_dropped++;
				}
			}
		}

		/// <summary>
		/// Reads the buffer.
		/// </summary>
		/// <returns>The buffered text and the number of dropped lines, or <see langword="nil"/> plus the
		/// reason when the lines were redirected elsewhere.</returns>
		public LuaTuple Read()
		{
			if (IsRedirected)
				return new LuaTuple(LuaNil.Instance, new LuaString("stderr is redirected"));

			lock (_lock)
			{
				var text = string.Join("\n", _buffer);
				return new LuaTuple(new LuaString(text), new LuaNumber(_dropped));
			}
		}

		/// <inheritdoc/>
		public void Dispose()
		{
			_writer?.Dispose();
			_writer = null;
		}

		private async Task WriteFileAsync(string line, CancellationToken cancellationToken)
		{
			if (_writer is null)
			{
				var directory = Path.GetDirectoryName(_filePath!);
				if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
					Directory.CreateDirectory(directory);

				_writer = new StreamWriter(_filePath!, append: true, Encoding.UTF8) { AutoFlush = true };
			}

			await _writer.WriteLineAsync(line.AsMemory(), cancellationToken).ConfigureAwait(false);
		}
	}
}
