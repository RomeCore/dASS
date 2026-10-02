using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Reflection;

namespace LLMDesktopAssistant.MVVM.Dynamic
{
	public class DynamicViewModelTypeInfo : TypeInfo
	{
		private readonly Type _vmType;
		private readonly ConcurrentDictionary<string, PropertyInfo> _dynamicProperties = [];

		public DynamicViewModelTypeInfo(Type vmType)
		{
			_vmType = vmType ?? throw new ArgumentNullException(nameof(vmType));
		}

		protected override PropertyInfo? GetPropertyImpl(string name, BindingFlags bindingAttr, Binder? binder, Type? returnType, Type[]? types, ParameterModifier[]? modifiers)
		{
			var staticProp = _vmType.GetProperty(name, bindingAttr, binder, returnType, types ?? [], modifiers);
			if (staticProp is not null)
				return staticProp;
			return _dynamicProperties.GetOrAdd(name, n => new DynamicViewModelPropertyInfo(_vmType, n));
		}

		#region Delegated

		public override Assembly Assembly => _vmType.Assembly;

		public override string? AssemblyQualifiedName => _vmType.AssemblyQualifiedName;

		public override Type? BaseType => _vmType.BaseType;

		public override string? FullName => _vmType.FullName;

		public override Guid GUID => _vmType.GUID;

		public override Module Module => _vmType.Module;

		public override string? Namespace => _vmType.Namespace;

		public override Type UnderlyingSystemType => _vmType.UnderlyingSystemType;

		public override string Name => _vmType.Name;

		public override ConstructorInfo[] GetConstructors(BindingFlags bindingAttr)
		{
			return _vmType.GetConstructors(bindingAttr);
		}

		public override object[] GetCustomAttributes(bool inherit)
		{
			return _vmType.GetCustomAttributes(inherit);
		}

		public override object[] GetCustomAttributes(Type attributeType, bool inherit)
		{
			return _vmType.GetCustomAttributes(attributeType, inherit);
		}

		public override Type? GetElementType()
		{
			return _vmType.GetElementType();
		}

		public override EventInfo? GetEvent(string name, BindingFlags bindingAttr)
		{
			return _vmType.GetEvent(name, bindingAttr);
		}

		public override EventInfo[] GetEvents(BindingFlags bindingAttr)
		{
			return _vmType.GetEvents(bindingAttr);
		}

		public override FieldInfo? GetField(string name, BindingFlags bindingAttr)
		{
			return _vmType.GetField(name, bindingAttr);
		}

		public override FieldInfo[] GetFields(BindingFlags bindingAttr)
		{
			return _vmType.GetFields(bindingAttr);
		}

		[return: DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.Interfaces)]
		public override Type? GetInterface(string name, bool ignoreCase)
		{
			return _vmType.GetInterface(name, ignoreCase);
		}

		public override Type[] GetInterfaces()
		{
			return _vmType.GetInterfaces();
		}

		public override MemberInfo[] GetMembers(BindingFlags bindingAttr)
		{
			return _vmType.GetMembers(bindingAttr);
		}

		public override PropertyInfo[] GetProperties(BindingFlags bindingAttr)
		{
			// Reflection bindings resolve members through GetProperty/GetPropertyImpl, not by
			// enumerating properties, so only the CLR properties of the underlying type are listed
			// here. Dynamic members are resolved on demand in GetPropertyImpl.
			return _vmType.GetProperties(bindingAttr);
		}

		public override MethodInfo[] GetMethods(BindingFlags bindingAttr)
		{
			return _vmType.GetMethods(bindingAttr);
		}

		public override Type? GetNestedType(string name, BindingFlags bindingAttr)
		{
			return _vmType.GetNestedType(name, bindingAttr);
		}

		public override Type[] GetNestedTypes(BindingFlags bindingAttr)
		{
			return _vmType.GetNestedTypes(bindingAttr);
		}

		public override object? InvokeMember(string name, BindingFlags invokeAttr, Binder? binder, object? target, object?[]? args, ParameterModifier[]? modifiers, CultureInfo? culture, string[]? namedParameters)
		{
			return _vmType.InvokeMember(name, invokeAttr, binder, target, args, modifiers, culture, namedParameters);
		}

		public override bool IsDefined(Type attributeType, bool inherit)
		{
			return _vmType.IsDefined(attributeType, inherit);
		}

		protected override TypeAttributes GetAttributeFlagsImpl()
		{
			return TypeAttributes.Class | (_vmType.IsPublic ? TypeAttributes.Public : TypeAttributes.NotPublic);
		}

		protected override ConstructorInfo? GetConstructorImpl(BindingFlags bindingAttr, Binder? binder, CallingConventions callConvention, Type[] types, ParameterModifier[]? modifiers)
		{
			return _vmType.GetConstructor(bindingAttr, binder, callConvention, types, modifiers);
		}

		protected override MethodInfo? GetMethodImpl(string name, BindingFlags bindingAttr, Binder? binder, CallingConventions callConvention, Type[]? types, ParameterModifier[]? modifiers)
		{
			return _vmType.GetMethod(name, bindingAttr, binder, callConvention, types ?? [], modifiers);
		}

		protected override bool HasElementTypeImpl()
		{
			return false;
		}

		protected override bool IsArrayImpl()
		{
			return false;
		}

		protected override bool IsByRefImpl()
		{
			return false;
		}

		protected override bool IsCOMObjectImpl()
		{
			return false;
		}

		protected override bool IsPointerImpl()
		{
			return false;
		}

		protected override bool IsPrimitiveImpl()
		{
			return false;
		}

		#endregion
	}
}
