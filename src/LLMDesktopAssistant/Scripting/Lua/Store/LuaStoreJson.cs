using System.Collections.Immutable;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Unicode;
using LLMDesktopAssistant.StructuredValues;
using LLMDesktopAssistant.StructuredValues.Const;
using LLMDesktopAssistant.StructuredValues.Converters;
using Serilog;

namespace LLMDesktopAssistant.Scripting.Lua.Store
{
	/// <summary>
	/// Reads and writes the JSON files a <c>store.file</c> keeps its data in.
	/// </summary>
	/// <remarks>
	/// Parsing is strict on purpose: the tolerant parser accepts a truncated document as a valid prefix,
	/// and a store would then write that prefix back over the original file. A store never guesses.
	/// The non-finite number convention lives in <see cref="JsonStructuredConverter"/>.
	/// </remarks>
	public static class LuaStoreJson
	{
		private static readonly JsonSerializerOptions _options = new()
		{
			Encoder = JavaScriptEncoder.Create(UnicodeRanges.All),
			WriteIndented = true
		};

		/// <summary>
		/// Reads a store file into a structured dictionary.
		/// </summary>
		/// <param name="path">The absolute path of the file.</param>
		/// <param name="quarantine">
		/// When <see langword="true"/>, a file that is not valid JSON is renamed to a timestamped
		/// <c>.broken</c> sibling and an empty store is returned; when <see langword="false"/>
		/// the parse error propagates and the file is left untouched.
		/// </param>
		/// <returns>The stored keys, never <see langword="null"/>.</returns>
		public static ConstNodeDictionaryValue ReadFile(string path, bool quarantine)
		{
			if (!File.Exists(path))
				return Empty();

			var text = File.ReadAllText(path);
			if (string.IsNullOrWhiteSpace(text))
				return Empty();

			try
			{
				if (JsonNode.Parse(text) is not JsonObject root)
					throw new JsonException("the root of a store file must be a JSON object");

				return root.ToConstNodeValue() as ConstNodeDictionaryValue ?? Empty();
			}
			catch (Exception exception) when (exception is JsonException or FormatException)
			{
				if (!quarantine)
					throw;

				var broken = Quarantine(path);
				Log.Warning("Store file {Path} is not valid JSON ({Reason}); moved to {BrokenPath}",
					path, exception.Message, broken);
				return Empty();
			}
		}

		/// <summary>
		/// Writes the given snapshot to a store file, replacing it atomically.
		/// </summary>
		/// <param name="path">The absolute path of the file.</param>
		/// <param name="snapshot">The snapshot to write.</param>
		public static void WriteFile(string path, INodeValue snapshot)
		{
			var directory = Path.GetDirectoryName(path);
			if (!string.IsNullOrEmpty(directory))
				Directory.CreateDirectory(directory);

			var text = snapshot.ToJsonNode()?.ToJsonString(_options) ?? "{}";
			var temporary = $"{path}.{Guid.NewGuid():N}.tmp";

			try
			{
				File.WriteAllText(temporary, text, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
				File.Move(temporary, path, overwrite: true);
			}
			finally
			{
				if (File.Exists(temporary))
					File.Delete(temporary);
			}
		}

		private static ConstNodeDictionaryValue Empty()
		{
			return new ConstNodeDictionaryValue { Items = ImmutableDictionary<string, ConstNodeValue>.Empty };
		}

		private static string Quarantine(string path)
		{
			var prefix = $"{path}.{DateTime.Now:yyyyMMdd-HHmmss}";
			var target = $"{prefix}.broken";
			var index = 1;

			while (File.Exists(target))
				target = $"{prefix}-{index++}.broken";

			File.Move(path, target);
			return target;
		}
	}
}
