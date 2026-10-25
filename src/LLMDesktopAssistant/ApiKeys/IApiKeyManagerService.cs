using RCLargeLanguageModels.Security;

namespace LLMDesktopAssistant.ApiKeys
{
	/// <summary>
	/// Service for managing API keys: storage, encryption, and resolution.
	/// Provides <see cref="ITokenAccessor"/> for integration with RCLLM clients.
	/// </summary>
	public interface IApiKeyManagerService
	{
		// ========== CRUD ==========

		/// <summary>
		/// Adds a new API key and returns the created configuration item.
		/// </summary>
		/// <param name="name">Display name/label for the key.</param>
		/// <param name="value">The raw key value.</param>
		/// <param name="scheme">Storage scheme (defaults to <see cref="ApiKeyStorageScheme.Encrypted"/>).</param>
		ApiKeysConfigurationItem AddKey(string name, string value, ApiKeyStorageScheme scheme = ApiKeyStorageScheme.Encrypted);

		/// <summary>
		/// Removes an API key by its identifier.
		/// </summary>
		/// <returns><see langword="true"/> if removed, otherwise <see langword="false"/>.</returns>
		bool RemoveKey(Guid id);

		/// <summary>
		/// Updates the name, value and/or storage scheme of an existing key.
		/// Pass <see langword="null"/> for any parameter to keep it unchanged.
		/// </summary>
		/// <param name="id">The key identifier.</param>
		/// <param name="name">New display name (<see langword="null"/> to keep unchanged).</param>
		/// <param name="value">New value (<see langword="null"/> to keep unchanged).</param>
		/// <param name="scheme">New storage scheme (<see langword="null"/> to keep unchanged).</param>
		/// <returns><see langword="true"/> if updated, otherwise <see langword="false"/>.</returns>
		bool UpdateKey(Guid id, string? name, string? value, ApiKeyStorageScheme? scheme);

		/// <summary>
		/// Returns the configuration item for a key by ID (value is masked!).
		/// Use <see cref="ResolveKey(Guid)"/> to get the actual key value.
		/// </summary>
		ApiKeysConfigurationItem? GetKey(Guid id);

		/// <summary>
		/// Returns all stored keys (values are masked).
		/// </summary>
		IEnumerable<ApiKeysConfigurationItem> GetAllKeys();

		// ========== Resolution ==========

		/// <summary>
		/// Resolves the actual API key value by ID.
		/// For <see cref="ApiKeyStorageScheme.Raw"/> - returns as-is.
		/// For <see cref="ApiKeyStorageScheme.Encrypted"/> - decrypts.
		/// For <see cref="ApiKeyStorageScheme.EnvironmentVariable"/> - reads from environment variable.
		/// </summary>
		/// <returns>The actual key value or <see langword="null"/>.</returns>
		string? ResolveKey(Guid id);

		/// <summary>
		/// Creates an <see cref="ITokenAccessor"/> for the specified key.
		/// Returns <see langword="null"/> if the key is not found.
		/// </summary>
		ITokenAccessor? GetTokenAccessor(Guid id);

		/// <summary>
		/// Checks whether a key with the specified ID exists.
		/// </summary>
		bool HasKey(Guid id);

		/// <summary>
		/// Gets the number of stored keys.
		/// </summary>
		int Count { get; }
	}
}
