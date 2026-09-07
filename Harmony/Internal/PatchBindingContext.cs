using System;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using static HarmonyLib.Code;

namespace HarmonyLib
{
	// A slot contains either a value or the managed pointer supplied by a ref/out argument.
	// Loading a pointer slot preserves that pointer; the binder decides when to dereference it.
	internal readonly struct InjectionStorage
	{
		internal readonly Type type;
		readonly int argumentIndex;
		readonly LocalBuilder local;

		internal InjectionStorage(Type type, int argumentIndex)
		{
			this.type = type;
			this.argumentIndex = argumentIndex;
			local = null;
		}

		internal InjectionStorage(LocalBuilder local)
		{
			type = local.LocalType;
			argumentIndex = -1;
			this.local = local;
		}

		internal CodeInstruction Load() => local is null ? Ldarg[argumentIndex] : Ldloc[local];
		internal CodeInstruction LoadAddress() => local is null ? Ldarga[argumentIndex] : Ldloca[local];
		internal CodeInstruction Store() => local is null ? Starg[argumentIndex] : Stloc[local];
	}

	internal readonly struct BindingParameter(string name, Type type, bool isOut = false, bool isRetval = false)
	{
		internal readonly string Name = name;
		internal readonly Type ParameterType = type;
		internal readonly bool IsOut = isOut;
		internal readonly bool IsRetval = isRetval;

		internal static BindingParameter[] From(MethodBase method) => [.. method.GetParameters()
			.Select(parameter => new BindingParameter(parameter.Name, parameter.ParameterType, parameter.IsOut, parameter.IsRetval))];
	}

	internal sealed class PatchBindingContext
	{
		internal readonly MemberInfo member;
		internal readonly bool isStatic;
		internal readonly Type returnType;
		internal readonly Type receiverType;
		internal readonly BindingParameter[] parameters;
		internal readonly string[] parameterNames;
		internal readonly InjectionStorage? receiver;
		internal readonly InjectionStorage[] arguments;
		internal readonly VariableState variables;
		internal bool refreshArgumentArray;
		internal string Description => member is MethodBase method ? method.FullDescription() : member?.ToString() ?? "constant load";

		internal PatchBindingContext(MethodBase method, VariableState variables)
		{
			member = method;
			isStatic = method.IsStatic;
			this.variables = variables;
			returnType = AccessTools.GetReturnedType(method);
			receiverType = method.DeclaringType;
			parameters = BindingParameter.From(method);
			parameterNames = [.. parameters.Select(parameter => parameter.Name)];
			arguments = [.. parameters.Select((parameter, index) => new InjectionStorage(parameter.ParameterType, index + (method.IsStatic ? 0 : 1)))];
			if (!method.IsStatic)
				receiver = new InjectionStorage(receiverType.IsValueType ? receiverType.MakeByRefType() : receiverType, 0);
		}

		internal PatchBindingContext(MethodBase method, Type receiverType, InjectionStorage? receiver, InjectionStorage[] arguments, VariableState variables)
			: this(method, AccessTools.GetReturnedType(method), BindingParameter.From(method), receiverType, receiver, arguments, variables) { }

		internal PatchBindingContext(MemberInfo member, Type returnType, BindingParameter[] parameters, Type receiverType,
			InjectionStorage? receiver, InjectionStorage[] arguments, VariableState variables)
		{
			this.member = member;
			isStatic = receiver is null;
			this.receiverType = receiverType;
			this.receiver = receiver;
			this.arguments = arguments;
			this.variables = variables;
			this.returnType = returnType;
			this.parameters = parameters;
			parameterNames = [.. parameters.Select(parameter => parameter.Name)];
		}
	}
}
