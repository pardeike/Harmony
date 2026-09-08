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
		readonly Func<InjectionStorage> resolve;

		internal InjectionStorage(Type type, int argumentIndex)
		{
			this.type = type;
			this.argumentIndex = argumentIndex;
			local = null;
			resolve = null;
		}

		internal InjectionStorage(LocalBuilder local)
		{
			type = local.LocalType;
			argumentIndex = -1;
			this.local = local;
			resolve = null;
		}

		// A helper materializes a caller-backed parameter only when the binder actually uses its storage.
		internal InjectionStorage(Type type, Func<InjectionStorage> resolve)
		{
			this.type = type;
			this.resolve = resolve;
			argumentIndex = -1;
			local = null;
		}

		internal CodeInstruction Load() => resolve != null ? resolve().Load() : local is null ? Ldarg[argumentIndex] : Ldloc[local];
		internal CodeInstruction LoadAddress() => resolve != null ? resolve().LoadAddress() : local is null ? Ldarga[argumentIndex] : Ldloca[local];
		internal CodeInstruction Store() => resolve != null ? resolve().Store() : local is null ? Starg[argumentIndex] : Stloc[local];
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
		internal Type receiverParameterType;
		internal readonly InjectionStorage[] arguments;
		internal readonly VariableState variables;
		internal bool refreshArgumentArray;
		internal InjectionStorage[] originalLocals;
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
			receiverParameterType = receiver?.type;
		}

		internal PatchBindingContext(MemberInfo member, Type returnType, BindingParameter[] parameters, Type receiverType,
			InjectionStorage? receiver, InjectionStorage[] arguments, VariableState variables)
		{
			this.member = member;
			isStatic = receiver is null;
			this.receiverType = receiverType;
			this.receiver = receiver;
			receiverParameterType = receiver?.type;
			this.arguments = arguments;
			this.variables = variables;
			this.returnType = returnType;
			this.parameters = parameters;
			parameterNames = [.. parameters.Select(parameter => parameter.Name)];
		}
	}
}
