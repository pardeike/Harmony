using System;
using System.Globalization;
using System.Reflection;
using System.Reflection.Emit;

namespace HarmonyLib
{
	// Keep the shared Dictionary<MethodInfo, MethodBase> usable by older Harmony readers without
	// making its keys retain collectible generated assemblies. Only dictionary keys use this wrapper.
	internal sealed class WeakMethodInfo : MethodInfo
	{
		readonly WeakReference assembly;
		readonly int token;
		readonly int hash;

		internal WeakMethodInfo(MethodInfo method)
		{
			assembly = new WeakReference(method.Module.Assembly);
			token = method.MetadataToken;
			hash = method.GetHashCode();
		}

		internal bool IsAlive => assembly.IsAlive;
		MethodInfo Method => assembly.Target is Assembly owner ? (MethodInfo)owner.ManifestModule.ResolveMethod(token)
			: throw new InvalidOperationException("The generated method has been collected");

		public override bool Equals(object obj)
		{
			if (ReferenceEquals(this, obj)) return true;
			var owner = assembly.Target;
			if (owner is null) return false;
			return obj is WeakMethodInfo weak ? token == weak.token && ReferenceEquals(owner, weak.assembly.Target)
				: obj is MethodInfo method && method is not DynamicMethod && token == method.MetadataToken && ReferenceEquals(owner, method.Module.Assembly);
		}

		public override int GetHashCode() => hash;
		public override string Name => Method.Name;
		public override Type DeclaringType => Method.DeclaringType;
		public override Type ReflectedType => Method.ReflectedType;
		public override Module Module => Method.Module;
		public override int MetadataToken => token;
		public override MethodAttributes Attributes => Method.Attributes;
		public override RuntimeMethodHandle MethodHandle => Method.MethodHandle;
		public override Type ReturnType => Method.ReturnType;
		public override ICustomAttributeProvider ReturnTypeCustomAttributes => Method.ReturnTypeCustomAttributes;
		public override MethodInfo GetBaseDefinition() => Method.GetBaseDefinition();
		public override MethodImplAttributes GetMethodImplementationFlags() => Method.GetMethodImplementationFlags();
		public override ParameterInfo[] GetParameters() => Method.GetParameters();
		public override object[] GetCustomAttributes(bool inherit) => Method.GetCustomAttributes(inherit);
		public override object[] GetCustomAttributes(Type attributeType, bool inherit) => Method.GetCustomAttributes(attributeType, inherit);
		public override bool IsDefined(Type attributeType, bool inherit) => Method.IsDefined(attributeType, inherit);
		public override object Invoke(object obj, BindingFlags invokeAttr, Binder binder, object[] parameters, CultureInfo culture)
			=> Method.Invoke(obj, invokeAttr, binder, parameters, culture);
	}
}
