using System.Collections.Concurrent;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using LLMDesktopAssistant.Utils;
using Serilog;

namespace LLMDesktopAssistant.MVVM
{
	/// <summary>
	/// Represents a view locator for mapping view models to views.
	/// </summary>
	/// <remarks>
	/// Resolution is forward-only (view model → view). When no exact mapping is found,
	/// the locator walks up the view model's base type chain (nearest ancestor first),
	/// trying each level's exact mapping and then its generic type definition.
	/// </remarks>
	public sealed class ViewLocator : IDataTemplate
	{
		private static readonly Dictionary<Type, Type> _ViewModel_to_View_map;
		private static readonly ConcurrentDictionary<Type, Type?> _resolvedTypesCache = new();

		static ViewLocator()
		{
			List<(Type ViewModelType, Type ViewType)> mappings = [];

			// Try-catch block needed for designer support
			try
			{
				mappings = ReflectionUtility.GetTypesWithAttribute<ViewModelForAttribute>()
					.Select(t => (t.Type, t.Attribute.TargetView))
					.Concat(
						ReflectionUtility.GetTypesWithAttribute<ViewForAttribute>()
							.Select(t => (t.Attribute.TargetViewModel, t.Type))
					)
					.Distinct()
					.ToList();
			}
			catch
			{
			}

			// Validate mappings for correctness.

			var errors = new List<string>();

			foreach (var group in mappings.ToLookup(t => t.ViewModelType).Where(g => g.Count() > 1))
				errors.Add($"Multiple views found for view model type {group.Key}.");

			foreach (var (viewModelType, viewType) in mappings)
			{
				if (viewType.IsAbstract || viewType.IsInterface)
					errors.Add($"View type {viewType} (for view model {viewModelType}) is abstract and cannot be instantiated.");
				else if (viewType.IsGenericTypeDefinition)
					errors.Add($"View type {viewType} (for view model {viewModelType}) is an open generic type and cannot be instantiated.");
				else if (viewType.GetConstructor(Type.EmptyTypes) is null)
					errors.Add($"View type {viewType} (for view model {viewModelType}) has no public parameterless constructor.");
			}

			if (errors.Count > 0)
			{
				foreach (var error in errors)
					Log.Error(error);
				throw new InvalidOperationException($"ViewLocator initialization failed:\n{string.Join("\n", errors)}");
			}

			_ViewModel_to_View_map = mappings.ToDictionary(t => t.ViewModelType, t => t.ViewType);
		}

		/// <summary>
		/// Gets the singleton instance of the <see cref="ViewLocator"/> class.
		/// </summary>
		public static ViewLocator Instance { get; } = new ViewLocator();

		/// <summary>
		/// Resolves the view type for a given view model type.
		/// Walks up the base type chain (nearest ancestor first) when no exact mapping exists.
		/// </summary>
		/// <param name="viewModelType">The type of the view model.</param>
		/// <returns>The type of the view, or null if no mapping is found.</returns>
		public static Type? ResolveViewType(Type? viewModelType)
		{
			if (viewModelType == null)
				return null;

			if (_resolvedTypesCache.TryGetValue(viewModelType, out var cachedViewType))
				return cachedViewType;

			var viewType = ResolveViewTypeCore(viewModelType);
			_resolvedTypesCache.TryAdd(viewModelType, viewType);
			return viewType;
		}

		private static Type? ResolveViewTypeCore(Type viewModelType)
		{
			// Nearest-first: the type itself, then its ancestors, until we hit object.
			for (var type = viewModelType; type is not null && type != typeof(object); type = type.BaseType)
			{
				if (TryMap(type, out var viewType))
					return viewType;
			}

			return null;
		}

		private static bool TryMap(Type viewModelType, out Type? viewType)
		{
			if (_ViewModel_to_View_map.TryGetValue(viewModelType, out var mapped))
			{
				viewType = mapped;
				return true;
			}

			if (viewModelType.IsGenericType &&
				_ViewModel_to_View_map.TryGetValue(viewModelType.GetGenericTypeDefinition(), out mapped))
			{
				viewType = mapped;
				return true;
			}

			viewType = null;
			return false;
		}

		/// <summary>
		/// Selects the appropriate data template based on the view model.
		/// </summary>
		/// <param name="viewModel">The view model.</param>
		/// <returns>The data template for the specified view model.</returns>
		public static object? Resolve(object? viewModel)
		{
			if (ResolveViewType(viewModel?.GetType()) is Type viewType)
			{
				var view = Activator.CreateInstance(viewType);
				if (view is Control fe)
					fe.DataContext = viewModel;
				return view;
			}
			return null;
		}

		/// <summary>
		/// Determines if a view exists for the specified view model type.
		/// </summary>
		/// <param name="viewModelType">The type of the view model.</param>
		/// <returns>True if a view exists for the specified type; otherwise, false.</returns>
		public static bool HasView(Type viewModelType)
		{
			return ResolveViewType(viewModelType) != null;
		}

		/// <summary>
		/// Determines if a view exists for the specified view model type.
		/// </summary>
		/// <typeparam name="T">The type of the view model.</typeparam>
		/// <returns>True if a view exists for the specified type; otherwise, false.</returns>
		public static bool HasView<T>()
		{
			return HasView(typeof(T));
		}

		public Control Build(object? data)
		{
			return Resolve(data) as Control ?? new Label() { Content = "No view found for this model." };
		}

		public bool Match(object? data)
		{
			return data is not null && HasView(data.GetType());
		}
	}
}
