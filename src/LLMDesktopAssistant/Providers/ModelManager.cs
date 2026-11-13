using AngleSharp.Common;
using LLMDesktopAssistant.ApiKeys;
using LLMDesktopAssistant.Services;
using LLMDesktopAssistant.Settings;
using RCLargeLanguageModels;
using Serilog;

namespace LLMDesktopAssistant.Providers
{
	[Service(typeof(IModelManager))]
	public class ModelManager(
		IEnumerable<ModelProviderType> providerTypes,
		IApiKeyManagerService apiKeyManager
	) : IModelManager
	{
		private readonly ModelProvidersConfiguration providers = SettingsManager.Get<ModelProvidersConfiguration>();
		private readonly ModelDescriptorsCache cache = SettingsManager.Get<ModelDescriptorsCache>();
		private readonly ModelModifiersConfiguration modifiers = SettingsManager.Get<ModelModifiersConfiguration>();
		private readonly Dictionary<string, ModelProviderType> providerTypesMap = providerTypes.ToDictionary(t => t.Id);

		public bool IsModelAvailable(string fullName)
		{
			if (!ModelReference.TryParse(fullName, out var reference))
				return false;
			var foundProvider = providers.ModelProviders.FirstOrDefault(p => p.Name == reference.Provider);
			if (foundProvider == null)
			{
				Log.Warning("Could not find provider for model {ModelName}: {ProviderName}", reference.ModelId, reference.Provider);
				return false;
			}
			if (!foundProvider.Models.Concat(foundProvider.CustomModels).Any(m => m.Name == reference.ModelId))
				return false;
			return (reference.Modifiers?.Count ?? 0) == 0 || reference.Modifiers!.All(m => FindModifier(m) is not null);
		}

		public LLModel GetModel(string fullName)
		{
			var reference = ModelReference.Parse(fullName);
			var client = CreateClient(reference.Provider);
			var model = new LLModel(client, reference.ModelId);
			// TODO: Count all modifiers, not only first one.
			if (reference.Modifiers?.Count > 0)
				model = model.WithProperties(FindModifier(reference.Modifiers[0]).ToCompletionProperties());
			return model;
		}

		public LLModel? TryGetModel(string fullName)
		{
			try
			{
				return GetModel(fullName);
			}
			catch
			{
				return null;
			}
		}

		public IEnumerable<ModelItem> ListModels()
		{
			var cacheLookup = cache.Descriptors.ToDictionary(k => k.Name);
			return providers.ModelProviders.SelectMany(p => p.Models.Concat(p.CustomModels)
				.GroupBy(m => m.Name)
				.Select(g =>
				{
					var m = g.Last();
					return new ModelItem
					{
						Provider = p,
						Descriptor = m.IsInformationKnown ? m : cacheLookup.TryGetValue(m.Name, out var cached) ? cached : m,
						FullName = p.Name + "$" + m.Name
					};
				}));
		}

		public IEnumerable<ModelItem> ListSelectedModels()
		{
			var cacheLookup = cache.Descriptors.ToDictionary(k => k.Name);
			return providers.ModelProviders.SelectMany(p =>
			{
				var selectedModelNames = p.SelectedModelNames.ToHashSet();
				return p.Models.Where(m => selectedModelNames.Contains(m.Name))
					.Concat(p.CustomModels)
					.GroupBy(m => m.Name)
					.Select(g => g.Last())
					.Select(m => new ModelItem
					{
						Provider = p,
						Descriptor = m.IsInformationKnown ? m : cacheLookup.TryGetValue(m.Name, out var cached) ? cached : m,
						FullName = p.Name + "$" + m.Name
					});
			});
		}

		public IReadOnlyList<ModelModifier> ListModifiers() => modifiers.Modifiers;

		public async Task<bool> CheckConnectionAsync(ModelProviderConfiguration provider, CancellationToken cancellationToken = default)
		{
			try
			{
				var client = CreateClient(provider);
				await client.ListModelDescriptorsAsync(cancellationToken);
				return true;
			}
			catch (Exception ex)
			{
				Log.Warning(ex, "Failed to check connection for model provider {provider}", provider.Name);
				return false;
			}
		}

		public async Task RefreshModelsAsync(ModelProviderConfiguration provider, CancellationToken cancellationToken = default)
		{
			var client = CreateClient(provider);
			var models = await client.ListModelDescriptorsAsync(cancellationToken);
			provider.Models = [..models.Select(m => ConvertModelFromRCLLM(m))];
		}

		private LLMClient? TryCreateClient(string provider)
		{
			var foundProvider = providers.ModelProviders.FirstOrDefault(p => p.Name == provider);
			if (foundProvider == null)
				return null;
			try
			{
				return CreateClient(foundProvider);
			}
			catch (Exception ex)
			{
				Log.Warning(ex, "Failed to create client for model provider {provider}", provider);
				return null;
			}
		}

		private LLMClient CreateClient(string provider)
		{
			var foundProvider = providers.ModelProviders.FirstOrDefault(p => p.Name == provider);
			if (foundProvider == null)
				throw new ArgumentException($"Provider '{provider}' not found. Please check the name of the provider." +
					$"Expected one of: " + string.Join(", ", providers.ModelProviders.Select(p => p.Name)), nameof(provider));
			return CreateClient(foundProvider);
		}

		private LLMClient CreateClient(ModelProviderConfiguration provider)
		{
			if (!providerTypesMap.TryGetValue(provider.Type, out var providerType))
				throw new InvalidOperationException($"Provider '{provider.Name}' not found in the provider types map."
					+ $"Expected one of: " + string.Join(", ", providerTypesMap.Keys));
			var apiKey = apiKeyManager.GetTokenAccessor(provider.ApiKeyId);
			var client = providerType.CreateClient(provider, apiKey);
			client.Name = provider.Name;
			client.DisplayName = provider.Name;
			return client;
		}

		private static ModelDescriptor ConvertModelFromRCLLM(LLModelDescriptor descriptor)
		{
			return new ModelDescriptor
			{
				Name = descriptor.Name,

				IsInformationKnown = descriptor.Capabilities != LLMCapabilities.Unknown ||
									 descriptor.InputModalities != LLMModalities.Unknown ||
									 descriptor.OutputModalities != LLMModalities.Unknown,

				DisplayName = descriptor.DisplayName,

				InputModalities = descriptor.InputModalities,
				OutputModalities = descriptor.OutputModalities,
				Capabilities = descriptor.Capabilities,

				ContextSize = descriptor.ContextLength,
				MaxOutputTokens = -1,

				InputTokenCost = 0.0m,
				InputCacheTokenCost = 0.0m,
				OutputTokenCost = 0.0m
			};
		}

		private ModelModifier FindModifier(string name)
		{
			var modifier = modifiers.Modifiers.FirstOrDefault(m => m.Name == name);
			if (modifier is null)
				throw new ArgumentException($"Modifier '{name}' not found in the model modifiers configuration.", nameof(name));
			return modifier;
		}
	}
}
