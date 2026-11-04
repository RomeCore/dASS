using LLMDesktopAssistant.Addons.Sources;

namespace LLMDesktopAssistant.Addons
{
	/// <summary>
	/// Marks a class as an <see cref="IAddonTypeDescriptor"/> that is auto-discovered by
	/// <see cref="Services.Configurators.AddonServicesConfigurator"/>-based registration.
	/// Descriptors define the closed addon CLR types for which generic addon services
	/// (<see cref="IFileAddonAccessor{T}"/>, <see cref="IReactiveAddonLoader{T}"/>,
	/// <see cref="Loading.IDiagnosticAddonFactory{T}"/>) are registered.
	/// </summary>
	[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
	public sealed class AddonTypeDescriptorAttribute : Attribute
	{
	}
}
