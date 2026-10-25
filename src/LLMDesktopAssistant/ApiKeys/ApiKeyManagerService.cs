using System.Security.Cryptography;
using LLMDesktopAssistant.Services;
using LLMDesktopAssistant.Settings;
using LLMDesktopAssistant.Utils;
using RCLargeLanguageModels.Security;
using Serilog;

namespace LLMDesktopAssistant.ApiKeys
{
	/// <summary>
	/// Default implementation of <see cref="IApiKeyManagerService"/>.
	/// Manages API keys with support for raw, encrypted (AES), and environment variable storage schemes.
	/// Registered automatically via <see cref="ServiceAttribute"/>.
	/// </summary>
	[Service(typeof(IApiKeyManagerService))]
	public class ApiKeyManagerService : Disposable, IApiKeyManagerService
	{
		private const string KeyFileName = "api_keys.aes";
		private const int AesKeySizeBits = 256;
		private const int AesIvSizeBytes = 16;

		private readonly ApiKeysConfiguration _configuration;
		private readonly byte[] _aesKey;
		private bool _disposed;

		/// <summary>
		/// Gets the number of stored keys.
		/// </summary>
		public int Count => _configuration.ApiKeys.Count;

		public ApiKeyManagerService()
		{
			_configuration = SettingsManager.Get<ApiKeysConfiguration>();
			_aesKey = LoadOrCreateAesKey();
		}

		/// <inheritdoc/>
		public ApiKeysConfigurationItem AddKey(string name, string value, ApiKeyStorageScheme scheme = ApiKeyStorageScheme.Encrypted)
		{
			if (string.IsNullOrWhiteSpace(name))
				throw new ArgumentException("Key name cannot be empty.", nameof(name));
			if (string.IsNullOrEmpty(value))
				throw new ArgumentException("Key value cannot be null or empty.", nameof(value));

			var storedValue = PrepareStoredValue(value, scheme);

			var item = new ApiKeysConfigurationItem
			{
				Id = Guid.NewGuid(),
				Name = name,
				StoredValue = storedValue,
				StorageScheme = scheme
			};

			_configuration.ApiKeys.Add(item);

			Log.Information("API key '{Name}' ({Id}) added with scheme {Scheme}", name, item.Id, scheme);

			return MaskItem(item);
		}

		/// <inheritdoc/>
		public bool RemoveKey(Guid id)
		{
			var item = FindItem(id);
			if (item == null)
				return false;

			_configuration.ApiKeys.Remove(item);
			Log.Information("API key '{Name}' ({Id}) removed", item.Name, id);
			return true;
		}

		/// <inheritdoc/>
		public bool UpdateKey(Guid id, string? name, string? value, ApiKeyStorageScheme? scheme)
		{
			var item = FindItem(id);
			if (item == null)
				return false;

			var effectiveScheme = scheme ?? item.StorageScheme;

			if (name != null && !string.IsNullOrWhiteSpace(name))
			{
				item.Name = name;
			}

			if (value != null)
			{
				item.StoredValue = PrepareStoredValue(value, effectiveScheme);
				item.StorageScheme = effectiveScheme;
				Log.Information("API key '{Name}' ({Id}) value updated with scheme {Scheme}", item.Name, id, effectiveScheme);
			}
			else if (scheme != null && scheme != item.StorageScheme)
			{
				// Scheme changed but value unchanged - re-encrypt with new scheme
				var rawValue = ResolveKey(id);
				if (rawValue != null)
				{
					item.StoredValue = PrepareStoredValue(rawValue, scheme.Value);
					item.StorageScheme = scheme.Value;
					Log.Information("API key '{Name}' ({Id}) scheme changed to {Scheme}", item.Name, id, scheme.Value);
				}
			}

			return true;
		}

		/// <inheritdoc/>
		public ApiKeysConfigurationItem? GetKey(Guid id)
		{
			var item = FindItem(id);
			return item != null ? MaskItem(item) : null;
		}

		/// <inheritdoc/>
		public IEnumerable<ApiKeysConfigurationItem> GetAllKeys()
		{
			return _configuration.ApiKeys.Select(MaskItem);
		}

		/// <inheritdoc/>
		public string? ResolveKey(Guid id)
		{
			var item = FindItem(id);
			if (item == null)
				return null;

			return ResolveStoredValue(item);
		}

		/// <inheritdoc/>
		public ITokenAccessor? GetTokenAccessor(Guid id)
		{
			var item = FindItem(id);
			if (item == null)
				return null;

			return new DelegateTokenAccessor(() => ResolveStoredValue(item) ?? string.Empty);
		}

		/// <inheritdoc/>
		public bool HasKey(Guid id)
		{
			return FindItem(id) != null;
		}

		/// <summary>
		/// Prepares the stored value based on the storage scheme.
		/// </summary>
		private string PrepareStoredValue(string rawValue, ApiKeyStorageScheme scheme)
		{
			return scheme switch
			{
				ApiKeyStorageScheme.Raw => rawValue,
				ApiKeyStorageScheme.Encrypted => Encrypt(rawValue),
				ApiKeyStorageScheme.EnvironmentVariable => rawValue, // stores the env var name
				_ => throw new ArgumentOutOfRangeException(nameof(scheme))
			};
		}

