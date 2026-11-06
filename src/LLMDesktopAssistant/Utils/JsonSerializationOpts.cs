using System;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;

namespace LLMDesktopAssistant.Utils
{
	public static class JsonSerializationOpts
	{
		public static JsonSerializerOptions UnsafeEscaping { get; } = new JsonSerializerOptions
		{
			Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
			TypeInfoResolver = new DefaultJsonTypeInfoResolver()
		};
	}
}
