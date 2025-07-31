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
			realName = CalculateRealName(method);
			injectionType = Type(realName);
		}

		string CalculateRealName(MethodInfo method)
		{
			var baseArgs = method.GetArgumentAttributes();
			if (method.DeclaringType is not null)
				baseArgs = baseArgs.Union(method.DeclaringType.GetArgumentAttributes());
			var arg = parameterInfo.GetArgumentAttribute();
			if (arg != null)
				return arg.OriginalName ?? parameterInfo.Name;
			return baseArgs.GetRealName(parameterInfo.Name, null) ?? parameterInfo.Name;
		}

		static readonly Dictionary<string, InjectionType> types = new()
		{
			{ INSTANCE_PARAM, InjectionType.Instance },
			{ ORIGINAL_METHOD_PARAM, InjectionType.OriginalMethod },
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
