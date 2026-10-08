using System.Text.Json.Serialization;
using LiteDB;
using LLMDesktopAssistant.Agents;
using LLMDesktopAssistant.StructuredValues.Parameterization;
using YamlDotNet.Serialization;

namespace LLMDesktopAssistant.Addons
{
	public abstract class AddonChangedBase<Self, TChange> : AddonBase<Self>
		where Self : AddonBase<Self>
		where TChange : AddonChangeBase
	{
		/// <summary>
		/// The key that identifies the addon for deduplication and change resolution: addons that share a
		/// <see cref="Key"/> are one logical addon (the collector collapses them into <see cref="Overrides"/>), and a
		/// per-chat change is stored under it. Defaults to the addon name; an addon whose name is not unique across its
		/// namespaces (a command, for example) overrides it.
		/// </summary>
		[JsonIgnore]
		[BsonIgnore]
		[YamlIgnore]
		public virtual string Key => Name;

		/// <summary>
		/// The order that used when addons with same name is conflicting.
		/// The addon with the higher <see cref="OverrideOrder"/> wins, others puts to <see cref="Overrides"/>.
		/// </summary>
		public int OverrideOrder
		{
			get;
			set => SetProperty(ref field, value);
		} = 0;

		// ===================================
		// === Variable state              ===
		// ===================================

		/// <summary>
		/// Gets a value indicating whether the addon is fixed (always enabled and visible).
		/// If set, <see cref="Enabled"/> will be always set to <see langword="true"/> and
		/// <see cref="Hidden"/> will be always set to <see langword="false"/> in
		/// the <see cref="IAddonSetCollector{T}"/>.
		/// </summary>
		public bool IsFixed
		{
			get;
			set => SetProperty(ref field, value);
		} = false;

		/// <summary>
		/// Gets a value indicating whether the addon is enabled. Defaults to <see langword="null"/>.
		/// </summary>
		public bool? Enabled
		{
			get;
			set => SetProperty(ref field, value);
		} = null;

		/// <summary>
		/// Gets a value indicating whether the addon is hidden (not visible in
		/// system prompt for agents, but still available for agents to search and use).
		/// Defaults to <see langword="null"/>.
		/// </summary>
		public bool? Hidden
		{
			get;
			set => SetProperty(ref field, value);
		} = null;

		/// <summary>
		/// Defines a predicate used for detemining that addon is currently available at this time at chat-level.
		/// Note that this predicate ignores <see cref="IsFixed"/>.
		/// </summary>
		[JsonIgnore]
		[BsonIgnore]
		[YamlIgnore]
		public Func<Self, IServiceProvider, bool>? ChatAvailablePredicate
		{
			get;
			set => SetProperty(ref field, value);
		} = null;

		/// <summary>
		/// Defines a predicate used for detemining that addon is currently available at this time at agent-level.
		/// Note that this predicate ignores <see cref="IsFixed"/> and also <see cref="ChatAvailablePredicate"/>
		/// will be checked before trying this predicate.
		/// </summary>
		[JsonIgnore]
		[BsonIgnore]
		[YamlIgnore]
		public Func<Self, IServiceProvider, ChatAgentDescriptor, bool>? AgentAvailablePredicate
		{
			get;
			set => SetProperty(ref field, value);
		} = null;

		/// <summary>
		/// The change confiuration object that been used to make some changes to this addon instance.
		/// </summary>
		[JsonIgnore]
		[BsonIgnore]
		[YamlIgnore]
		public TChange? Change
		{
			get;
			set => SetProperty(ref field, value);
		} = null;

		/// <summary>
		/// The parameter schema of the addon template, if the addon body is a template with @params metadata.
		/// Null for plain-text addons without parameters.
		/// </summary>
		public ParameterSchema? ParameterSchema
		{
			get;
			set => SetProperty(ref field, value);
		} = null;

		/// <summary>
		/// Gets the list of overriden addons during deduplication by name.
		/// </summary>
		[JsonIgnore]
		[BsonIgnore]
		[YamlIgnore]
		public ImmutableList<Self> Overrides
		{
			get;
			set => SetProperty(ref field, value);
		} = [];
	}
}
