using System.Globalization;
using System.Reflection;

namespace LLMDesktopAssistant.MVVM.Dynamic
{
	public class DynamicViewModelPropertyInfo : PropertyInfo
	{
		private readonly Type _declaringType;
		private readonly string _propertyName;

		public DynamicViewModelPropertyInfo(Type declaringType, string propertyName)
		{
			_declaringType = declaringType ?? throw new ArgumentNullException(nameof(declaringType));
			_propertyName = propertyName ?? throw new ArgumentNullException(nameof(propertyName));
		}

		public override string Name => _propertyName;

		public override Type? DeclaringType => _declaringType;

		public override Type? ReflectedType => _declaringType;

		public override PropertyAttributes Attributes => PropertyAttributes.None;

		public override bool CanRead => true;

		public override bool CanWrite => true;

		public override Type PropertyType => typeof(object);

		public override object? GetValue(object? obj, BindingFlags invokeAttr, Binder? binder, object?[]? index, CultureInfo? culture)
		{
			return (obj as DynamicViewModel)?.GetDynamicMember(_propertyName);
		}

		public override void SetValue(object? obj, object? value, BindingFlags invokeAttr, Binder? binder, object?[]? index, CultureInfo? culture)
		{
			(obj as DynamicViewModel)?.SetDynamicMember(_propertyName, value);
		}

		public override MethodInfo[] GetAccessors(bool nonPublic)
		{
			return [];
		}

		public override object[] GetCustomAttributes(bool inherit)
		{
			return [];
		}

		public override object[] GetCustomAttributes(Type attributeType, bool inherit)
		{
			return [];
		}

		public override MethodInfo? GetGetMethod(bool nonPublic)
		{
			return null;
		}

		public override ParameterInfo[] GetIndexParameters()
		{
			return [];
		}

		public override MethodInfo? GetSetMethod(bool nonPublic)
		{
			return null;
		}

		public override bool IsDefined(Type attributeType, bool inherit)
		{
			return false;
		}
	}
}
