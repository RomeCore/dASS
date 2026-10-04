using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json.Serialization;
using LiteDB;
using LLMDesktopAssistant.Localization;
using LLMDesktopAssistant.Prompting;
using LLMDesktopAssistant.StructuredValues.Const;
using LLMDesktopAssistant.Utils;
using Material.Icons;
using YamlDotNet.Serialization;

namespace LLMDesktopAssistant.Addons
{
	/// <summary>
	/// Represents a base class of all agentic addons (tools, skills, prompt parts, sub-agents, commands, etc.).
	/// </summary>
	/// <remarks>
	/// The object will be frozen after full initialization. Any changes to the object after that will result in an exception.
	/// </remarks>
	/// <typeparam name="Self">The type of the derived class. Used for covariance.</typeparam>
	public abstract class AddonBase<Self> : AddonMetadata
		where Self : AddonBase<Self>
	{
		/// <summary>
		/// The name used to identify the addon for deduplication and agent selection purposes.
		/// Usually has constraints like only alpha-numeric characters, underscores and hyphens.
		/// </summary>
		public string Name
		{
			get;
			set => SetProperty(ref field, value);
		} = string.Empty;

		/// <summary>
		/// The order in which addons will be sorted and provided.
		/// Addons are sorted by this property, then by <see cref="Name"/>.
		/// </summary>
		public int Order
		{
			get;
			set => SetProperty(ref field, value);
		} = 0;

		/// <summary>
		/// The aliases for the addon. These are alternative names that can be used to invoke the addon.
		/// </summary>
		public ImmutableList<string> Aliases
		{
			get;
			set => SetProperty(ref field, value);
		} = [];

		/// <summary>
		/// The dynamic description getter for the addon.
		/// </summary>
		[JsonIgnore]
		[BsonIgnore]
		[YamlIgnore]
		public Func<Self, string> DescriptionGetter
		{
			get;
			set => SetProperty(ref field, value);
		} = s => string.Empty;

		/// <summary>
		/// The short agent-readable description of the addon. Used by agent for understanding when to use this addon.
		/// </summary>
		public string Description
		{
			get => DescriptionGetter((Self)this);
			set
			{
				if (value != Description)
				{
					RaisePropertyChanging();
					DescriptionGetter = s => value;
					RaisePropertyChanged();
				}
			}
		}

		/// <summary>
		/// The addon file content getter, excluding the frontmatter.
		/// </summary>
		[JsonIgnore]
		[BsonIgnore]
		[YamlIgnore]
		public Func<Self, string> BodyGetter
		{
			get;
			set => SetProperty(ref field, value);
		} = s => string.Empty;

		/// <summary>
		/// The addon file content getter, excluding the frontmatter.
		/// </summary>
		public string Body
		{
			get => BodyGetter((Self)this);
			set
			{
				if (value != Body)
				{
					RaisePropertyChanging();
					BodyGetter = s => value;
					RaisePropertyChanged();
				}
			}
		}

		/// <summary>
		/// The icon for the addon. Used by the UI to display the addon card.
		/// </summary>
		public MaterialIconKind? Icon
		{
			get;
			set => SetProperty(ref field, value);
		}

		/// <summary>
		/// The locale key for the name. If not set, it will be automatically generated from the name.
		/// Used for user-friendly display.
		/// </summary>
		[NotFrozen]
		public LocaleKeyBase NameKey
		{
			get => field ??= Locale.GetConstKey(Name);
			set => SetProperty(ref field, value);
		}

		/// <summary>
		/// The locale key for the description. If not set, it will be automatically generated from the description.
		/// Used for user-friendly display of the addon's description.
		/// </summary>
		[NotFrozen]
		public LocaleKeyBase DescriptionKey
		{
			get => field ??= Locale.GetConstKey(Description);
			set => SetProperty(ref field, value);
		}

		/// <summary>
		/// The category of the addon. Used for organizing and filtering addons in the UI.
		/// </summary>
		[NotFrozen]
		public LocaleKeyBase? CategoryKey
		{
			get;
			set => SetProperty(ref field, value);
		}

		// ===================================
		// === Directories and paths       ===
		// ===================================

		/// <summary>
		/// Full path to main addon file, if applicable. This is 'SKILL.md' file for skills, 'my-tool.lua' for metatools.
		/// </summary>
		public string? Path
		{
			get;
			set => SetProperty(ref field, value);
		}

		/// <summary>
		/// The home directory of the addon. If not set, it will be automatically generated from the path.
		/// </summary>
		public string? HomeDirectory
		{
			get => field ??= System.IO.Path.GetDirectoryName(Path);
			set => SetProperty(ref field, value);
		}

		/// <summary>
		/// The source of the addon.
		/// </summary>
		public AddonSource AddonSource
		{
			get;
			set => SetProperty(ref field, value);
		} = AddonSource.Unknown;

		/// <summary>
		/// The optional source pack that this addon is part of. Used for organizing and filtering addons in the UI.
		/// </summary>
		[JsonIgnore]
		public AddonPackInfo? SourcePack
		{
			get;
			set => SetProperty(ref field, value);
		}

		/// <summary>
		/// The optional source of the template that this addon is created from.
		/// </summary>
		[JsonIgnore]
		public PromptPartSource? TemplateSource
		{
			get;
			set => SetProperty(ref field, value);
		}

		/// <summary>
		/// The additional properties associated with the addon.
		/// Used for root properties that are not covered by other properties of this class.
		/// </summary>
		public ImmutableDictionary<string, ConstNodeValue> AdditionalProperties
		{
			get;
			set => SetProperty(ref field, value);
		} = [];

		// ===================================
		// === Diagnostics                 ===
		// ===================================

		/// <summary>
		/// The diagnostic information associated with the addon.
		/// </summary>
		[JsonIgnore]
		public AddonDiagnostic? Diagnostic
		{
			get;
			set => SetProperty(ref field, value);
		}

		/// <summary>
		/// Expands the addon's diagnostic information with the provided diagnostic information.
		/// </summary>
		/// <param name="diagnostic">The diagnostic information to expand with.</param>
		public void ExpandDiagnostic(AddonDiagnostic diagnostic)
		{
			Diagnostic = AddonDiagnostic.Combine(Diagnostic, diagnostic);
		}

		/// <summary>
		/// Checks if the required properties of the addon are set. If not, it throws an <see cref="ArgumentException"/>.
		/// </summary>
		/// <exception cref="ArgumentException">Thrown when any of the required properties are not set.</exception>
		public void ValidateProperties()
		{
			AppendOnlyList<string> errors = [];

			ValidatePropertiesCore(errors);

			if (errors.Count > 0)
				throw new ArgumentException(string.Join(Environment.NewLine, errors));
		}

		/// <summary>
		/// Checks the required properties of the addon. This method can be overridden by derived classes to add additional checks.
		/// </summary>
		/// <param name="errors">The list of errors to add to. If any required properties are not set, their error message should be added to this list.</param>
		protected virtual void ValidatePropertiesCore(AppendOnlyList<string> errors)
		{
			if (string.IsNullOrEmpty(Name))
				errors.Add("Name is required.");
			if (Description is null)
				errors.Add("Description is required.");
		}

		// Interesting fact:
		// In C#, static class fields are separated by generic arguments (the Self type in our case),
		// so we can just put this field here without dictionaries!
		private static readonly Func<Self, Self>? cloneDelegate = null;

		static AddonBase()
		{
			var type = typeof(Self);

			var constructor = type.GetConstructor(
				BindingFlags.CreateInstance | BindingFlags.Public | BindingFlags.Instance,
				Type.EmptyTypes);

			if (constructor is null)
				return;

			var properties = type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
				.Where(p => !p.IsSpecialName && p.GetMethod is not null && p.SetMethod is not null &&
				p.Name is not nameof(Body) and not nameof(Description));

			var sourceParam = Expression.Parameter(typeof(Self), "source");
			var instanceVar = Expression.Variable(typeof(Self), "instance");

			var expressions = new List<Expression>
			{
				// instance = new Self();
				Expression.Assign(instanceVar, Expression.New(constructor))
			};

			foreach (var property in properties)
			{
				var getValue = Expression.Call(sourceParam, property.GetMethod!);
				var setValue = Expression.Call(instanceVar, property.SetMethod!, getValue);
				expressions.Add(setValue);
			}

			expressions.Add(instanceVar);

			var body = Expression.Block([instanceVar], expressions);
			var lambda = Expression.Lambda<Func<Self, Self>>(body, sourceParam);
			var cloneDelegate = lambda.Compile();

			AddonBase<Self>.cloneDelegate = cloneDelegate;
		}

		/// <summary>
		/// Creates the shallow copy of the current instance of the addon, returning it unfrozen.
		/// </summary>
		public Self Clone()
		{
			if (cloneDelegate is null)
				throw new InvalidOperationException("Clone is not supported for this type. " +
					"Cloneable types must have a parameterless public constructor.");

			return cloneDelegate((Self)this);
		}
	}
}
