using MonoMod.Utils;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace HarmonyLib
{
	internal enum InjectionType
	{
		Unknown,
		Instance,
		OriginalMethod,
		OriginalMember,
		ArgsArray,
		Result,
		ResultRef,
		State,
		Exception,
		RunOriginal
	}

	internal class InjectedParameter
	{
		internal ParameterInfo parameterInfo;
		internal string realName;
		internal InjectionType injectionType;
		internal ArgumentMode argumentMode;
		internal bool outer;

		internal InjectionType TypeFor(bool infix) => !infix && injectionType == InjectionType.OriginalMember ? InjectionType.Unknown : injectionType;

		internal const string INSTANCE_PARAM = "__instance";
		internal const string ORIGINAL_METHOD_PARAM = "__originalMethod";
		internal const string ARGS_ARRAY_VAR = "__args";
		internal const string RESULT_VAR = "__result";
		internal const string RESULT_REF_VAR = "__resultRef";
		internal const string STATE_VAR = "__state";
		internal const string EXCEPTION_VAR = "__exception";
		internal const string RUN_ORIGINAL_VAR = "__runOriginal";

		internal InjectedParameter(MethodInfo method, ParameterInfo parameterInfo)
		{
			this.parameterInfo = parameterInfo;
			// Dynamic patch methods cannot carry HarmonyOuter, and Mono cannot inspect their parameter attributes.
			outer = !method.IsDynamicMethod() && parameterInfo.GetCustomAttributes(true).Any(attribute => attribute.GetType().FullName == "HarmonyLib.HarmonyOuter");
			var arg = parameterInfo.GetArgumentAttribute();
			argumentMode = arg?.Mode ?? ArgumentMode.Default;
			if (argumentMode is ArgumentMode.Original or ArgumentMode.Captured or ArgumentMode.Persistent)
			{
				realName = arg.NewName;
				injectionType = InjectionType.Unknown;
			}
			else
			{
				realName = CalculateRealName(method, arg);
				injectionType = Type(realName);
			}
		}

		string CalculateRealName(MethodInfo method, HarmonyArgument arg)
		{
			var baseArgs = method.GetArgumentAttributes();
			if (method.DeclaringType is not null)
				baseArgs = baseArgs.Union(method.DeclaringType.GetArgumentAttributes());
			if (arg != null)
				return arg.OriginalName ?? parameterInfo.Name ?? string.Empty;
			return baseArgs.GetRealName(parameterInfo.Name, null) ?? parameterInfo.Name ?? string.Empty;
		}

		static readonly Dictionary<string, InjectionType> types = new()
		{
			{ INSTANCE_PARAM, InjectionType.Instance },
			{ ORIGINAL_METHOD_PARAM, InjectionType.OriginalMethod },
			{ "__originalMember", InjectionType.OriginalMember },
			{ ARGS_ARRAY_VAR, InjectionType.ArgsArray },
			{ RESULT_VAR, InjectionType.Result },
			{ RESULT_REF_VAR, InjectionType.ResultRef },
			{ STATE_VAR, InjectionType.State },
			{ EXCEPTION_VAR, InjectionType.Exception },
			{ RUN_ORIGINAL_VAR, InjectionType.RunOriginal },
		};

		static InjectionType Type(string name)
		{
			if (types.TryGetValue(name, out var type))
				return type;
			return InjectionType.Unknown;
		}
	}
}
