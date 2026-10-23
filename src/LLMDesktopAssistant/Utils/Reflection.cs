using System.Collections.Concurrent;
using System.Reflection;

namespace LLMDesktopAssistant.Utils
{
	public static class Reflection
	{
		private static ConcurrentDictionary<Type, ConcurrentDictionary<string, PropertyInfo>> _propertyCache = [];

		public static PropertyInfo? TryGetProperty(this Type type, string propertyName)
		{
			if (!_propertyCache.TryGetValue(type, out var propertyCache))
			{
				propertyCache = new ConcurrentDictionary<string, PropertyInfo>();

				foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.NonPublic |
					BindingFlags.Instance | BindingFlags.Static))
					propertyCache.TryAdd(property.Name, property);

				_propertyCache.TryAdd(type, propertyCache);
			}

			return propertyCache.GetValueOrDefault(propertyName);
		}

		public static PropertyInfo GetProperty(this Type type, string propertyName)
		{
			return TryGetProperty(type, propertyName) ??
				throw new ArgumentException($"Property '{propertyName}' not found on type '{type}'.");
		}

		/// <summary>
		/// Gets the value of a property on an object via reflection (public and non-public).
		/// If the property is not found, an exception is thrown.
		/// </summary>
		public static object? GetPropertyValue(this object obj, string propertyName, params object?[] index)
		{
			var property = GetProperty(obj.GetType(), propertyName);
			return property.GetValue(obj, index);
		}

		/// <summary>
		/// Gets the value of a property on an object via reflection (public and non-public).
		/// If the property is not found, an exception is thrown.
		/// </summary>
		public static T GetPropertyValue<T>(this object obj, string propertyName, params object?[] index)
		{
			var property = GetProperty(obj.GetType(), propertyName);
			return (T)property.GetValue(obj, index)!;
		}

		/// <summary>
		/// Sets the value of a property on an object via reflection (public and non-public).
		/// If the property is not found, an exception is thrown.
		/// </summary>
		public static void SetPropertyValue(this object obj, string propertyName, object? value, params object?[] index)
		{
			var property = GetProperty(obj.GetType(), propertyName);
			property.SetValue(obj, value, index);
		}
	}
}
