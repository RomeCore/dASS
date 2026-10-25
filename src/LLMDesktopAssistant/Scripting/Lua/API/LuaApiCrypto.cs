using System.Security.Cryptography;
using System.Text;
using AsyncLua;
using AsyncLua.Values;

namespace LLMDesktopAssistant.Scripting.Lua.API
{
	/// <summary>
	/// Lua API for cryptography: <c>crypto.*</c>.
	/// Provides hashing (MD5, SHA1, SHA256, SHA512), HMAC, Base64 and random hex.
	/// </summary>
	[LuaApi(chatScoped: false)]
	public class LuaApiCrypto : LuaApiBase
	{
		public override string? Namespace => "crypto";

		public override string? Manuals => """
			--- crypto - cryptography API

			Provides hashing, HMAC, Base64 encoding/decoding and random hex generation.

			All hash/HMAC functions accept either a string or a table of bytes as input.

			FUNCTIONS:

			--- crypto.md5(data)
			--- crypto.sha1(data)
			--- crypto.sha256(data)
			--- crypto.sha512(data)
			  Returns the hex-encoded hash of the input data.
			  Parameters:
			    - data: string or table of bytes - input data
			  Returns: string - lowercase hex digest

			--- crypto.hmac(key, data, algorithm)
			  Returns the hex-encoded HMAC of the input data.
			  Parameters:
			    - key: string or table of bytes - HMAC key
			    - data: string or table of bytes - input data
			    - algorithm: string - hash algorithm: "md5", "sha1", "sha256", "sha512"
			  Returns: string - lowercase hex digest

			--- crypto.base64_encode(data)
			  Encodes a string or bytes to Base64.
			  Parameters:
			    - data: string or table of bytes - data to encode
			  Returns: string - Base64 encoded

			--- crypto.base64_decode(str)
			  Decodes a Base64 string to plain text (UTF-8).
			  Parameters:
			    - str: string - Base64 encoded string
			  Returns: string - decoded text

			--- crypto.base64_decode_bytes(str)
			  Decodes a Base64 string to an array of bytes.
			  Parameters:
			    - str: string - Base64 encoded string
			  Returns: table - array of integers (0-255)

			--- crypto.random_hex(n)
			  Generates a random hex string of n bytes (2n hex chars).
			  Parameters:
			    - n: number - number of random bytes
			  Returns: string - hex string

			EXAMPLES:

			  -- Hash
			  print(crypto.md5("hello"))
			  print(crypto.sha256("hello"))

			  -- HMAC
			  print(crypto.hmac("key", "message", "sha256"))

			  -- Base64
			  print(crypto.base64_encode("hello world"))
			  local decoded = crypto.base64_decode("aGVsbG8gd29ybGQ=")

			  -- Random hex
			  local token = crypto.random_hex(16) -- 32 chars
			""";

		public override Action? Populate(LuaTable globals, LuaTable ns, LuaService luaService)
		{
			ns["md5"] = new LuaCallbackFunction(Md5);
			ns["sha1"] = new LuaCallbackFunction(Sha1);
			ns["sha256"] = new LuaCallbackFunction(Sha256);
			ns["sha512"] = new LuaCallbackFunction(Sha512);
			ns["hmac"] = new LuaCallbackFunction(Hmac);
			ns["base64_encode"] = new LuaCallbackFunction(Base64Encode);
			ns["base64_decode"] = new LuaCallbackFunction(Base64Decode);
			ns["base64_decode_bytes"] = new LuaCallbackFunction(Base64DecodeBytes);
			ns["random_hex"] = new LuaCallbackFunction(RandomHex);
			return null;
		}

		private static LuaTuple Md5(LuaCallingContext ctx, LuaValue[] args)
		{
			if (args.Length < 1)
				throw new LuaRuntimeException("crypto.md5(data): at least 1 argument expected.");
			var data = GetBytes(args[0]);
			return new LuaTuple(new LuaString(HexEncode(MD5.HashData(data))));
		}

		private static LuaTuple Sha1(LuaCallingContext ctx, LuaValue[] args)
		{
			if (args.Length < 1)
				throw new LuaRuntimeException("crypto.sha1(data): at least 1 argument expected.");
			var data = GetBytes(args[0]);
			return new LuaTuple(new LuaString(HexEncode(SHA1.HashData(data))));
		}

