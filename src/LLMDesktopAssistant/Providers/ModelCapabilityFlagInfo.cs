using Avalonia.Media;
using LLMDesktopAssistant.Localization;
using Material.Icons;
using RCLargeLanguageModels;

namespace LLMDesktopAssistant.Providers
{
	/// <summary>
	/// Represents a modality flag for display as a colored icon with tooltip.
	/// </summary>
	public class ModelModalityFlagInfo
	{
		/// <summary>
		/// The modality value.
		/// </summary>
		public LLMModalities Modality { get; }

		/// <summary>
		/// Localized display name.
		/// </summary>
		public string DisplayName { get; }

		/// <summary>
		/// The icon to display.
		/// </summary>
		public VisualIconKind Icon { get; }

		/// <summary>
		/// The color associated with this modality.
		/// </summary>
		public IBrush Color { get; }

		public ModelModalityFlagInfo(LLMModalities modality, string displayName, VisualIconKind icon, IBrush color)
		{
			Modality = modality;
			DisplayName = displayName;
			Icon = icon;
			Color = color;
		}

		/// <summary>
		/// Creates a list of flag infos from an <see cref="LLMModalities"/> flags value.
		/// </summary>
		public static List<ModelModalityFlagInfo> FromModalities(LLMModalities modalities)
		{
			var result = new List<ModelModalityFlagInfo>();
			if (modalities == LLMModalities.Unknown)
			{
				result.Add(new ModelModalityFlagInfo(LLMModalities.Unknown, LocalizationManager.LocalizeStatic("model.modality.unknown"), MaterialIconKind.HelpCircle, Brushes.Gray));
				return result;
			}

			if (modalities.HasFlag(LLMModalities.Text))
				result.Add(new ModelModalityFlagInfo(LLMModalities.Text, LocalizationManager.LocalizeStatic("model.modality.text"), MaterialIconKind.FormatText, Brushes.LimeGreen));
			if (modalities.HasFlag(LLMModalities.Image))
				result.Add(new ModelModalityFlagInfo(LLMModalities.Image, LocalizationManager.LocalizeStatic("model.modality.image"), MaterialIconKind.Image, Brushes.DodgerBlue));
			if (modalities.HasFlag(LLMModalities.Audio))
				result.Add(new ModelModalityFlagInfo(LLMModalities.Audio, LocalizationManager.LocalizeStatic("model.modality.audio"), MaterialIconKind.Music, Brushes.Orange));
			if (modalities.HasFlag(LLMModalities.Video))
				result.Add(new ModelModalityFlagInfo(LLMModalities.Video, LocalizationManager.LocalizeStatic("model.modality.video"), MaterialIconKind.Video, Brushes.MediumPurple));

			return result;
		}
	}

	/// <summary>
	/// Represents a capability flag for display as a colored icon with tooltip.
	/// </summary>
	public class ModelCapabilityFlagInfo
	{
		/// <summary>
		/// The capability value.
		/// </summary>
		public LLMCapabilities Capability { get; }

		/// <summary>
		/// Localized display name.
		/// </summary>
		public string DisplayName { get; }

		/// <summary>
		/// The icon to display.
		/// </summary>
		public VisualIconKind Icon { get; }

		/// <summary>
		/// The color associated with this capability.
		/// </summary>
		public IBrush Color { get; }

		public ModelCapabilityFlagInfo(LLMCapabilities capability, string displayName, VisualIconKind icon, IBrush color)
		{
			Capability = capability;
			DisplayName = displayName;
			Icon = icon;
			Color = color;
		}

		/// <summary>
		/// Creates a list of flag infos from an <see cref="LLMCapabilities"/> flags value.
		/// </summary>
		public static List<ModelCapabilityFlagInfo> FromCapabilities(LLMCapabilities capabilities)
		{
			var result = new List<ModelCapabilityFlagInfo>();
			if (capabilities == LLMCapabilities.Unknown)
			{
				result.Add(new ModelCapabilityFlagInfo(LLMCapabilities.Unknown, LocalizationManager.LocalizeStatic("model.capability.unknown"), MaterialIconKind.HelpCircle, Brushes.Gray));
				return result;
			}

			if (capabilities.HasFlag(LLMCapabilities.ChatCompletions))
				result.Add(new ModelCapabilityFlagInfo(LLMCapabilities.ChatCompletions, LocalizationManager.LocalizeStatic("model.capability.chat"), MaterialIconKind.Chat, Brushes.LimeGreen));
			if (capabilities.HasFlag(LLMCapabilities.Completions))
				result.Add(new ModelCapabilityFlagInfo(LLMCapabilities.Completions, LocalizationManager.LocalizeStatic("model.capability.completion"), MaterialIconKind.CodeBraces, Brushes.DodgerBlue));
			if (capabilities.HasFlag(LLMCapabilities.Embeddings))
				result.Add(new ModelCapabilityFlagInfo(LLMCapabilities.Embeddings, LocalizationManager.LocalizeStatic("model.capability.embeddings"), MaterialIconKind.VectorArrangeAbove, Brushes.Orange));
			if (capabilities.HasFlag(LLMCapabilities.Reranking))
				result.Add(new ModelCapabilityFlagInfo(LLMCapabilities.Reranking, LocalizationManager.LocalizeStatic("model.capability.reranking"), MaterialIconKind.Sort, Brushes.MediumPurple));
			if (capabilities.HasFlag(LLMCapabilities.ToolSupport))
				result.Add(new ModelCapabilityFlagInfo(LLMCapabilities.ToolSupport, LocalizationManager.LocalizeStatic("model.capability.tools"), MaterialIconKind.Tools, Brushes.Gold));
			if (capabilities.HasFlag(LLMCapabilities.Reasoning))
				result.Add(new ModelCapabilityFlagInfo(LLMCapabilities.Reasoning, LocalizationManager.LocalizeStatic("model.capability.reasoning"), MaterialIconKind.LightbulbOn, Brushes.HotPink));
			if (capabilities.HasFlag(LLMCapabilities.StreamingCompletions))
				result.Add(new ModelCapabilityFlagInfo(LLMCapabilities.StreamingCompletions, LocalizationManager.LocalizeStatic("model.capability.streaming"), MaterialIconKind.RadioTower, Brushes.Cyan));

			return result;
		}
	}
}