		/// <summary>
		/// Resolves the actual API key value from a stored item.
		/// </summary>
		private string? ResolveStoredValue(ApiKeysConfigurationItem item)
		{
			if (item.StoredValue == null)
				return null;

			return item.StorageScheme switch
			{
				ApiKeyStorageScheme.Raw => item.StoredValue,
				ApiKeyStorageScheme.Encrypted => Decrypt(item.StoredValue),
				ApiKeyStorageScheme.EnvironmentVariable => Environment.GetEnvironmentVariable(item.StoredValue),
				_ => null
			};
		}

		/// <summary>
		/// Encrypts a plaintext string using AES-256-CBC with PKCS7 padding.
		/// The output format is: Base64(IV || ciphertext).
		/// </summary>
		private string Encrypt(string plaintext)
		{
			using var aes = Aes.Create();
			aes.Key = _aesKey;
			aes.GenerateIV();

			using var encryptor = aes.CreateEncryptor();
			var plaintextBytes = System.Text.Encoding.UTF8.GetBytes(plaintext);
			var ciphertextBytes = encryptor.TransformFinalBlock(plaintextBytes, 0, plaintextBytes.Length);

			// Prepend IV to ciphertext
			var result = new byte[aes.IV.Length + ciphertextBytes.Length];
			Buffer.BlockCopy(aes.IV, 0, result, 0, aes.IV.Length);
			Buffer.BlockCopy(ciphertextBytes, 0, result, aes.IV.Length, ciphertextBytes.Length);

			return Convert.ToBase64String(result);
		}

		/// <summary>
		/// Decrypts a Base64-encoded ciphertext (format: IV || ciphertext) using AES-256-CBC.
		/// Returns <see langword="null"/> if decryption fails.
		/// </summary>
		private string? Decrypt(string ciphertext)
		{
			try
			{
				var data = Convert.FromBase64String(ciphertext);

				if (data.Length < AesIvSizeBytes)
				{
					Log.Warning("Encrypted data is too short to contain IV");
					return null;
				}

				using var aes = Aes.Create();
				aes.Key = _aesKey;

				// Extract IV and ciphertext
				var iv = new byte[AesIvSizeBytes];
				var actualCiphertext = new byte[data.Length - AesIvSizeBytes];
				Buffer.BlockCopy(data, 0, iv, 0, AesIvSizeBytes);
				Buffer.BlockCopy(data, AesIvSizeBytes, actualCiphertext, 0, actualCiphertext.Length);

				aes.IV = iv;

				using var decryptor = aes.CreateDecryptor();
				var plaintextBytes = decryptor.TransformFinalBlock(actualCiphertext, 0, actualCiphertext.Length);
				return System.Text.Encoding.UTF8.GetString(plaintextBytes);
			}
			catch (Exception ex)
			{
				Log.Warning(ex, "Failed to decrypt API key");
				return null;
			}
		}

		/// <summary>
		/// Loads the AES key from a file, or creates a new one if it doesn't exist.
		/// The key file is stored in <see cref="Directories.Data"/>.
		/// </summary>
		private byte[] LoadOrCreateAesKey()
		{
			var keyPath = Path.Combine(Directories.Data, KeyFileName);

			try
			{
				if (File.Exists(keyPath))
				{
					var key = File.ReadAllBytes(keyPath);
					if (key.Length == AesKeySizeBits / 8)
					{
						Log.Debug("AES key loaded from {Path}", keyPath);
						return key;
					}

					Log.Warning("AES key file has invalid length ({Length} bytes), regenerating", key.Length);
				}
			}
			catch (Exception ex)
			{
				Log.Warning(ex, "Failed to load AES key from {Path}, generating a new one", keyPath);
			}

			// Generate new key
			using var aes = Aes.Create();
			aes.KeySize = AesKeySizeBits;
			aes.GenerateKey();

			try
			{
				Directory.CreateDirectory(Path.GetDirectoryName(keyPath)!);
				File.WriteAllBytes(keyPath, aes.Key);
				Log.Information("New AES key generated and saved to {Path}", keyPath);
			}
			catch (Exception ex)
			{
				Log.Error(ex, "Failed to save AES key to {Path}", keyPath);
			}

			return aes.Key;
		}

		private ApiKeysConfigurationItem? FindItem(Guid id)
		{
			return _configuration.ApiKeys.FirstOrDefault(k => k.Id == id);
		}

		/// <summary>
		/// Returns a copy of the item with the stored value masked (for safe display).
		/// </summary>
		private static ApiKeysConfigurationItem MaskItem(ApiKeysConfigurationItem source)
		{
			return new ApiKeysConfigurationItem
			{
				Id = source.Id,
				Name = source.Name,
				StoredValue = source.StoredValue != null
					? (source.StorageScheme == ApiKeyStorageScheme.EnvironmentVariable
						? source.StoredValue // env var name is not a secret, show it as-is
						: "••••••••")
					: null,
				StorageScheme = source.StorageScheme
			};
		}

		/// <inheritdoc/>
		protected override void Dispose(bool disposing)
		{
			if (_disposed)
				return;

			base.Dispose(disposing);

			if (disposing)
			{
				_disposed = true;
			}
		}
	}
}