		private static LuaTuple Sha256(LuaCallingContext ctx, LuaValue[] args)
		{
			if (args.Length < 1)
				throw new LuaRuntimeException("crypto.sha256(data): at least 1 argument expected.");
			var data = GetBytes(args[0]);
			return new LuaTuple(new LuaString(HexEncode(SHA256.HashData(data))));
		}

		private static LuaTuple Sha512(LuaCallingContext ctx, LuaValue[] args)
		{
			if (args.Length < 1)
				throw new LuaRuntimeException("crypto.sha512(data): at least 1 argument expected.");
			var data = GetBytes(args[0]);
			return new LuaTuple(new LuaString(HexEncode(SHA512.HashData(data))));
		}

		private static LuaTuple Hmac(LuaCallingContext ctx, LuaValue[] args)
		{
			if (args.Length < 3)
				throw new LuaRuntimeException("crypto.hmac(key, data, algorithm): at least 3 arguments expected.");

			var key = GetBytes(args[0]);
			var data = GetBytes(args[1]);
			if (args[2] is not LuaString algorithmVal)
				throw new LuaRuntimeException("crypto.hmac(): third argument must be a string (algorithm: md5, sha1, sha256, sha512).");

			byte[] hash;
			switch (algorithmVal.Value.ToLowerInvariant())
			{
				case "md5":
					hash = HMACMD5.HashData(key, data);
					break;
				case "sha1":
					hash = HMACSHA1.HashData(key, data);
					break;
				case "sha256":
					hash = HMACSHA256.HashData(key, data);
					break;
				case "sha512":
					hash = HMACSHA512.HashData(key, data);
					break;
				default:
					throw new LuaRuntimeException($"crypto.hmac(): unknown algorithm '{algorithmVal.Value}'. Supported: md5, sha1, sha256, sha512.");
			}

			return new LuaTuple(new LuaString(HexEncode(hash)));
		}

		private static LuaTuple Base64Encode(LuaCallingContext ctx, LuaValue[] args)
		{
			if (args.Length < 1)
				throw new LuaRuntimeException("crypto.base64_encode(data): at least 1 argument expected.");
			var data = GetBytes(args[0]);
			return new LuaTuple(new LuaString(Convert.ToBase64String(data)));
		}

		private static LuaTuple Base64Decode(LuaCallingContext ctx, LuaValue[] args)
		{
			if (args.Length < 1)
				throw new LuaRuntimeException("crypto.base64_decode(str): at least 1 argument expected.");
			if (args[0] is not LuaString strVal)
				throw new LuaRuntimeException("crypto.base64_decode(): first argument must be a string.");
			var bytes = Convert.FromBase64String(strVal.Value);
			return new LuaTuple(new LuaString(Encoding.UTF8.GetString(bytes)));
		}

		private static LuaTuple Base64DecodeBytes(LuaCallingContext ctx, LuaValue[] args)
		{
			if (args.Length < 1)
				throw new LuaRuntimeException("crypto.base64_decode_bytes(str): at least 1 argument expected.");
			if (args[0] is not LuaString strVal)
				throw new LuaRuntimeException("crypto.base64_decode_bytes(): first argument must be a string.");
			var bytes = Convert.FromBase64String(strVal.Value);
			var result = new LuaTable();
			for (int i = 0; i < bytes.Length; i++)
				result[i + 1] = new LuaNumber(bytes[i]);
			return new LuaTuple(result);
		}

		private static LuaTuple RandomHex(LuaCallingContext ctx, LuaValue[] args)
		{
			if (args.Length < 1)
				throw new LuaRuntimeException("crypto.random_hex(n): at least 1 argument expected.");
			if (args[0] is not LuaNumber nVal)
				throw new LuaRuntimeException("crypto.random_hex(): first argument must be a number.");
			var n = (int)nVal.Value;
			var bytes = RandomNumberGenerator.GetBytes(n);
			return new LuaTuple(new LuaString(HexEncode(bytes)));
		}

		private static byte[] GetBytes(LuaValue value)
		{
			if (value is LuaString str)
				return Encoding.UTF8.GetBytes(str.Value);
			if (value is LuaTable table)
			{
				var bytes = new byte[table.Length];
				for (int i = 0; i < table.Length; i++)
				{
					var item = table.Get(i + 1);
					if (item is LuaNumber num)
						bytes[i] = (byte)num.Value;
				}
				return bytes;
			}
			throw new LuaRuntimeException("expected a string or table of bytes.");
		}

		private static string HexEncode(byte[] bytes)
		{
			return Convert.ToHexStringLower(bytes);
		}
	}
}
